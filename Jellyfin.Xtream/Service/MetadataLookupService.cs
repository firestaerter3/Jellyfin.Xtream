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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for looking up TMDb/TVDb IDs using Jellyfin's provider infrastructure.
/// Results are cached persistently to avoid repeated API calls.
/// </summary>
public class MetadataLookupService
{
    private const string CacheFileName = "metadata-cache.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IProviderManager _providerManager;
    private readonly IServerConfigurationManager _serverConfigManager;
    private readonly ILogger<MetadataLookupService> _logger;
    private readonly string _cachePath;

    // In-memory cache with persistence
    private readonly ConcurrentDictionary<string, MetadataCacheEntry> _movieCache = new();
    private readonly ConcurrentDictionary<string, MetadataCacheEntry> _seriesCache = new();
    private bool _cacheLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataLookupService"/> class.
    /// </summary>
    /// <param name="providerManager">Jellyfin's provider manager for metadata lookups.</param>
    /// <param name="applicationPaths">Application paths for determining cache location.</param>
    /// <param name="serverConfigManager">Server configuration for metadata language settings.</param>
    /// <param name="logger">Logger instance.</param>
    public MetadataLookupService(
        IProviderManager providerManager,
        IApplicationPaths applicationPaths,
        IServerConfigurationManager serverConfigManager,
        ILogger<MetadataLookupService> logger)
    {
        _providerManager = providerManager;
        _serverConfigManager = serverConfigManager;
        _logger = logger;
        _cachePath = Path.Combine(
            applicationPaths.PluginConfigurationsPath,
            "Jellyfin.Xtream",
            CacheFileName);
    }

    /// <summary>
    /// Looks up the TMDb ID for a movie using Jellyfin's provider infrastructure.
    /// </summary>
    /// <param name="title">The movie title to search for.</param>
    /// <param name="year">Optional release year for more accurate matching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The TMDb ID if found, null otherwise.</returns>
    public async Task<int?> LookupMovieTmdbIdAsync(string title, int? year, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        // Ensure cache is loaded
        await LoadCacheAsync(cancellationToken).ConfigureAwait(false);

        string cacheKey = $"{title.ToLowerInvariant()}|{year}";

        // Check cache first
        if (_movieCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            _logger.LogDebug("TMDb cache hit for '{Title}' ({Year}): ID {Id}", title, year, cached.ProviderId);
            return cached.ProviderId;
        }

        try
        {
            // Use Jellyfin's provider manager to search
            string? lang = _serverConfigManager.Configuration?.PreferredMetadataLanguage;
            var query = new RemoteSearchQuery<MovieInfo>
            {
                SearchInfo = new MovieInfo
                {
                    Name = title,
                    Year = year,
                    MetadataLanguage = lang ?? string.Empty,
                },
                SearchProviderName = "TheMovieDb",
            };

            var results = await _providerManager
                .GetRemoteSearchResults<Movie, MovieInfo>(query, cancellationToken)
                .ConfigureAwait(false);

            var firstResult = results.FirstOrDefault();
            int? tmdbId = null;

            if (firstResult?.ProviderIds?.TryGetValue(MetadataProvider.Tmdb.ToString(), out var tmdbIdStr) == true)
            {
                if (int.TryParse(tmdbIdStr, out var id))
                {
                    tmdbId = id;
                }
            }

            // Cache the result (including not-found)
            _movieCache[cacheKey] = new MetadataCacheEntry
            {
                ProviderId = tmdbId,
                LookupDate = DateTime.UtcNow
            };

            if (tmdbId.HasValue)
            {
                _logger.LogDebug("TMDb lookup for '{Title}' ({Year}): found ID {Id}", title, year, tmdbId);
            }
            else
            {
                _logger.LogDebug("TMDb lookup for '{Title}' ({Year}): not found", title, year);
            }

            return tmdbId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDb lookup failed for '{Title}' ({Year})", title, year);
            return null;
        }
    }

    /// <summary>
    /// Looks up the TVDb ID for a series using Jellyfin's provider infrastructure.
    /// </summary>
    /// <param name="title">The series title to search for.</param>
    /// <param name="year">Optional premiere year for more accurate matching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The TVDb ID if found, null otherwise.</returns>
    public async Task<int?> LookupSeriesTvdbIdAsync(string title, int? year, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        // Ensure cache is loaded
        await LoadCacheAsync(cancellationToken).ConfigureAwait(false);

        string cacheKey = $"{title.ToLowerInvariant()}|{year}";

        // Check cache first
        if (_seriesCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            _logger.LogDebug("TVDb cache hit for '{Title}' ({Year}): ID {Id}", title, year, cached.ProviderId);
            return cached.ProviderId;
        }

        try
        {
            // Use Jellyfin's provider manager to search
            string? lang = _serverConfigManager.Configuration?.PreferredMetadataLanguage;
            var query = new RemoteSearchQuery<SeriesInfo>
            {
                SearchInfo = new SeriesInfo
                {
                    Name = title,
                    Year = year,
                    MetadataLanguage = lang ?? string.Empty,
                },
                SearchProviderName = "TheTVDB",
            };

            var results = await _providerManager
                .GetRemoteSearchResults<Series, SeriesInfo>(query, cancellationToken)
                .ConfigureAwait(false);

            var firstResult = results.FirstOrDefault();
            int? tvdbId = null;

            if (firstResult?.ProviderIds?.TryGetValue(MetadataProvider.Tvdb.ToString(), out var tvdbIdStr) == true)
            {
                if (int.TryParse(tvdbIdStr, out var id))
                {
                    tvdbId = id;
                }
            }

            // Cache the result (including not-found)
            _seriesCache[cacheKey] = new MetadataCacheEntry
            {
                ProviderId = tvdbId,
                LookupDate = DateTime.UtcNow
            };

            if (tvdbId.HasValue)
            {
                _logger.LogDebug("TVDb lookup for '{Title}' ({Year}): found ID {Id}", title, year, tvdbId);
            }
            else
            {
                _logger.LogDebug("TVDb lookup for '{Title}' ({Year}): not found", title, year);
            }

            return tvdbId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TVDb lookup failed for '{Title}' ({Year})", title, year);
            return null;
        }
    }

    /// <summary>
    /// Generates a folder name for a movie with an optional TMDb ID.
    /// </summary>
    /// <param name="title">The movie title.</param>
    /// <param name="year">Optional release year.</param>
    /// <param name="tmdbId">Optional TMDb ID to include.</param>
    /// <returns>Folder name in format "Title (Year) [tmdbid-ID]" or "Title (Year)" if no ID.</returns>
    public static string GetMovieFolderName(string title, int? year, int? tmdbId)
    {
        string baseName = year.HasValue ? $"{title} ({year})" : title;
        return tmdbId.HasValue ? $"{baseName} [tmdbid-{tmdbId}]" : baseName;
    }

    /// <summary>
    /// Generates a folder name for a series with an optional TVDb ID.
    /// </summary>
    /// <param name="title">The series title.</param>
    /// <param name="year">Optional premiere year.</param>
    /// <param name="tvdbId">Optional TVDb ID to include.</param>
    /// <returns>Folder name in format "Title (Year) [tvdbid-ID]" or "Title (Year)" if no ID.</returns>
    public static string GetSeriesFolderName(string title, int? year, int? tvdbId)
    {
        string baseName = year.HasValue ? $"{title} ({year})" : title;
        return tvdbId.HasValue ? $"{baseName} [tvdbid-{tvdbId}]" : baseName;
    }

    /// <summary>
    /// Loads the metadata cache from disk if not already loaded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        if (_cacheLoaded)
        {
            return;
        }

        if (!File.Exists(_cachePath))
        {
            _cacheLoaded = true;
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            var cacheFile = JsonSerializer.Deserialize<MetadataCacheFile>(json, JsonOptions);

            if (cacheFile?.Movies != null)
            {
                foreach (var kvp in cacheFile.Movies)
                {
                    _movieCache[kvp.Key] = kvp.Value;
                }
            }

            if (cacheFile?.Series != null)
            {
                foreach (var kvp in cacheFile.Series)
                {
                    _seriesCache[kvp.Key] = kvp.Value;
                }
            }

            _logger.LogInformation(
                "Loaded metadata cache: {MovieCount} movies, {SeriesCount} series",
                _movieCache.Count,
                _seriesCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load metadata cache from {Path}", _cachePath);
        }

        _cacheLoaded = true;
    }

    /// <summary>
    /// Saves the metadata cache to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task SaveCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheFile = new MetadataCacheFile
            {
                Movies = new Dictionary<string, MetadataCacheEntry>(_movieCache),
                Series = new Dictionary<string, MetadataCacheEntry>(_seriesCache)
            };

            var json = JsonSerializer.Serialize(cacheFile, JsonOptions);

            string? dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(_cachePath, json, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Saved metadata cache: {MovieCount} movies, {SeriesCount} series",
                _movieCache.Count,
                _seriesCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save metadata cache to {Path}", _cachePath);
        }
    }

    /// <summary>
    /// Clears all cached metadata entries.
    /// </summary>
    public void ClearCache()
    {
        _movieCache.Clear();
        _seriesCache.Clear();
        _logger.LogInformation("Metadata cache cleared");
    }

    /// <summary>
    /// Gets statistics about the current cache state.
    /// </summary>
    /// <returns>Tuple of (movie count, series count, expired count).</returns>
    public (int Movies, int Series, int Expired) GetCacheStats()
    {
        int expiredMovies = _movieCache.Values.Count(e => e.IsExpired);
        int expiredSeries = _seriesCache.Values.Count(e => e.IsExpired);

        return (_movieCache.Count, _seriesCache.Count, expiredMovies + expiredSeries);
    }
}
