# Eager Caching - Implementation Details

## Document Info
- **Status:** Implemented
- **Version:** 0.9.5.0+
- **Last Updated:** 2026-01-27
- **Related:** [REQUIREMENTS.md](./REQUIREMENTS.md), [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## Implementation Approach

The eager caching feature was implemented in phases:

1. **Phase 1 (v0.9.4.6)**: Basic caching infrastructure
2. **Phase 2 (v0.9.4.10-16)**: UI controls and cache invalidation fixes
3. **Phase 3 (v0.9.5.0)**: True eager loading with automatic Jellyfin DB population
4. **Phase 4 (v0.9.5.2)**: Clear Cache triggers Jellyfin DB cleanup
5. **Phase 5 (v0.9.5.3)**: Malformed JSON handling for robust caching

---

## Code Changes

### Files Added

#### 1. **Jellyfin.Xtream/Service/SeriesCacheService.cs**
   - **Purpose:** Core caching service that manages series data pre-fetching
   - **Key Methods:**
     - `RefreshCacheAsync()`: Orchestrates the entire cache refresh process
     - `ClearCache()`: Invalidates cache by incrementing version number
     - `GetCachedCategories()`: Retrieves cached categories
     - `GetCachedSeriesList()`: Retrieves cached series for a category
     - `GetCachedSeriesInfo()`: Retrieves cached series details
     - `GetCachedSeason()`: Retrieves cached season data
     - `GetCachedEpisodes()`: Retrieves cached episodes for a season
   - **Cache Key Strategy:**
     ```csharp
     private string CachePrefix => $"series_cache_{Plugin.Instance.CacheDataVersion}_v{_cacheVersion}_";
     ```
   - **Progress Tracking:** Thread-safe progress reporting for UI updates
   - **Cancellation Support:** CancellationTokenSource for stopping refresh operations

#### 2. **Jellyfin.Xtream/Tasks/SeriesCacheRefreshTask.cs**
   - **Purpose:** Jellyfin scheduled task for periodic cache refresh
   - **Trigger:** Runs every N minutes (configurable via `SeriesCacheExpirationMinutes`)
   - **Integration:** Registered in Plugin.cs as IScheduledTask

#### 3. **Jellyfin.Xtream/Service/TaskService.cs**
   - **Purpose:** Triggers Jellyfin scheduled tasks programmatically
   - **Key Method:**
     ```csharp
     public async Task TriggerChannelRefreshTask()
     {
         var task = _taskManager.ScheduledTasks
             .FirstOrDefault(t => t.Name == "Refresh Channels");
         if (task != null)
         {
             await _taskManager.Execute(task, new TaskOptions()).ConfigureAwait(false);
         }
     }
     ```
   - **Usage:** Called after cache refresh to populate Jellyfin DB

### Files Modified

#### 1. **Jellyfin.Xtream/Plugin.cs**

**Lines 52-94:** Constructor initialization with background cache refresh
```csharp
SeriesCacheService = new Service.SeriesCacheService(
    StreamService, memoryCache, loggerFactory.CreateLogger<Service.SeriesCacheService>());

// Start cache refresh in background on startup
if (Configuration.EnableSeriesCaching &&
    !string.IsNullOrEmpty(Configuration.BaseUrl) &&
    Configuration.BaseUrl != "https://example.com" &&
    !string.IsNullOrEmpty(Configuration.Username))
{
    _ = Task.Run(async () =>
    {
        try
        {
            await SeriesCacheService.RefreshCacheAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize series cache");
        }
    });
}
```

**Decision Rationale (2026-01-24):**
- Background task ensures Jellyfin starts quickly without blocking
- Credential check prevents errors during initial setup
- Fire-and-forget pattern (`_ = Task.Run`) is appropriate for startup tasks

**Lines 150-170:** CacheDataVersion property
```csharp
public string CacheDataVersion
{
    get
    {
        // Hash of cache-relevant configuration
        var hash = $"{Configuration.BaseUrl}_{Configuration.Username}_" +
                   $"{Configuration.SeriesCategoryIds}_{Configuration.FlattenSeriesView}";
        return Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(hash)))
            .Substring(0, 8);
    }
}
```

**Decision:** Cache keys include config hash to auto-invalidate when settings change
- Changing credentials = different Xtream provider = different data
- Changing categories = different content = different cache
- Changing FlattenSeriesView = different data structure

#### 2. **Jellyfin.Xtream/Configuration/PluginConfiguration.cs**

**Added Properties:**
```csharp
public bool EnableSeriesCaching { get; set; } = true;
public int SeriesCacheExpirationMinutes { get; set; } = 60;
```

**Default Values Rationale:**
- `EnableSeriesCaching = true`: Eager caching is the primary improvement
- `SeriesCacheExpirationMinutes = 60`: Balance between freshness and API load

#### 3. **Jellyfin.Xtream/SeriesChannel.cs**

**Lines 80-120:** Modified GetChannelItems to check cache first
```csharp
private async Task<IEnumerable<SeriesCategory>> GetAllCategoriesAsync()
{
    if (Plugin.Instance.Configuration.EnableSeriesCaching)
    {
        var cached = Plugin.Instance.SeriesCacheService.GetCachedCategories();
        if (cached != null)
        {
            return cached;
        }
    }

    // Fallback to API if cache miss
    return await Plugin.Instance.StreamService.GetSeriesCategoriesAsync();
}
```

**Performance Impact:**
- Cache hit: ~1ms (memory lookup)
- Cache miss: ~5000ms (API call + deserialization)
- 5000x speedup when cache populated

#### 4. **Jellyfin.Xtream/Configuration/Web/config.html**

**Added UI Controls:**
- "Enable Caching" toggle checkbox
- "Refresh Now" button (triggers cache refresh)
- "Clear Cache" button (invalidates cache)
- Progress bar showing refresh status
- Status text showing current operation

**JavaScript Functions:**
```javascript
function refreshCache() {
    // POST to /Xtream/RefreshCache endpoint
    // Polls /Xtream/CacheStatus for progress updates
    // Updates UI with progress bar and status text
}

function clearCache() {
    // POST to /Xtream/ClearCache endpoint
    // Shows "Clearing..." during operation
    // Resets UI after completion
}
```

#### 5. **Jellyfin.Xtream/Api/XtreamController.cs**

**Added API Endpoints:**
```csharp
[HttpPost("RefreshCache")]
public async Task<ActionResult> RefreshCache()
{
    await Plugin.Instance.SeriesCacheService.RefreshCacheAsync();
    await Plugin.Instance.TaskService.TriggerChannelRefreshTask();
    return Ok();
}

[HttpPost("ClearCache")]
public ActionResult ClearCache()
{
    Plugin.Instance.SeriesCacheService.ClearCache();
    await Plugin.Instance.TaskService.TriggerChannelRefreshTask();
    return Ok();
}

[HttpGet("CacheStatus")]
public ActionResult<CacheStatusResponse> GetCacheStatus()
{
    return new CacheStatusResponse
    {
        IsRefreshing = Plugin.Instance.SeriesCacheService.IsRefreshing,
        Progress = Plugin.Instance.SeriesCacheService.CurrentProgress,
        Status = Plugin.Instance.SeriesCacheService.CurrentStatus
    };
}
```

---

## Configuration

### Plugin Settings

Added to `PluginConfiguration.cs`:

```csharp
/// <summary>
/// Enable pre-fetching and caching of all series data.
/// </summary>
public bool EnableSeriesCaching { get; set; } = true;

/// <summary>
/// How often to refresh the series cache (in minutes).
/// </summary>
public int SeriesCacheExpirationMinutes { get; set; } = 60;
```

### UI Changes

**Configuration Page (`config.html`):**
- Added "Caching" section with toggle and buttons
- Real-time progress bar during refresh
- Status messages for user feedback

---

## Edge Cases Handled

### 1. **Malformed JSON Responses (v0.9.5.3)**

**Problem:** Some Xtream providers return `[]` (array) instead of series object
```json
// Expected:
{"series_id": "123", "title": "Show Name"}

// Received from some providers:
[]
```

**Solution:** Added try-catch with JsonException handling
```csharp
try
{
    var info = await _xtreamClient.GetSeriesInfoByIdAsync(seriesId);
    _memoryCache.Set(cacheKey, info, cacheOptions);
}
catch (System.Text.Json.JsonException ex)
{
    _logger?.LogWarning(ex, "Malformed JSON for series {SeriesId}, storing empty data", seriesId);
    _memoryCache.Set(cacheKey, new SeriesStreamInfo(), cacheOptions);
}
```

**Impact:** Series with bad API responses no longer break cache refresh

### 2. **Clear Cache Doesn't Stop Refresh (v0.9.4.10)**

**Problem:** Clear Cache button cleared cache but didn't cancel running refresh
**Solution:** Cancel refresh operation before clearing cache
```csharp
public void ClearCache()
{
    _refreshCancellationTokenSource?.Cancel();
    _cacheVersion++;
    _logger?.LogInformation("Cache invalidated, new version: {Version}", _cacheVersion);
}
```

### 3. **Clear Cache Doesn't Clean Jellyfin DB (v0.9.5.2)**

**Problem:** Clearing plugin cache didn't remove old items from `jellyfin.db`
**Solution:** Trigger Jellyfin channel refresh after clearing cache
```csharp
[HttpPost("ClearCache")]
public async Task<ActionResult> ClearCache()
{
    Plugin.Instance.SeriesCacheService.ClearCache();
    // Trigger Jellyfin to refresh from API (cache now empty)
    await Plugin.Instance.TaskService.TriggerChannelRefreshTask();
    return Ok();
}
```

**Effect:** Jellyfin fetches from API (cache miss), updates DB with current data

### 4. **Concurrent Refresh Attempts**

**Problem:** Multiple refresh triggers could run simultaneously
**Solution:** Semaphore lock with immediate return if locked
```csharp
private readonly SemaphoreSlim _refreshLock = new(1, 1);

public async Task RefreshCacheAsync(...)
{
    if (!await _refreshLock.WaitAsync(0, cancellationToken))
    {
        _logger?.LogInformation("Cache refresh already in progress, skipping");
        return;
    }

    try { /* ... */ }
    finally { _refreshLock.Release(); }
}
```

### 5. **Jellyfin Startup Without Credentials**

**Problem:** Plugin tried to cache before credentials configured
**Solution:** Check credentials before starting background refresh
```csharp
if (Configuration.EnableSeriesCaching &&
    !string.IsNullOrEmpty(Configuration.BaseUrl) &&
    Configuration.BaseUrl != "https://example.com" &&
    !string.IsNullOrEmpty(Configuration.Username))
{
    // Start background refresh
}
```

---

## API Changes

### New Public Methods

**Plugin.cs:**
```csharp
public SeriesCacheService SeriesCacheService { get; }
public TaskService TaskService { get; }
public string CacheDataVersion { get; }
```

**SeriesCacheService.cs:**
```csharp
public async Task RefreshCacheAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
public void ClearCache()
public IEnumerable<SeriesCategory>? GetCachedCategories()
public IEnumerable<SeriesStream>? GetCachedSeriesList(string categoryId)
public SeriesStreamInfo? GetCachedSeriesInfo(string seriesId)
public Season? GetCachedSeason(string seriesId, int seasonNumber)
public IEnumerable<Episode>? GetCachedEpisodes(string seriesId, int seasonNumber)
public bool IsRefreshing { get; }
public double CurrentProgress { get; }
public string CurrentStatus { get; }
```

**TaskService.cs:**
```csharp
public async Task TriggerChannelRefreshTask()
```

### New REST API Endpoints

**XtreamController.cs:**
```csharp
POST /Xtream/RefreshCache  - Triggers cache refresh + Jellyfin DB population
POST /Xtream/ClearCache    - Invalidates cache + triggers DB cleanup
GET  /Xtream/CacheStatus   - Returns refresh progress/status
```

---

## Performance Optimizations

### 1. **Parallel Series Fetching**

**Implementation:**
```csharp
var allSeries = new List<SeriesStream>();
foreach (var category in categories)
{
    var seriesInCategory = await GetSeriesForCategoryAsync(category.CategoryId);
    allSeries.AddRange(seriesInCategory);
}

// Fetch all series info in parallel (max 10 concurrent)
var semaphore = new SemaphoreSlim(10);
var tasks = allSeries.Select(async series => {
    await semaphore.WaitAsync(cancellationToken);
    try {
        await FetchSeriesDataAsync(series.SeriesId, cancellationToken);
    }
    finally {
        semaphore.Release();
    }
});
await Task.WhenAll(tasks);
```

**Impact:** 10x faster refresh for large libraries

### 2. **Cache Entry Expiration**

**Configuration:**
```csharp
var cacheOptions = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
};
```

**Rationale:** 24-hour safety net in case scheduled refresh fails

### 3. **Lazy Jellyfin DB Population**

**Strategy:** Don't wait for Jellyfin refresh to complete
```csharp
await SeriesCacheService.RefreshCacheAsync();  // Wait for plugin cache
_ = TaskService.TriggerChannelRefreshTask();   // Fire-and-forget Jellyfin refresh
return Ok();  // Return immediately
```

---

## Known Limitations

1. **Cache lost on restart**: IMemoryCache is in-process, not persistent
   - Mitigation: Auto-refresh on startup

2. **No incremental updates**: Full refresh every time
   - Acceptable: Most Xtream APIs don't support delta queries

3. **Memory usage scales with library size**: ~1KB per episode
   - Acceptable: 10,000 episodes = ~10MB

4. **Jellyfin DB orphans**: Removed categories/series stay in DB until manual cleanup
   - Mitigation (v0.9.5.2): Clear Cache now triggers DB cleanup

---

## Testing Notes

### Manual Test Procedure

1. **Enable caching**: Check "Enable Caching" in plugin settings
2. **Trigger refresh**: Click "Refresh Now"
3. **Verify progress**: Progress bar should show 0-100%
4. **Check logs**: `docker logs jellyfin` shows cache refresh progress
5. **Browse library**: All series/episodes should load instantly
6. **Clear cache**: Click "Clear Cache"
7. **Verify cleanup**: Old items should disappear from Jellyfin library

### Performance Benchmarks

**Test Environment:** 200 series, 2500 episodes

| Operation | Before Eager Caching | After Eager Caching |
|-----------|---------------------|---------------------|
| Cache Refresh | N/A | 18 minutes |
| Jellyfin DB Population | N/A | 8 minutes |
| Browse Series List | 8 seconds (API call) | Instant (DB) |
| Open Series Details | 6 seconds (API call) | Instant (DB) |
| Browse Episodes | 10 seconds (API call) | Instant (DB) |

**Total user benefit:** 24 seconds → instant for typical browse session

---

## Deployment Considerations

### Docker Deployment

**File Permissions:** Plugin DLL must be owned by `abc:abc` (Jellyfin user)
```bash
docker cp Jellyfin.Xtream.dll jellyfin:/config/plugins/Jellyfin.Xtream_0.9.5.0/
docker exec jellyfin chown -R abc:abc /config/plugins/Jellyfin.Xtream_0.9.5.0/
docker restart jellyfin
```

**Container Resources:** Ensure adequate memory for caching large libraries
- Minimum: 512MB RAM (small libraries)
- Recommended: 2GB RAM (100+ series)

### Logging

**Key Log Messages:**
```
[INF] Starting series data cache refresh
[INF] Cached 12 categories
[INF] Fetching series for category: Movies
[INF] Cached 45 series in category Movies
[INF] Fetching seasons and episodes for 45 series...
[INF] Progress: 50% - Fetching series data (23/45)
[INF] Cache refresh completed in 00:18:23
[INF] Triggering Jellyfin channel refresh to populate database
```

---

## Related Commits

- `de9fd6b` - Implement true eager loading by auto-populating Jellyfin database (v0.9.5.0)
- `111c298` - Clear Cache now triggers Jellyfin refresh to clean up jellyfin.db (v0.9.5.2)
- `6f5509e` - Fix malformed JSON responses from Xtream API (v0.9.5.3)
- `3115144` - Fix Clear Cache not stopping cache refresh operation
- `02cfd2d` - Fix Clear Cache button getting stuck on 'Clearing...'
- `7046bc1` - Add cache maintenance buttons and configurable refresh frequency

---

## Future Improvements

See [TODO.md](./TODO.md) for planned enhancements.
