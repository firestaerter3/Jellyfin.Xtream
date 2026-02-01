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

#pragma warning disable CA2227
namespace Jellyfin.Xtream.Service;

/// <summary>
/// Represents the persistent state for incremental sync.
/// Tracks when series and movies were last modified to avoid re-processing unchanged content.
/// </summary>
public class SyncState
{
    /// <summary>
    /// Gets or sets the timestamp of the last full sync.
    /// A full sync fetches all series/episodes regardless of timestamps.
    /// </summary>
    public DateTime LastFullSync { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last incremental sync.
    /// An incremental sync only processes items that have changed since the last sync.
    /// </summary>
    public DateTime LastIncrementalSync { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp for each series.
    /// Key is the series ID, value is the LastModified timestamp from the API.
    /// </summary>
    public ConcurrentDictionary<int, DateTime> SeriesLastModified { get; set; } = new();

    /// <summary>
    /// Gets or sets the added timestamp for each movie.
    /// Key is the stream ID, value is the Added timestamp from the API.
    /// </summary>
    public ConcurrentDictionary<int, DateTime> MoviesAdded { get; set; } = new();
}
#pragma warning restore CA2227
