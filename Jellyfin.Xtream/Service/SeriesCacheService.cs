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
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for pre-fetching and caching all series data upfront.
/// </summary>
public class SeriesCacheService : IDisposable
{
    private readonly StreamService _streamService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SeriesCacheService>? _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private int _cacheVersion = 0;
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
    /// Gets the current cache key prefix.
    /// Uses CacheDataVersion which only changes when cache-relevant settings change
    /// (not when refresh frequency changes).
    /// </summary>
    private string CachePrefix => $"series_cache_{Plugin.Instance.CacheDataVersion}_v{_cacheVersion}_";

    /// <summary>
    /// Pre-fetches and caches all series data (categories, series, seasons, episodes).
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
            _logger?.LogInformation("Starting series data cache refresh");

            string cacheDataVersion = Plugin.Instance.CacheDataVersion;
            string cachePrefix = $"series_cache_{cacheDataVersion}_v{_cacheVersion}_";

            // Clear old cache entries
            ClearCache(cacheDataVersion);

            try
            {
                // Cache entries have a 24-hour safety expiration to prevent memory leaks
                // from orphaned entries (e.g., when cache version changes).
                // Normal refresh frequency is controlled by the scheduled task (default: every 60 minutes)
                MemoryCacheEntryOptions cacheOptions = new()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                };

                // Fetch all categories
                _currentStatus = "Fetching categories...";
                progress?.Report(0.05);
                _logger?.LogInformation("Fetching series categories...");
                IEnumerable<Category> categories = await _streamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false);
                List<Category> categoryList = categories.ToList();
                _memoryCache.Set($"{cachePrefix}categories", categoryList, cacheOptions);
                _logger?.LogInformation("Found {CategoryCount} categories", categoryList.Count);

                int seriesCount = 0;
                int seasonCount = 0;
                int episodeCount = 0;
                int categoryIndex = 0;
                int totalCategories = categoryList.Count;
                int totalSeries = 0; // Will be calculated after fetching all series lists

                // First pass: count total series for progress calculation
                foreach (Category category in categoryList)
                {
                    IEnumerable<Series> seriesList = await _streamService.GetSeries(category.CategoryId, cancellationToken).ConfigureAwait(false);
                    totalSeries += seriesList.Count();
                }

                // Second pass: fetch and cache all data
                int processedSeries = 0;
                foreach (Category category in categoryList)
                {
                    categoryIndex++;
                    _logger?.LogInformation("Processing category {CategoryIndex}/{TotalCategories}: {CategoryName} (ID: {CategoryId})", categoryIndex, totalCategories, category.CategoryName, category.CategoryId);
                    progress?.Report(0.1 + (((categoryIndex - 1) * 0.8) / totalCategories)); // 10% for categories, 80% for series processing

                    IEnumerable<Series> seriesList = await _streamService.GetSeries(category.CategoryId, cancellationToken).ConfigureAwait(false);
                    List<Series> seriesListItems = seriesList.ToList();
                    _logger?.LogInformation("  Found {SeriesCount} series in category {CategoryName}", seriesListItems.Count, category.CategoryName);

                    int seriesInCategory = 0;
                    foreach (Series series in seriesListItems)
                    {
                        seriesCount++;
                        seriesInCategory++;
                        processedSeries++;

                        // Report progress and log every 10 series or at the start
                        if (totalSeries > 0)
                        {
                            double progressValue = 0.1 + (processedSeries * 0.8 / totalSeries); // 10% for categories, 80% for series
                            _currentProgress = progressValue;
                            _currentStatus = $"Processing series {processedSeries}/{totalSeries} ({seriesCount} series, {seasonCount} seasons, {episodeCount} episodes)";
                            progress?.Report(progressValue);
                        }

                        if (seriesInCategory == 1 || seriesInCategory % 10 == 0)
                        {
                            _logger?.LogInformation("  Processing series {SeriesInCategory}/{TotalInCategory}: {SeriesName} (ID: {SeriesId}) - Total progress: {TotalSeries} series, {TotalSeasons} seasons, {TotalEpisodes} episodes", seriesInCategory, seriesListItems.Count, series.Name, series.SeriesId, seriesCount, seasonCount, episodeCount);
                        }

                        try
                        {
                            // Fetch seasons for this series
                            IEnumerable<Tuple<SeriesStreamInfo, int>> seasons = await _streamService.GetSeasons(series.SeriesId, cancellationToken).ConfigureAwait(false);
                            List<Tuple<SeriesStreamInfo, int>> seasonList = seasons.ToList();

                            SeriesStreamInfo? seriesStreamInfo = null;
                            foreach (var seasonTuple in seasonList)
                            {
                                if (seriesStreamInfo == null)
                                {
                                    seriesStreamInfo = seasonTuple.Item1;
                                }

                                int seasonId = seasonTuple.Item2;
                                seasonCount++;

                                // Fetch episodes for this season
                                IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>> episodes = await _streamService.GetEpisodes(series.SeriesId, seasonId, cancellationToken).ConfigureAwait(false);

                                List<Episode> episodeList = episodes.Select(e => e.Item3).ToList();
                                episodeCount += episodeList.Count;

                                // Cache episodes for this season
                                _memoryCache.Set($"{cachePrefix}episodes_{series.SeriesId}_{seasonId}", episodeList, cacheOptions);

                                // Cache season info
                                Season? season = seriesStreamInfo.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
                                _memoryCache.Set($"{cachePrefix}season_{series.SeriesId}_{seasonId}", season, cacheOptions);
                            }

                            // Cache series stream info
                            if (seriesStreamInfo != null)
                            {
                                _memoryCache.Set($"{cachePrefix}seriesinfo_{series.SeriesId}", seriesStreamInfo, cacheOptions);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to cache data for series {SeriesId} ({SeriesName})", series.SeriesId, series.Name);
                        }
                    }

                    _logger?.LogInformation("Completed category {CategoryName}: {SeriesInCategory} series, running totals: {TotalSeries} series, {TotalSeasons} seasons, {TotalEpisodes} episodes", category.CategoryName, seriesInCategory, seriesCount, seasonCount, episodeCount);
                }

                progress?.Report(1.0); // 100% complete
                _currentProgress = 1.0;
                _currentStatus = $"Completed: {seriesCount} series, {seasonCount} seasons, {episodeCount} episodes";
                _lastRefreshComplete = DateTime.UtcNow;
                _logger?.LogInformation("Cache refresh completed: {SeriesCount} series, {SeasonCount} seasons, {EpisodeCount} episodes across {CategoryCount} categories", seriesCount, seasonCount, episodeCount, totalCategories);
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
    /// Gets cached categories.
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
    /// Gets cached series stream info.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <returns>Cached series stream info, or null if not available.</returns>
    public SeriesStreamInfo? GetCachedSeriesInfo(int seriesId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}seriesinfo_{seriesId}";
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
            string cacheKey = $"{CachePrefix}season_{seriesId}_{seasonId}";
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
            string cacheKey = $"{CachePrefix}episodes_{seriesId}_{seasonId}";
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
    /// Clears all cache entries for the given data version.
    /// </summary>
    private void ClearCache(string dataVersion)
    {
        // Note: IMemoryCache doesn't support enumerating keys, so we can't clear by prefix
        // Instead, we rely on cache expiration and version-based keys
        // When data version changes, old keys won't be accessed anymore
        _logger?.LogInformation("Cache cleared (old entries will expire naturally)");
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
    /// Invalidates all cached data by incrementing the cache version.
    /// Old cache entries will remain in memory but won't be accessed.
    /// </summary>
    public void InvalidateCache()
    {
        _cacheVersion++;
        _currentProgress = 0.0;
        _currentStatus = "Cache invalidated";
        _lastRefreshComplete = null;
        _logger?.LogInformation("Cache invalidated (version incremented to {Version})", _cacheVersion);
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
    /// Releases the unmanaged resources used by the SeriesCacheService and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshLock?.Dispose();
        }
    }
}
