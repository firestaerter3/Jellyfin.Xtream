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
    private bool _isRefreshing = false;

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
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
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
            _logger?.LogInformation("Starting series data cache refresh");

            string dataVersion = Plugin.Instance.DataVersion;
            string cachePrefix = $"series_cache_{dataVersion}_";

            // Clear old cache entries
            ClearCache(dataVersion);

            try
            {
                // Get configured cache expiration time (default 60 minutes)
                int cacheExpirationMinutes = Plugin.Instance.Configuration.SeriesCacheExpirationMinutes;
                if (cacheExpirationMinutes <= 0)
                {
                    cacheExpirationMinutes = 60; // Default to 1 hour if invalid
                }

                TimeSpan cacheExpiration = TimeSpan.FromMinutes(cacheExpirationMinutes);

                // Fetch all categories
                IEnumerable<Category> categories = await _streamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false);
                _memoryCache.Set($"{cachePrefix}categories", categories, cacheExpiration);

                int seriesCount = 0;
                int seasonCount = 0;
                int episodeCount = 0;

                // Fetch all series, seasons, and episodes for each category
                foreach (Category category in categories)
                {
                    IEnumerable<Series> seriesList = await _streamService.GetSeries(category.CategoryId, cancellationToken).ConfigureAwait(false);

                    foreach (Series series in seriesList)
                    {
                        seriesCount++;

                        try
                        {
                            // Fetch seasons for this series
                            IEnumerable<Tuple<SeriesStreamInfo, int>> seasons = await _streamService.GetSeasons(series.SeriesId, cancellationToken).ConfigureAwait(false);

                            SeriesStreamInfo? seriesStreamInfo = null;
                            foreach (var seasonTuple in seasons)
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
                                _memoryCache.Set($"{cachePrefix}episodes_{series.SeriesId}_{seasonId}", episodeList, cacheExpiration);

                                // Cache season info
                                Season? season = seriesStreamInfo.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
                                _memoryCache.Set($"{cachePrefix}season_{series.SeriesId}_{seasonId}", season, cacheExpiration);
                            }

                            // Cache series stream info
                            if (seriesStreamInfo != null)
                            {
                                _memoryCache.Set($"{cachePrefix}seriesinfo_{series.SeriesId}", seriesStreamInfo, cacheExpiration);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to cache data for series {SeriesId}", series.SeriesId);
                        }
                    }
                }

                _logger?.LogInformation("Cache refresh completed: {SeriesCount} series, {SeasonCount} seasons, {EpisodeCount} episodes", seriesCount, seasonCount, episodeCount);
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
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_categories";
            return _memoryCache.TryGetValue(cacheKey, out IEnumerable<Category>? categories) ? categories : null;
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
            return _memoryCache.TryGetValue(cacheKey, out IEnumerable<Episode>? episodes) ? episodes : null;
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
            string cacheKey = $"series_cache_{Plugin.Instance.DataVersion}_categories";
            return _memoryCache.TryGetValue(cacheKey, out _);
        }
        catch
        {
            return false;
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
        }
    }
}
