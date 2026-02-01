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

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Represents a cached metadata lookup result for a movie or series.
/// </summary>
public class MetadataCacheEntry
{
    /// <summary>
    /// Gets or sets the provider ID (TMDb for movies, TVDb for series).
    /// Null if the lookup found no results.
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// Gets or sets the date when this lookup was performed.
    /// </summary>
    public DateTime LookupDate { get; set; }

    /// <summary>
    /// Gets a value indicating whether this cache entry has expired.
    /// Cache entries expire after 30 days.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow - LookupDate > TimeSpan.FromDays(30);
}
