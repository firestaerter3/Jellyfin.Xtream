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
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for persisting and loading sync state to enable incremental syncs.
/// State is stored as a JSON file in the plugin's data directory.
/// </summary>
public class SyncStateService : IDisposable
{
    private const string StateFileName = "sync-state.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _statePath;
    private readonly ILogger<SyncStateService>? _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private SyncState? _cachedState;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStateService"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths for determining plugin data directory.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SyncStateService(IApplicationPaths applicationPaths, ILogger<SyncStateService>? logger = null)
    {
        _logger = logger;
        // Store state in the plugin's data directory
        string pluginDataPath = Path.Combine(applicationPaths.PluginConfigurationsPath, "Jellyfin.Xtream");
        Directory.CreateDirectory(pluginDataPath);
        _statePath = Path.Combine(pluginDataPath, StateFileName);
    }

    /// <summary>
    /// Loads the sync state from disk, or returns a new state if none exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or new sync state.</returns>
    public async Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedState != null)
        {
            return _cachedState;
        }

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedState != null)
            {
                return _cachedState;
            }

            if (!File.Exists(_statePath))
            {
                _logger?.LogInformation("No sync state file found, starting fresh");
                _cachedState = new SyncState();
                return _cachedState;
            }

            string json = await File.ReadAllTextAsync(_statePath, cancellationToken).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<SyncState>(json, JsonOptions);

            if (state == null)
            {
                _logger?.LogWarning("Failed to deserialize sync state, starting fresh");
                _cachedState = new SyncState();
                return _cachedState;
            }

            // Ensure dictionaries are initialized (JSON deserialization may not initialize them)
            state.SeriesLastModified ??= new ConcurrentDictionary<int, DateTime>();
            state.MoviesAdded ??= new ConcurrentDictionary<int, DateTime>();

            _logger?.LogInformation(
                "Loaded sync state: LastFullSync={LastFullSync}, {SeriesCount} series, {MovieCount} movies tracked",
                state.LastFullSync,
                state.SeriesLastModified.Count,
                state.MoviesAdded.Count);

            _cachedState = state;
            return state;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error loading sync state from {Path}, starting fresh", _statePath);
            _cachedState = new SyncState();
            return _cachedState;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Saves the sync state to disk.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_statePath, json, cancellationToken).ConfigureAwait(false);
            _cachedState = state;

            _logger?.LogDebug(
                "Saved sync state: LastFullSync={LastFullSync}, {SeriesCount} series, {MovieCount} movies tracked",
                state.LastFullSync,
                state.SeriesLastModified.Count,
                state.MoviesAdded.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save sync state to {Path}", _statePath);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Clears the cached state, forcing a reload from disk on next access.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedState = null;
    }

    /// <summary>
    /// Resets the sync state completely, forcing a full sync on next refresh.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cachedState = new SyncState();

            if (File.Exists(_statePath))
            {
                File.Delete(_statePath);
                _logger?.LogInformation("Sync state reset - deleted {Path}", _statePath);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Checks if a full sync is needed based on the configured interval.
    /// </summary>
    /// <param name="state">The current sync state.</param>
    /// <param name="fullSyncIntervalHours">The configured full sync interval in hours.</param>
    /// <returns>True if a full sync is needed, false otherwise.</returns>
    public static bool IsFullSyncNeeded(SyncState state, int fullSyncIntervalHours)
    {
        if (state.LastFullSync == default)
        {
            return true;
        }

        var timeSinceFullSync = DateTime.UtcNow - state.LastFullSync;
        return timeSinceFullSync.TotalHours >= fullSyncIntervalHours;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SyncStateService and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _fileLock?.Dispose();
        }

        _disposed = true;
    }
}
