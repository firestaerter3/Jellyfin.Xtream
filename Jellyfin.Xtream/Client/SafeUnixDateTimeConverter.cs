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
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Converts Unix timestamps to DateTime, gracefully handling empty strings
/// and other malformed values returned by some Xtream API providers.
/// </summary>
public class SafeUnixDateTimeConverter : DateTimeConverterBase
{
    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonToken.Integer)
        {
            long seconds = (long)reader.Value!;
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        if (reader.TokenType == JsonToken.String)
        {
            string? value = reader.Value as string;
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }

            return null;
        }

        return null;
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is DateTime dt)
        {
            long unix = new DateTimeOffset(dt).ToUnixTimeSeconds();
            writer.WriteValue(unix);
        }
        else
        {
            writer.WriteNull();
        }
    }
}
