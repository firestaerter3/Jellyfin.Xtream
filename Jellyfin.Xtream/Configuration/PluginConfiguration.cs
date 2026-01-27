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
using MediaBrowser.Model.Plugins;

#pragma warning disable CA2227
namespace Jellyfin.Xtream.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the base url including protocol and trailing slash.
    /// </summary>
    public string BaseUrl { get; set; } = "https://example.com";

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user agent override.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the Catch-up channel is visible.
    /// </summary>
    public bool IsCatchupVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Series channel is visible.
    /// </summary>
    public bool IsSeriesVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Video On-demand channel is visible.
    /// </summary>
    public bool IsVodVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Video On-demand channel is visible.
    /// </summary>
    public bool IsTmdbVodOverride { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show all series directly without category folders.
    /// When enabled, all series from selected categories appear directly in the library.
    /// </summary>
    public bool FlattenSeriesView { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to show all VOD movies directly without category folders.
    /// When enabled, all movies from selected categories appear directly in the library.
    /// </summary>
    public bool FlattenVodView { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether series caching is enabled.
    /// When enabled, series data is pre-fetched and cached for faster navigation.
    /// </summary>
    public bool EnableSeriesCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache expiration time in minutes for series data.
    /// Default is 600 minutes (10 hours). Data is refreshed when this time expires or when configuration changes.
    /// Maximum is 1380 minutes (23 hours) to ensure refresh happens before 24-hour cache safety expiration.
    /// </summary>
    public int SeriesCacheExpirationMinutes { get; set; } = 600;

    /// <summary>
    /// Gets or sets a value indicating whether HTTP error retry is enabled.
    /// When enabled, transient HTTP 5xx errors are automatically retried with exponential backoff.
    /// </summary>
    public bool EnableHttpRetry { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient HTTP errors.
    /// Default is 3 attempts. Range: 0-10.
    /// </summary>
    public int HttpRetryMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial retry delay in milliseconds for exponential backoff.
    /// Default is 1000ms (1 second). Each retry doubles the delay.
    /// Range: 100-10000ms.
    /// </summary>
    public int HttpRetryInitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the failure cache expiration in hours.
    /// URLs that persistently fail are cached to avoid repeated retries.
    /// Default is 24 hours. Range: 1-168 (1 week).
    /// </summary>
    public int HttpFailureCacheExpirationHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets a value indicating whether to throw exceptions for persistent failures
    /// or silently skip them. Default is false (skip silently after logging).
    /// </summary>
    public bool HttpRetryThrowOnPersistentFailure { get; set; } = false;

    /// <summary>
    /// Gets or sets the channels displayed in Live TV.
    /// </summary>
    public SerializableDictionary<int, HashSet<int>> LiveTv { get; set; } = [];

    /// <summary>
    /// Gets or sets the streams displayed in VOD.
    /// </summary>
    public SerializableDictionary<int, HashSet<int>> Vod { get; set; } = [];

    /// <summary>
    /// Gets or sets the streams displayed in Series.
    /// </summary>
    public SerializableDictionary<int, HashSet<int>> Series { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel override configuration for Live TV.
    /// </summary>
    public SerializableDictionary<int, ChannelOverrides> LiveTvOverrides { get; set; } = [];

    /// <summary>
    /// Gets a hash code based only on cache-relevant configuration.
    /// This excludes settings like refresh frequency that don't affect cached data.
    /// </summary>
    /// <returns>Hash code for cache invalidation purposes.</returns>
    public int GetCacheRelevantHash()
    {
        // Only include settings that affect what data is cached:
        // - Credentials (determines which server/account)
        // - Series selections (determines which series to cache)
        // - FlattenSeriesView (affects data structure)
        int hash = HashCode.Combine(BaseUrl, Username, Password, FlattenSeriesView);

        // Include series selections
        foreach (var kvp in Series)
        {
            hash = HashCode.Combine(hash, kvp.Key);
            foreach (var val in kvp.Value)
            {
                hash = HashCode.Combine(hash, val);
            }
        }

        return hash;
    }
}
#pragma warning restore CA2227
