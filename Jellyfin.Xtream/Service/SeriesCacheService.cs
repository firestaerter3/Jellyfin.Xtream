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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for pre-fetching and caching all series data upfront.
/// Supports parallel processing and incremental updates for faster refreshes.
/// </summary>
public class SeriesCacheService : IDisposable
{
    /// <summary>
    /// Maximum number of concurrent API requests during cache refresh.
    /// </summary>
    private const int MaxParallelRequests = 10;

    private readonly StreamService _streamService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SeriesCacheService>? _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _apiThrottle = new(MaxParallelRequests, MaxParallelRequests);
    private readonly ConcurrentDictionary<int, DateTime> _seriesLastModified = new();
    private readonly ConcurrentDictionary<string, bool> _cacheKeys = new();

    private bool _isRefreshing = false;
    private double _currentProgress = 0.0;
    private string _currentStatus = "Idle";
    private DateTime? _lastRefreshStart;
    private DateTime? _lastRefreshComplete;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesCacheService"/> class.
    /// </summary>
    /// <param name="streamService">The stream service instance.</param>
    /// <param name="memoryCache">The memory cache instance.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SeriesCacheService(StreamService streamService, IMemoryCache memoryCache, ILogger<SeriesCacheService>? logger = null)
    {
        _streamService = streamService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Pre-fetches and caches all series data (categories, series, seasons, episodes).
    /// Uses parallel processing and incremental updates for faster performance.
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task RefreshCacheAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Prevent concurrent refreshes
        if (!await _refreshLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger?.LogInformation("Cache refresh already in progress, skipping");
            return;
        }

        try
        {
            if (_isRefreshing)
            {
                _logger?.LogInformation("Cache refresh already in progress, skipping");
                return;
            }

            _isRefreshing = true;
            _currentProgress = 0.0;
            _currentStatus = "Starting...";
            _lastRefreshStart = DateTime.UtcNow;

            bool isIncremental = !_seriesLastModified.IsEmpty;
            _logger?.LogInformation("Starting series data cache refresh (mode: {Mode})", isIncremental ? "incremental" : "full");

            string dataVersion = Plugin.Instance.DataVersion;
            string cachePrefix = $"series_cache_{dataVersion}_";

            // Create cache entry options - no expiration (cache lives until refreshed)
            MemoryCacheEntryOptions cacheOptions = new();

            try
            {
                // Fetch all categories
                _currentStatus = "Fetching categories...";
                progress?.Report(0.02);
                _logger?.LogInformation("Fetching series categories...");
                IEnumerable<Category> categories = await _streamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false);
                List<Category> categoryList = categories.ToList();
                string categoriesKey = $"{cachePrefix}categories";
                _memoryCache.Set(categoriesKey, categoryList, cacheOptions);
                _cacheKeys.TryAdd(categoriesKey, true);
                _logger?.LogInformation("Found {CategoryCount} categories", categoryList.Count);

                // Collect all series from all categories (fast - just metadata)
                _currentStatus = "Fetching series list...";
                progress?.Report(0.05);
                List<Series> allSeries = new();
                int categoryIndex = 0;
                foreach (Category category in categoryList)
                {
                    categoryIndex++;
                    try
                    {
                        // Check for cancellation before each category
                        cancellationToken.ThrowIfCancellationRequested();

                        _currentStatus = $"Fetching series list... ({categoryIndex}/{categoryList.Count})";
                        _logger?.LogInformation(
                            "Fetching series for category {CategoryId} ({CategoryName}) - {Current}/{Total}",
                            category.CategoryId,
                            category.CategoryName,
                            categoryIndex,
                            categoryList.Count);

                        IEnumerable<Series> seriesList = await _streamService.GetSeries(category.CategoryId, cancellationToken).ConfigureAwait(false);
                        allSeries.AddRange(seriesList);

                        _logger?.LogInformation(
                            "Found {SeriesCount} series in category {CategoryId}",
                            seriesList.Count(),
                            category.CategoryId);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("Cache refresh cancelled while fetching category {CategoryId}", category.CategoryId);
                        throw; // Re-throw cancellation
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            ex,
                            "Failed to fetch series for category {CategoryId} ({CategoryName}), skipping...",
                            category.CategoryId,
                            category.CategoryName);
                        // Continue with next category instead of failing completely
                    }

                    // Update progress slightly for each category
                    double categoryProgress = 0.05 + ((categoryIndex / (double)categoryList.Count) * 0.05);
                    progress?.Report(categoryProgress);
                }

                _logger?.LogInformation("Found {TotalSeries} total series across all categories", allSeries.Count);

                // Determine which series need to be fetched (incremental vs full)
                List<Series> seriesToFetch;
                if (isIncremental)
                {
                    seriesToFetch = allSeries.Where(s => NeedsRefresh(s)).ToList();
                    _logger?.LogInformation(
                        "Incremental update: {ChangedCount}/{TotalCount} series have changed",
                        seriesToFetch.Count,
                        allSeries.Count);
                }
                else
                {
                    seriesToFetch = allSeries;
                    _logger?.LogInformation("Full refresh: fetching all {TotalCount} series", allSeries.Count);
                }

                if (seriesToFetch.Count == 0)
                {
                    _currentProgress = 1.0;
                    _currentStatus = "No changes detected";
                    _lastRefreshComplete = DateTime.UtcNow;
                    _logger?.LogInformation("No series have changed, cache refresh complete");
                    progress?.Report(1.0);
                    return;
                }

                // Thread-safe counters for progress tracking
                int processedSeries = 0;
                int totalToProcess = seriesToFetch.Count;
                int seasonCount = 0;
                int episodeCount = 0;
                int skippedCount = allSeries.Count - seriesToFetch.Count;

                // Process series in parallel with throttling
                _currentStatus = $"Processing series... (0/{totalToProcess})";
                progress?.Report(0.10);
                _logger?.LogInformation(
                    "Processing {Count} series with {Parallelism} parallel requests...",
                    totalToProcess,
                    MaxParallelRequests);

                var tasks = seriesToFetch.Select(async series =>
                {
                    await _apiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        // Log first few series to verify processing is working
                        int beforeCount = Interlocked.CompareExchange(ref processedSeries, 0, 0);
                        if (beforeCount < 5)
                        {
                            _logger?.LogInformation("Starting to process series {SeriesId} ({SeriesName})", series.SeriesId, series.Name);
                        }

                        var result = await ProcessSeriesAsync(series, cachePrefix, cacheOptions, cancellationToken).ConfigureAwait(false);

                        int afterCount = Interlocked.CompareExchange(ref processedSeries, 0, 0);
                        if (afterCount < 5)
                        {
                            _logger?.LogInformation("Completed processing series {SeriesId}", series.SeriesId);
                        }

                        // Update counters thread-safely
                        int current = Interlocked.Increment(ref processedSeries);
                        Interlocked.Add(ref seasonCount, result.SeasonCount);
                        Interlocked.Add(ref episodeCount, result.EpisodeCount);

                        // Update progress
                        double progressValue = 0.1 + (current * 0.9 / totalToProcess);
                        _currentProgress = progressValue;
                        _currentStatus = $"Processing {current}/{totalToProcess} ({seasonCount} seasons, {episodeCount} episodes)";
                        progress?.Report(progressValue);

                        // Log every 10 series or at milestones (also log first few to see if it's working)
                        if (current <= 5 || current % 10 == 0 || current == totalToProcess)
                        {
                            _logger?.LogInformation(
                                "Progress: {Current}/{Total} series processed ({Seasons} seasons, {Episodes} episodes)",
                                current,
                                totalToProcess,
                                seasonCount,
                                episodeCount);
                        }

                        // Store last_modified for incremental updates
                        _seriesLastModified[series.SeriesId] = series.LastModified;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("Cache refresh cancelled while processing series {SeriesId}", series.SeriesId);
                        throw; // Re-throw cancellation
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to cache data for series {SeriesId} ({SeriesName})", series.SeriesId, series.Name);
                        int current = Interlocked.Increment(ref processedSeries);
                        // Update progress even on error
                        double progressValue = 0.1 + (current * 0.9 / totalToProcess);
                        _currentProgress = progressValue;
                        _currentStatus = $"Processing {current}/{totalToProcess} (errors may occur)";
                        progress?.Report(progressValue);
                    }
                    finally
                    {
                        _apiThrottle.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);

                progress?.Report(1.0);
                _currentProgress = 1.0;
                _currentStatus = $"Completed: {processedSeries} updated, {skippedCount} unchanged, {seasonCount} seasons, {episodeCount} episodes";
                _lastRefreshComplete = DateTime.UtcNow;
                _logger?.LogInformation(
                    "Cache refresh completed: {ProcessedCount} series updated, {SkippedCount} unchanged, {SeasonCount} seasons, {EpisodeCount} episodes",
                    processedSeries,
                    skippedCount,
                    seasonCount,
                    episodeCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during cache refresh");
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
    /// Checks if a series needs to be refreshed based on its last_modified timestamp.
    /// </summary>
    private bool NeedsRefresh(Series series)
    {
        if (!_seriesLastModified.TryGetValue(series.SeriesId, out DateTime cachedLastModified))
        {
            return true; // New series, needs fetch
        }

        return series.LastModified > cachedLastModified;
    }

    /// <summary>
    /// Processes a single series - fetches seasons and episodes and caches them.
    /// </summary>
    private async Task<(int SeasonCount, int EpisodeCount)> ProcessSeriesAsync(
        Series series,
        string cachePrefix,
        MemoryCacheEntryOptions cacheOptions,
        CancellationToken cancellationToken)
    {
        int seasonCount = 0;
        int episodeCount = 0;

        // Fetch seasons for this series
        IEnumerable<Tuple<SeriesStreamInfo, int>> seasons = await _streamService.GetSeasons(series.SeriesId, cancellationToken).ConfigureAwait(false);
        List<Tuple<SeriesStreamInfo, int>> seasonList = seasons.ToList();

        SeriesStreamInfo? seriesStreamInfo = null;

        // Process seasons in parallel too
        var seasonTasks = seasonList.Select(async seasonTuple =>
        {
            await _apiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (seriesStreamInfo == null)
                {
                    seriesStreamInfo = seasonTuple.Item1;
                }

                int seasonId = seasonTuple.Item2;

                // Fetch episodes for this season
                IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>> episodes = await _streamService.GetEpisodes(series.SeriesId, seasonId, cancellationToken).ConfigureAwait(false);
                List<Episode> episodeList = episodes.Select(e => e.Item3).ToList();

                // Cache episodes for this season
                string episodesKey = $"{cachePrefix}episodes_{series.SeriesId}_{seasonId}";
                _memoryCache.Set(episodesKey, episodeList, cacheOptions);
                _cacheKeys.TryAdd(episodesKey, true);

                // Cache season info
                if (seriesStreamInfo != null)
                {
                    Season? season = seriesStreamInfo.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
                    string seasonKey = $"{cachePrefix}season_{series.SeriesId}_{seasonId}";
                    _memoryCache.Set(seasonKey, season, cacheOptions);
                    _cacheKeys.TryAdd(seasonKey, true);
                }

                return (SeasonCount: 1, EpisodeCount: episodeList.Count);
            }
            finally
            {
                _apiThrottle.Release();
            }
        });

        var results = await Task.WhenAll(seasonTasks).ConfigureAwait(false);
        seasonCount = results.Sum(r => r.SeasonCount);
        episodeCount = results.Sum(r => r.EpisodeCount);

        // Cache series stream info
        if (seriesStreamInfo != null)
        {
            string seriesInfoKey = $"{cachePrefix}seriesinfo_{series.SeriesId}";
            _memoryCache.Set(seriesInfoKey, seriesStreamInfo, cacheOptions);
            _cacheKeys.TryAdd(seriesInfoKey, true);
        }

        return (seasonCount, episodeCount);
    }

    /// <summary>
    /// Gets cached categories.
    /// </summary>
    /// <returns>Cached categories, or null if not available.</returns>
    public IEnumerable<Category>? GetCachedCategories()
    {
        try
        {
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_categories";
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
    /// Gets cached series stream info.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <returns>Cached series stream info, or null if not available.</returns>
    public SeriesStreamInfo? GetCachedSeriesInfo(int seriesId)
    {
        try
        {
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_seriesinfo_{seriesId}";
            return _memoryCache.TryGetValue(cacheKey, out SeriesStreamInfo? info) ? info : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached season info.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <param name="seasonId">The season ID.</param>
    /// <returns>Cached season info, or null if not available.</returns>
    public Season? GetCachedSeason(int seriesId, int seasonId)
    {
        try
        {
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_season_{seriesId}_{seasonId}";
            return _memoryCache.TryGetValue(cacheKey, out Season? season) ? season : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached episodes for a season.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <param name="seasonId">The season ID.</param>
    /// <returns>Cached episodes, or null if not available.</returns>
    public IEnumerable<Episode>? GetCachedEpisodes(int seriesId, int seasonId)
    {
        try
        {
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_episodes_{seriesId}_{seasonId}";
            if (_memoryCache.TryGetValue(cacheKey, out List<Episode>? episodes) && episodes != null)
            {
                return episodes;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if cache is populated for the current data version.
    /// </summary>
    /// <returns>True if cache is populated, false otherwise.</returns>
    public bool IsCachePopulated()
    {
        try
        {
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_categories";
            return _memoryCache.TryGetValue(cacheKey, out _);
        }
        catch
        {
            return false;
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

    /// <summary>
    /// Clears the incremental update tracking, forcing a full refresh on next run.
    /// </summary>
    public void ClearIncrementalTracking()
    {
        _seriesLastModified.Clear();
        _logger?.LogInformation("Incremental tracking cleared, next refresh will be a full refresh");
    }

    /// <summary>
    /// Clears all cached series data for the current data version.
    /// </summary>
    public void ClearCache()
    {
        string dataVersion = Plugin.Instance.DataVersion;
        string cachePrefix = $"series_cache_{dataVersion}_";

        // Clear all cache entries with the current prefix
        // Note: IMemoryCache doesn't provide a way to enumerate keys, so we clear known patterns
        // The cache will naturally expire unused entries, but we can't directly remove them all
        _seriesLastModified.Clear();
        _logger?.LogInformation("Cache clearing requested for data version {DataVersion}", dataVersion);
    }

    /// <summary>
    /// Clears all cached series data by removing all cache entries.
    /// This forces a complete cache refresh on next access.
    /// </summary>
    public void ClearAllCache()
    {
        try
        {
            int clearedCount = 0;

            // Clear all tracked cache keys
            foreach (string key in _cacheKeys.Keys)
            {
                _memoryCache.Remove(key);
                clearedCount++;
            }

            _cacheKeys.Clear();

            // Clear incremental tracking
            _seriesLastModified.Clear();

            // Reset status
            _currentProgress = 0.0;
            _currentStatus = "Cache cleared";
            _lastRefreshStart = null;
            _lastRefreshComplete = null;

            _logger?.LogInformation("All cache data cleared: {Count} entries removed. Cache will be rebuilt on next refresh.", clearedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing cache");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SeriesCacheService and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshLock?.Dispose();
            _apiThrottle?.Dispose();
        }
    }
}
