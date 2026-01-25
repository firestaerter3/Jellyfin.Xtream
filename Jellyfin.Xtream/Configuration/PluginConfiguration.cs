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
    /// Gets or sets the cache expiration time in minutes for series data.
    /// Default is 60 minutes (1 hour). Data is refreshed when this time expires or when configuration changes.
    /// </summary>
    public int SeriesCacheExpirationMinutes { get; set; } = 60;

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
