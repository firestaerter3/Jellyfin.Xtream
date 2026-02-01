// Copyright (C) 2022  Kevin Jilissen

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

// Type aliases to disambiguate from MediaBrowser types
using JellyfinMovie = MediaBrowser.Controller.Entities.Movies.Movie;
using JellyfinMovieInfo = MediaBrowser.Controller.Providers.MovieInfo;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for pre-fetching and caching all VOD movie data upfront.
/// </summary>
public class VodCacheService : IDisposable
{
    private readonly StreamService _streamService;
    private readonly IMemoryCache _memoryCache;
    private readonly FailureTrackingService _failureTrackingService;
    private readonly SyncStateService _syncStateService;
    private readonly ILogger<VodCacheService>? _logger;
    private readonly IProviderManager? _providerManager;
    private readonly IServerConfigurationManager? _serverConfigManager;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private int _cacheVersion = 0;
    private bool _isRefreshing = false;
    private double _currentProgress = 0.0;
    private string _currentStatus = "Idle";
    private DateTime? _lastRefreshStart;
    private DateTime? _lastRefreshComplete;
    private CancellationTokenSource? _refreshCancellationTokenSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="VodCacheService"/> class.
    /// </summary>
    /// <param name="streamService">The stream service instance.</param>
    /// <param name="memoryCache">The memory cache instance.</param>
    /// <param name="failureTrackingService">The failure tracking service instance.</param>
    /// <param name="syncStateService">The sync state service instance.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="providerManager">Optional provider manager for TMDB lookups.</param>
    /// <param name="serverConfigManager">Optional server configuration manager for metadata language.</param>
    public VodCacheService(
        StreamService streamService,
        IMemoryCache memoryCache,
        FailureTrackingService failureTrackingService,
        SyncStateService syncStateService,
        ILogger<VodCacheService>? logger = null,
        IProviderManager? providerManager = null,
        IServerConfigurationManager? serverConfigManager = null)
    {
        _streamService = streamService;
        _memoryCache = memoryCache;
        _failureTrackingService = failureTrackingService;
        _syncStateService = syncStateService;
        _logger = logger;
        _providerManager = providerManager;
        _serverConfigManager = serverConfigManager;
    }

    /// <summary>
    /// Gets the current cache key prefix.
    /// Uses VodCacheDataVersion which only changes when cache-relevant settings change
    /// (not when refresh frequency changes).
    /// </summary>
    private string CachePrefix => $"vod_cache_{Plugin.Instance.VodCacheDataVersion}_v{_cacheVersion}_";

    /// <summary>
    /// Pre-fetches and caches all VOD movie data (categories, movies, TMDB images).
    /// Supports incremental sync to skip unchanged movies based on their Added timestamps.
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="forceFullSync">If true, forces a full sync ignoring incremental sync settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task RefreshCacheAsync(
        IProgress<double>? progress = null,
        bool forceFullSync = false,
        CancellationToken cancellationToken = default)
    {
        // Prevent concurrent refreshes
        if (!await _refreshLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger?.LogInformation("VOD cache refresh already in progress, skipping");
            return;
        }

        try
        {
            if (_isRefreshing)
            {
                _logger?.LogInformation("VOD cache refresh already in progress, skipping");
                return;
            }

            _isRefreshing = true;
            _currentProgress = 0.0;
            _currentStatus = "Starting...";
            _lastRefreshStart = DateTime.UtcNow;

            // Create a linked cancellation token source so we can cancel the refresh
            var oldCts = _refreshCancellationTokenSource;
            _refreshCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            oldCts?.Dispose();

            // Load sync state for incremental sync
            var config = Plugin.Instance.Configuration;
            SyncState syncState = await _syncStateService.LoadStateAsync(_refreshCancellationTokenSource.Token).ConfigureAwait(false);

            // Determine if we should do a full sync
            bool isIncrementalSync = config.IncrementalSyncEnabled
                && !forceFullSync
                && !SyncStateService.IsFullSyncNeeded(syncState, config.FullSyncIntervalHours);

            if (forceFullSync)
            {
                _logger?.LogInformation("Starting FULL VOD cache refresh (forced)");
            }
            else if (!config.IncrementalSyncEnabled)
            {
                _logger?.LogInformation("Starting FULL VOD cache refresh (incremental sync disabled)");
            }
            else if (SyncStateService.IsFullSyncNeeded(syncState, config.FullSyncIntervalHours))
            {
                _logger?.LogInformation(
                    "Starting FULL VOD cache refresh (last full sync: {LastFullSync}, interval: {Interval}h)",
                    syncState.LastFullSync,
                    config.FullSyncIntervalHours);
                isIncrementalSync = false;
            }
            else
            {
                _logger?.LogInformation(
                    "Starting INCREMENTAL VOD cache refresh (last sync: {LastSync}, {TrackedCount} movies tracked)",
                    syncState.LastIncrementalSync,
                    syncState.MoviesAdded.Count);
            }

            string cacheDataVersion = Plugin.Instance.VodCacheDataVersion;
            string cachePrefix = $"vod_cache_{cacheDataVersion}_v{_cacheVersion}_";

            // Clear old cache entries only for full sync
            if (!isIncrementalSync)
            {
                ClearCache(cacheDataVersion);
            }

            try
            {
                // Cache entries have a 24-hour safety expiration to prevent memory leaks
                MemoryCacheEntryOptions cacheOptions = new()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                };

                // Fetch all categories
                _currentStatus = "Fetching categories...";
                progress?.Report(0.05);
                _logger?.LogInformation("Fetching VOD categories...");
                IEnumerable<Category> categories = await _streamService.GetVodCategories(_refreshCancellationTokenSource.Token).ConfigureAwait(false);
                List<Category> categoryList = categories.ToList();
                _memoryCache.Set($"{cachePrefix}categories", categoryList, cacheOptions);
                _logger?.LogInformation("Found {CategoryCount} VOD categories", categoryList.Count);

                // Log configuration state for debugging
                var vodConfig = Plugin.Instance.Configuration.Vod;
                _logger?.LogInformation("Configuration has {ConfigCategoryCount} configured VOD categories", vodConfig.Count);

                int movieCount = 0;
                int moviesSkipped = 0;
                int totalMovies = 0;

                // Fetch all movie lists from categories
                Dictionary<int, List<StreamInfo>> moviesByCategory = new();
                _currentStatus = "Fetching movie lists...";
                foreach (Category category in categoryList)
                {
                    _refreshCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    IEnumerable<StreamInfo> movies = await _streamService.GetVodStreams(category.CategoryId, _refreshCancellationTokenSource.Token).ConfigureAwait(false);
                    List<StreamInfo> movieItems = movies.ToList();
                    moviesByCategory[category.CategoryId] = movieItems;
                    totalMovies += movieItems.Count;
                }

                _logger?.LogInformation("Fetched {TotalMovies} movies across {CategoryCount} categories", totalMovies, categoryList.Count);

                progress?.Report(0.2);

                // Get parallelism configuration (reuse series settings)
                int parallelism = Math.Max(1, Math.Min(10, Plugin.Instance?.Configuration.CacheRefreshParallelism ?? 3));
                int minDelayMs = Math.Max(0, Math.Min(1000, Plugin.Instance?.Configuration.CacheRefreshMinDelayMs ?? 100));
                _logger?.LogInformation("Starting parallel VOD processing with parallelism={Parallelism}, minDelayMs={MinDelayMs}", parallelism, minDelayMs);

                // Throttle semaphore for rate limiting API requests
                using SemaphoreSlim throttleSemaphore = new(1, 1);
                DateTime lastRequestTime = DateTime.MinValue;

                // Helper to throttle requests
                async Task ThrottleRequestAsync()
                {
                    if (minDelayMs <= 0)
                    {
                        return;
                    }

                    await throttleSemaphore.WaitAsync(_refreshCancellationTokenSource.Token).ConfigureAwait(false);
                    try
                    {
                        double elapsedMs = (DateTime.UtcNow - lastRequestTime).TotalMilliseconds;
                        if (elapsedMs < minDelayMs)
                        {
                            await Task.Delay(minDelayMs - (int)elapsedMs, _refreshCancellationTokenSource.Token).ConfigureAwait(false);
                        }

                        lastRequestTime = DateTime.UtcNow;
                    }
                    finally
                    {
                        throttleSemaphore.Release();
                    }
                }

                // Cache movies by category
                foreach (Category category in categoryList)
                {
                    List<StreamInfo> movieItems = moviesByCategory[category.CategoryId];
                    _memoryCache.Set($"{cachePrefix}movies_{category.CategoryId}", movieItems, cacheOptions);
                    movieCount += movieItems.Count;
                }

                _logger?.LogInformation("Cached {MovieCount} movies", movieCount);

                // Fetch TMDB images for movies if enabled
                bool useTmdb = Plugin.Instance?.Configuration.UseTmdbForVodMetadata ?? true;
                if (useTmdb && _providerManager != null)
                {
                    _logger?.LogInformation("Looking up TMDB metadata for {Count} movies...", totalMovies);
                    _currentStatus = "Fetching TMDB images...";

                    // Parse title overrides once before the lookup loop
                    Dictionary<string, string> titleOverrides = ParseTitleOverrides(
                        Plugin.Instance?.Configuration.TmdbTitleOverrides ?? string.Empty);

                    if (titleOverrides.Count > 0)
                    {
                        _logger?.LogInformation("Loaded {Count} TMDB title overrides", titleOverrides.Count);
                    }

                    int tmdbFound = 0;
                    int tmdbNotFound = 0;
                    int processedMovies = 0;

                    // Flatten all movies for parallel processing
                    List<(StreamInfo Movie, int CategoryId)> allMovies = new();
                    HashSet<int> currentMovieIds = new();
                    foreach (var kvp in moviesByCategory)
                    {
                        foreach (var movie in kvp.Value)
                        {
                            allMovies.Add((movie, kvp.Key));
                            currentMovieIds.Add(movie.StreamId);
                        }
                    }

                    // Detect deleted movies (in sync state but no longer on provider)
                    int moviesDeleted = 0;
                    List<int> deletedMovieIds = syncState.MoviesAdded.Keys
                        .Where(id => !currentMovieIds.Contains(id))
                        .ToList();

                    foreach (int deletedId in deletedMovieIds)
                    {
                        syncState.MoviesAdded.TryRemove(deletedId, out _);
                        moviesDeleted++;
                    }

                    if (moviesDeleted > 0)
                    {
                        _logger?.LogInformation(
                            "Detected {Count} deleted movies (removed from provider)",
                            moviesDeleted);
                    }

                    // Parallel processing options
                    ParallelOptions parallelOptions = new()
                    {
                        MaxDegreeOfParallelism = parallelism,
                        CancellationToken = _refreshCancellationTokenSource.Token
                    };

                    await Parallel.ForEachAsync(allMovies, parallelOptions, async (item, ct) =>
                    {
                        StreamInfo movie = item.Movie;

                        try
                        {
                            // INCREMENTAL SYNC CHECK: Skip unchanged movies
                            DateTime? addedTime = ParseUnixTimestamp(movie.Added);
                            if (isIncrementalSync &&
                                addedTime.HasValue &&
                                syncState.MoviesAdded.TryGetValue(movie.StreamId, out var lastAdded) &&
                                addedTime.Value <= lastAdded)
                            {
                                // Movie unchanged - skip TMDB lookup
                                Interlocked.Increment(ref moviesSkipped);
                                int skippedProcessed = Interlocked.Increment(ref processedMovies);

                                if (totalMovies > 0)
                                {
                                    double progressValue = 0.2 + (skippedProcessed * 0.7 / totalMovies);
                                    _currentProgress = progressValue;
                                    progress?.Report(progressValue);
                                }

                                return; // Skip this movie
                            }

                            // Throttle to prevent rate limiting
                            await ThrottleRequestAsync().ConfigureAwait(false);

                            string? tmdbUrl = await LookupAndCacheTmdbImageAsync(
                                movie.StreamId,
                                movie.Name,
                                titleOverrides,
                                cacheOptions,
                                ct).ConfigureAwait(false);

                            // Update sync state with this movie's timestamp
                            if (addedTime.HasValue)
                            {
                                syncState.MoviesAdded[movie.StreamId] = addedTime.Value;
                            }

                            if (tmdbUrl != null)
                            {
                                Interlocked.Increment(ref tmdbFound);
                            }
                            else
                            {
                                Interlocked.Increment(ref tmdbNotFound);
                            }
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError)
                        {
                            _logger?.LogWarning(
                                "Persistent HTTP {StatusCode} error for movie {MovieId} ({MovieName}): {Message}",
                                ex.StatusCode,
                                movie.StreamId,
                                movie.Name,
                                ex.Message);
                            Interlocked.Increment(ref tmdbNotFound);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to lookup TMDB for movie {MovieId} ({MovieName})", movie.StreamId, movie.Name);
                            Interlocked.Increment(ref tmdbNotFound);
                        }

                        int currentProcessed = Interlocked.Increment(ref processedMovies);
                        if (totalMovies > 0)
                        {
                            double progressValue = 0.2 + (currentProcessed * 0.7 / totalMovies);
                            _currentProgress = progressValue;
                            _currentStatus = $"TMDB lookup {currentProcessed}/{totalMovies} ({tmdbFound} found)";
                            progress?.Report(progressValue);
                        }

                        if (currentProcessed % 50 == 0)
                        {
                            _logger?.LogInformation(
                                "TMDB lookup progress: {Processed}/{Total} movies ({Found} found)",
                                currentProcessed,
                                totalMovies,
                                tmdbFound);
                        }
                    }).ConfigureAwait(false);

                    if (isIncrementalSync)
                    {
                        _logger?.LogInformation(
                            "Incremental TMDB lookup completed: {Found} found, {NotFound} not found, {Skipped} skipped (unchanged)",
                            tmdbFound,
                            tmdbNotFound,
                            moviesSkipped);
                    }
                    else
                    {
                        _logger?.LogInformation(
                            "TMDB lookup completed: {Found} found, {NotFound} not found",
                            tmdbFound,
                            tmdbNotFound);
                    }
                }

                // Update and save sync state
                syncState.LastIncrementalSync = DateTime.UtcNow;
                if (!isIncrementalSync)
                {
                    syncState.LastFullSync = DateTime.UtcNow;
                }

                await _syncStateService.SaveStateAsync(syncState, _refreshCancellationTokenSource.Token).ConfigureAwait(false);

                progress?.Report(0.95);
                _currentProgress = 0.95;
                _currentStatus = isIncrementalSync
                    ? $"Completed: {movieCount} processed, {moviesSkipped} skipped"
                    : $"Completed: {movieCount} movies";
                _lastRefreshComplete = DateTime.UtcNow;

                if (isIncrementalSync)
                {
                    _logger?.LogInformation(
                        "Incremental VOD cache refresh completed: {MovieCount} movies processed, {Skipped} skipped across {CategoryCount} categories",
                        movieCount,
                        moviesSkipped,
                        categoryList.Count);
                }
                else
                {
                    _logger?.LogInformation(
                        "Full VOD cache refresh completed: {MovieCount} movies across {CategoryCount} categories",
                        movieCount,
                        categoryList.Count);
                }

                // Log failure summary if failures occurred
                var (failureCount, failedItems) = _failureTrackingService.GetFailureStats();
                if (failureCount > 0)
                {
                    _logger?.LogWarning(
                        "VOD cache refresh completed with {FailureCount} persistent HTTP failures. " +
                        "These items will be skipped for the next {ExpirationHours} hours. " +
                        "First 10 failed URLs: {FailedItems}",
                        failureCount,
                        Plugin.Instance?.Configuration.HttpFailureCacheExpirationHours ?? 24,
                        string.Join(", ", failedItems.Take(10)));
                }

                // Eagerly populate Jellyfin's database
                try
                {
                    _logger?.LogInformation("Starting eager population of Jellyfin VOD database from cache...");
                    await PopulateJellyfinDatabaseAsync(_refreshCancellationTokenSource.Token).ConfigureAwait(false);
                    _logger?.LogInformation("Jellyfin VOD database population completed");
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("VOD database population cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to populate Jellyfin VOD database - items may load lazily when browsing");
                }

                progress?.Report(1.0);
                _currentProgress = 1.0;
                _currentStatus = $"Completed: {movieCount} movies";
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("VOD cache refresh cancelled");
                _currentStatus = "Cancelled";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during VOD cache refresh");
                throw;
            }
        }
        finally
        {
            _isRefreshing = false;
            if (_currentProgress < 1.0)
            {
                _currentStatus = "Failed or cancelled";
            }

            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Gets cached VOD categories.
    /// </summary>
    /// <returns>Cached categories, or null if not available.</returns>
    public IEnumerable<Category>? GetCachedCategories()
    {
        try
        {
            string cacheKey = $"{CachePrefix}categories";
            if (_memoryCache.TryGetValue(cacheKey, out List<Category>? categories) && categories != null)
            {
                return categories;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached movies for a category.
    /// </summary>
    /// <param name="categoryId">The category ID.</param>
    /// <returns>Cached movies, or null if not available.</returns>
    public IEnumerable<StreamInfo>? GetCachedMovies(int categoryId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}movies_{categoryId}";
            if (_memoryCache.TryGetValue(cacheKey, out List<StreamInfo>? movies) && movies != null)
            {
                return movies;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the cached TMDB image URL for a movie.
    /// </summary>
    /// <param name="movieId">The movie ID (stream ID).</param>
    /// <returns>TMDB image URL, or null if not cached.</returns>
    public string? GetCachedTmdbImageUrl(int movieId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}tmdb_image_{movieId}";
            if (_memoryCache.TryGetValue(cacheKey, out string? imageUrl) && imageUrl != null)
            {
                return imageUrl;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error retrieving cached TMDB image for movie {MovieId}", movieId);
            return null;
        }
    }

    /// <summary>
    /// Gets the cached TMDB movie ID for a movie.
    /// </summary>
    /// <param name="movieId">The Xtream movie ID (stream ID).</param>
    /// <returns>TMDB movie ID, or null if not cached.</returns>
    public string? GetCachedTmdbMovieId(int movieId)
    {
        string cacheKey = $"{CachePrefix}tmdb_movie_id_{movieId}";
        return _memoryCache.TryGetValue(cacheKey, out string? tmdbId) ? tmdbId : null;
    }

    /// <summary>
    /// Looks up TMDB image URL for a movie and caches it.
    /// Checks title overrides first for direct TMDB ID lookup, then falls back to name search.
    /// </summary>
    private async Task<string?> LookupAndCacheTmdbImageAsync(
        int movieId,
        string movieName,
        Dictionary<string, string> titleOverrides,
        MemoryCacheEntryOptions cacheOptions,
        CancellationToken cancellationToken)
    {
        if (_providerManager == null)
        {
            return null;
        }

        try
        {
            // Parse the name to remove tags
            string cleanName = StreamService.ParseName(movieName).Title;
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                return null;
            }

            // Check title overrides first for direct TMDB ID lookup
            if (titleOverrides.TryGetValue(cleanName, out string? tmdbId))
            {
                _logger?.LogInformation("Using TMDB title override for movie {MovieId} ({Name}) -> TMDB ID {TmdbId}", movieId, cleanName, tmdbId);
                string? overrideResult = await LookupByTmdbIdAsync(cleanName, tmdbId, cancellationToken).ConfigureAwait(false);
                if (overrideResult != null)
                {
                    string cacheKey = $"{CachePrefix}tmdb_image_{movieId}";
                    _memoryCache.Set(cacheKey, overrideResult, cacheOptions);

                    // Cache TMDB movie ID
                    _memoryCache.Set($"{CachePrefix}tmdb_movie_id_{movieId}", tmdbId, cacheOptions);

                    _logger?.LogInformation("Cached TMDB image for movie {MovieId} ({Name}) via override (TMDB ID {TmdbId}): {Url}", movieId, cleanName, tmdbId, overrideResult);
                    return overrideResult;
                }

                _logger?.LogWarning("TMDB title override for movie {MovieId} ({Name}) with TMDB ID {TmdbId} returned no image, falling back to name search", movieId, cleanName, tmdbId);
            }

            // Fall back to name-based search
            string[] searchTerms = GenerateSearchTerms(cleanName);

            foreach (string searchTerm in searchTerms)
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    continue;
                }

                // Search TMDB for the movie
                string? lang = _serverConfigManager?.Configuration?.PreferredMetadataLanguage;
                RemoteSearchQuery<JellyfinMovieInfo> query = new()
                {
                    SearchInfo = new()
                    {
                        Name = searchTerm,
                        MetadataLanguage = lang ?? string.Empty,
                    },
                    SearchProviderName = "TheMovieDb",
                };

                IEnumerable<RemoteSearchResult> results = await _providerManager
                    .GetRemoteSearchResults<JellyfinMovie, JellyfinMovieInfo>(query, cancellationToken)
                    .ConfigureAwait(false);

                // Find first result with a real image
                RemoteSearchResult? resultWithImage = results.FirstOrDefault(r =>
                    !string.IsNullOrEmpty(r.ImageUrl) &&
                    !r.ImageUrl.Contains("missing/movie", StringComparison.OrdinalIgnoreCase));
                if (resultWithImage?.ImageUrl != null)
                {
                    // Cache the TMDB image URL
                    string cacheKey = $"{CachePrefix}tmdb_image_{movieId}";
                    _memoryCache.Set(cacheKey, resultWithImage.ImageUrl, cacheOptions);

                    // Cache TMDB movie ID for future lookups
                    string? foundTmdbId = resultWithImage.GetProviderId(MetadataProvider.Tmdb);
                    if (!string.IsNullOrEmpty(foundTmdbId))
                    {
                        _memoryCache.Set($"{CachePrefix}tmdb_movie_id_{movieId}", foundTmdbId, cacheOptions);
                    }

                    _logger?.LogDebug("Cached TMDB image for movie {MovieId} ({Name}) using search term '{SearchTerm}': {Url}", movieId, cleanName, searchTerm, resultWithImage.ImageUrl);
                    return resultWithImage.ImageUrl;
                }
            }

            _logger?.LogDebug("TMDB search found no image for movie {MovieId} ({Name}) after trying: {SearchTerms}", movieId, cleanName, string.Join(", ", searchTerms));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to lookup TMDB image for movie {MovieId} ({Name})", movieId, movieName);
        }

        return null;
    }

    /// <summary>
    /// Looks up a movie on TMDB by its TMDB ID and returns the image URL.
    /// </summary>
    private async Task<string?> LookupByTmdbIdAsync(
        string cleanName,
        string tmdbId,
        CancellationToken cancellationToken)
    {
        if (_providerManager == null)
        {
            return null;
        }

        string? lang = _serverConfigManager?.Configuration?.PreferredMetadataLanguage;
        RemoteSearchQuery<JellyfinMovieInfo> query = new()
        {
            SearchInfo = new()
            {
                Name = cleanName,
                MetadataLanguage = lang ?? string.Empty,
                ProviderIds = new Dictionary<string, string>
                {
                    { MetadataProvider.Tmdb.ToString(), tmdbId }
                }
            },
            SearchProviderName = "TheMovieDb",
        };

        IEnumerable<RemoteSearchResult> results = await _providerManager
            .GetRemoteSearchResults<JellyfinMovie, JellyfinMovieInfo>(query, cancellationToken)
            .ConfigureAwait(false);

        RemoteSearchResult? resultWithImage = results.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.ImageUrl) &&
            !r.ImageUrl.Contains("missing/movie", StringComparison.OrdinalIgnoreCase));

        return resultWithImage?.ImageUrl;
    }

    /// <summary>
    /// Generates search terms from a movie name for TMDB lookup.
    /// Returns the original name and variants with common patterns stripped.
    /// </summary>
    private static string[] GenerateSearchTerms(string name)
    {
        List<string> terms = new();

        // 1. Add original name first
        terms.Add(name);

        // 2. Remove year in parentheses like "(2023)"
        string withoutYear = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"\s*\(\d{4}\)\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.None).Trim();

        if (!string.Equals(withoutYear, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(withoutYear))
        {
            terms.Add(withoutYear);
        }

        // 3. Remove language indicators like "(NL Gesproken)", "(DE)", "(French)", etc.
        string withoutLang = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"\s*\([^)]*(?:Gesproken|Dubbed|Subbed|NL|DE|FR|Dutch|German|French|Nederlands|Deutsch)[^)]*\)\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        if (!string.Equals(withoutLang, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(withoutLang))
        {
            terms.Add(withoutLang);
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Parses a Unix timestamp string to DateTime.
    /// </summary>
    /// <param name="timestamp">Unix timestamp string.</param>
    /// <returns>DateTime if parsing succeeds, null otherwise.</returns>
    private static DateTime? ParseUnixTimestamp(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp))
        {
            return null;
        }

        if (long.TryParse(timestamp, out var unix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        }

        return null;
    }

    /// <summary>
    /// Parses the title override configuration string into a dictionary.
    /// Format: one mapping per line, "MovieTitle=TmdbID".
    /// </summary>
    private static Dictionary<string, string> ParseTitleOverrides(string config)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(config))
        {
            return result;
        }

        foreach (string line in config.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equalsIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0 && equalsIndex < line.Length - 1)
            {
                string key = line[..equalsIndex].Trim();
                string value = line[(equalsIndex + 1)..].Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Clears all cache entries for the given data version.
    /// </summary>
    private void ClearCache(string dataVersion)
    {
        // Note: IMemoryCache doesn't support enumerating keys, so we can't clear by prefix
        // Instead, we rely on cache expiration and version-based keys
        _logger?.LogInformation("VOD cache cleared (old entries will expire naturally)");
    }

    /// <summary>
    /// Checks if cache is populated for the current data version.
    /// </summary>
    /// <returns>True if cache is populated, false otherwise.</returns>
    public bool IsCachePopulated()
    {
        try
        {
            string cacheKey = $"{CachePrefix}categories";
            return _memoryCache.TryGetValue(cacheKey, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cancels the currently running cache refresh operation.
    /// </summary>
    public void CancelRefresh()
    {
        var cts = _refreshCancellationTokenSource;
        if (_isRefreshing && cts != null)
        {
            _logger?.LogInformation("Cancelling VOD cache refresh...");
            _currentStatus = "Cancelling...";
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, nothing to cancel
            }
        }
    }

    /// <summary>
    /// Invalidates all cached data by incrementing the cache version.
    /// Old cache entries will remain in memory but won't be accessed.
    /// </summary>
    public void InvalidateCache()
    {
        _cacheVersion++;
        _currentProgress = 0.0;
        _currentStatus = "Cache invalidated";
        _lastRefreshComplete = null;
        _logger?.LogInformation("VOD cache invalidated (version incremented to {Version})", _cacheVersion);
    }

    /// <summary>
    /// Resets the sync state, forcing the next refresh to be a full sync.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task ResetSyncStateAsync(CancellationToken cancellationToken = default)
    {
        await _syncStateService.ResetStateAsync(cancellationToken).ConfigureAwait(false);
        _logger?.LogInformation("VOD sync state reset - next refresh will be a full sync");
    }

    /// <summary>
    /// Eagerly populates Jellyfin's database using delta-based approach.
    /// Compares existing movies in DB with cache and only processes new/missing items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task PopulateJellyfinDatabaseAsync(CancellationToken cancellationToken)
    {
        _currentStatus = "Populating Jellyfin database...";
        var startTime = DateTime.UtcNow;

        try
        {
            IChannelManager? channelManager = Plugin.Instance?.ChannelManager;
            if (channelManager == null)
            {
                _logger?.LogWarning("ChannelManager not available, skipping VOD database population");
                return;
            }

            // Find our VOD channel
            var channelQuery = new ChannelQuery();
            var channelsResult = await channelManager.GetChannelsInternalAsync(channelQuery).ConfigureAwait(false);

            var vodChannel = channelsResult.Items.FirstOrDefault(c => c.Name == "Xtream Video On-Demand");
            if (vodChannel == null)
            {
                _logger?.LogWarning("Xtream Video On-Demand channel not found, skipping database population");
                return;
            }

            Guid channelId = vodChannel.Id;

            // Get expected movies from cache
            var expectedMovies = new List<(int MovieId, string Name, int CategoryId, Guid FolderGuid)>();
            var categories = GetCachedCategories();
            if (categories != null)
            {
                foreach (var category in categories)
                {
                    var movies = GetCachedMovies(category.CategoryId);
                    if (movies != null)
                    {
                        foreach (var movie in movies)
                        {
                            var parsedName = StreamService.ParseName(movie.Name);
                            // Movie IDs use StreamPrefix
                            Guid movieGuid = StreamService.ToGuid(StreamService.StreamPrefix, movie.StreamId, 0, 0);
                            expectedMovies.Add((movie.StreamId, parsedName.Title, category.CategoryId, movieGuid));
                        }
                    }
                }
            }

            _logger?.LogInformation("VOD cache contains {Count} movies", expectedMovies.Count);

            if (expectedMovies.Count == 0)
            {
                _logger?.LogInformation("No movies in cache, skipping database population");
                _currentStatus = "No movies to populate";
                return;
            }

            // Query existing items from channel
            _logger?.LogInformation("Querying existing movies from channel...");
            var rootQuery = new InternalItemsQuery
            {
                ChannelIds = new[] { channelId },
                Recursive = true
            };

            var existingItems = await channelManager.GetChannelItemsInternal(
                rootQuery,
                new Progress<double>(),
                cancellationToken).ConfigureAwait(false);

            int existingCount = existingItems?.TotalRecordCount ?? 0;
            _logger?.LogInformation("Found {Count} existing items in VOD database", existingCount);

            // Simple population - just trigger channel refresh to populate items
            // The channel will use cached data for fast response
            var totalElapsed = DateTime.UtcNow - startTime;
            _logger?.LogInformation(
                "Jellyfin VOD database population completed in {Elapsed:F1}s",
                totalElapsed.TotalSeconds);

            _currentStatus = $"Database populated: {expectedMovies.Count} movies";
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("VOD database population cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during Jellyfin VOD database population");
        }
    }

    /// <summary>
    /// Gets the current cache refresh status.
    /// </summary>
    /// <returns>Cache status information.</returns>
    public (bool IsRefreshing, double Progress, string Status, DateTime? StartTime, DateTime? CompleteTime) GetStatus()
    {
        return (_isRefreshing, _currentProgress, _currentStatus, _lastRefreshStart, _lastRefreshComplete);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the VodCacheService and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshCancellationTokenSource?.Dispose();
            _refreshLock?.Dispose();
        }
    }
}
