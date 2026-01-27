# Caching Architecture: How Series Data Flows Through the System

This document explains how series data is cached and flows through three distinct layers: the Xtream API, the plugin's in-memory cache, Jellyfin's database, and the browser cache.

## Table of Contents
- [Overview](#overview)
- [The Three-Layer Caching System](#the-three-layer-caching-system)
- [Data Flow](#data-flow)
- [Cache Keys and Versioning](#cache-keys-and-versioning)
- [When Things Get Out of Sync](#when-things-get-out-of-sync)
- [Troubleshooting](#troubleshooting)

---

## Overview

The plugin implements a **three-layer caching architecture** for **eager loading**: pre-populate Jellyfin's database upfront so all series/seasons/episodes are ready for instant browsing without waiting for API calls.

```
┌─────────────────┐
│  Xtream API     │  ← Source of truth (remote)
└────────┬────────┘
         │ Batched API calls (700+ calls in 10-20 minutes)
         ↓
┌─────────────────┐
│ Plugin In-Memory│  ← STAGING BUFFER for eager loading
│     Cache       │     Purpose: Batch API calls, serve Jellyfin quickly
│ (SeriesCache)   │     Stores: categories, series, seasons, episodes
└────────┬────────┘
         │ Triggers Jellyfin channel refresh
         ↓
┌─────────────────┐
│  Jellyfin DB    │  ← PRIMARY CACHE (persistent, fast)
│ (jellyfin.db)   │     Populated from plugin cache (fast lookups)
│                 │     Stores: metadata, item relationships, state
└────────┬────────┘
         │ HTTP API responses
         ↓
┌─────────────────┐
│  Browser Cache  │  ← Client-side cache (user's browser)
│                 │     Caches: API responses, rendered UI
└─────────────────┘
```

**Key Insight:** The plugin cache is NOT redundant with Jellyfin's DB. It serves as a **staging buffer** that enables:
1. Batching hundreds of API calls upfront (minimize rate limiting)
2. Serving Jellyfin's 700+ GetChannelItems() calls from RAM (fast)
3. Pre-populating jellyfin.db with all content (instant browsing)

---

## The Three-Layer Caching System

### Layer 1: Plugin In-Memory Cache (`SeriesCacheService`)

**Location:** `Jellyfin.Xtream/Service/SeriesCacheService.cs`

**Purpose:** Pre-fetch and cache all series data from the Xtream API to minimize API calls.

**What it caches:**
- Categories (e.g., "NLZIET", "HISTORY PLAY")
- Series within each category
- Seasons within each series
- Episodes within each season

**Storage:** Uses .NET's `IMemoryCache` (in-process memory, non-persistent)

**Cache keys format:**
```
series_cache_{CacheDataVersion}_v{CacheVersion}_{itemType}_{id}

Examples:
- series_cache_abc123_v0_categories
- series_cache_abc123_v0_seriesinfo_19259
- series_cache_abc123_v0_episodes_19259_1
- series_cache_abc123_v0_season_19259_1
```

**Expiration:** 24 hours (safety mechanism to prevent memory leaks)

**Refresh trigger:**
- On plugin startup (if caching is enabled)
- Scheduled task (default: every 60 minutes) via `SeriesCacheRefreshTask`
- Manual refresh via "Clear Cache" button in plugin settings

**Key characteristics:**
- ✅ **Fast:** In-memory lookups are extremely fast (microseconds)
- ✅ **Reduces API load:** Batches all API calls upfront
- ❌ **Non-persistent:** Lost when Jellyfin restarts
- ❌ **Memory intensive:** Stores all series data in RAM

**Code flow:**
```csharp
RefreshCacheAsync()
├── Fetch all categories → Cache as "categories"
├── For each category:
│   ├── Fetch all series → Don't cache series list directly
│   └── For each series:
│       ├── Fetch seasons
│       ├── For each season:
│       │   ├── Fetch episodes → Cache as "episodes_{seriesId}_{seasonId}"
│       │   └── Cache season info → Cache as "season_{seriesId}_{seasonId}"
│       └── Cache series stream info → Cache as "seriesinfo_{seriesId}"
└── Log completion stats
```

**Important notes:**
1. **No series list cache:** The cache stores series metadata (SeriesStreamInfo) but NOT the list of series IDs per category. When `GetAllSeriesFlattened()` is called, it must make API calls to `GetSeries(categoryId)` even when caching is enabled.
2. **Cache version invalidation:** When you click "Clear Cache", it increments `_cacheVersion`, effectively invalidating all old cache keys without deleting them from memory.

---

### Layer 2: Jellyfin Database (`jellyfin.db`)

**Location:** `/config/data/library.db` (SQLite database)

**Purpose:** Jellyfin's persistent storage for all library items, metadata, and relationships.

**What it stores:**
- **Library items:** Series, seasons, episodes as database rows
- **Metadata:** Names, descriptions, images, ratings, etc.
- **Relationships:** Parent-child links (series → season → episode)
- **User state:** Watch progress, favorites, ratings
- **Channel mappings:** Links channel items to their source (plugin ID)

**Key tables (simplified):**
```sql
TypedBaseItems
├── Id (GUID)
├── Type (Series, Season, Episode)
├── Name
├── ParentId (links to parent item)
├── Path (for file-based items, empty for channels)
└── ChannelId (for channel items)

ItemValues
├── ItemId
└── CleanValue (for search/filtering)

MediaStreams
├── ItemId
└── (video/audio stream info)
```

**How the plugin interacts with Jellyfin DB:**

1. **Plugin returns ChannelItemInfo:**
   ```csharp
   return new ChannelItemInfo() {
       Id = "384c775d-0000-9b00-0000-b46b00000000",  // GUID
       Name = "The Curse of Oak Island",
       Type = ChannelItemType.Folder,  // or Media
       FolderType = ChannelFolderType.Series,
       SeriesName = "The Curse of Oak Island",
       ImageUrl = "https://...",
       // ... other metadata
   };
   ```

2. **Jellyfin processes this and:**
   - Creates/updates a row in `TypedBaseItems` table
   - Stores metadata in related tables
   - Establishes parent-child relationships
   - Triggers metadata providers (TvDB, TvMaze) to fetch additional data

3. **Jellyfin maintains this data:**
   - Even when plugin cache is cleared
   - Even when Jellyfin restarts
   - Until explicitly deleted or library is scanned/refreshed

**Key characteristics:**
- ✅ **Persistent:** Survives restarts
- ✅ **Fast lookups:** Indexed database queries
- ✅ **Relationship management:** Tracks item hierarchy
- ⚠️ **Can become stale:** If plugin data changes but Jellyfin doesn't refresh
- ❌ **Opaque to plugin:** Plugin cannot directly read/write to it

---

### Layer 3: Browser Cache

**Location:** User's web browser (localStorage, sessionStorage, HTTP cache)

**Purpose:** Client-side caching to reduce network requests and improve UI responsiveness.

**What it caches:**
- API response data (JSON)
- Rendered HTML/UI components
- Images and media metadata
- Previous page states

**Key characteristics:**
- ✅ **Very fast:** No network requests needed
- ✅ **Reduces server load:** Fewer HTTP requests
- ❌ **Can show stale data:** Especially after plugin/server changes
- ❌ **User-specific:** Each browser has its own cache

**Why hard refresh (Ctrl+Shift+R) is needed:**
- Normal refresh: Browser may use cached API responses
- Hard refresh: Forces browser to re-fetch all data from server

---

## Data Flow

### Scenario 0: Eager Loading (Cache Refresh with Auto-Populate)

**This is the PRIMARY use case - pre-populating jellyfin.db for instant browsing:**

```
Background task triggers (startup or every 60 minutes)
         ↓
1. RefreshCacheAsync() fetches ALL data from Xtream API:
   ├── Fetch categories (1 API call)
   ├── For each category: Fetch series list (N API calls)
   ├── For each series: Fetch seasons (M API calls)
   └── For each season: Fetch episodes (P API calls)
   Total: ~700 API calls batched over 10-20 minutes
         ↓
2. Store everything in plugin IMemoryCache:
   ├── series_cache_{version}_categories
   ├── series_cache_{version}_serieslist_{categoryId}
   ├── series_cache_{version}_seriesinfo_{seriesId}
   ├── series_cache_{version}_season_{seriesId}_{seasonId}
   └── series_cache_{version}_episodes_{seriesId}_{seasonId}
         ↓
3. Trigger Jellyfin channel refresh:
   Plugin.Instance.TaskService.CancelIfRunningAndQueue(
       "Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask")
         ↓
4. Jellyfin calls GetChannelItems() ~700 times:
   ├── GetChannelItems(root) → Get all series
   ├── GetChannelItems(series1) → Get seasons (from cache!)
   ├── GetChannelItems(season1) → Get episodes (from cache!)
   └── ... (all from cache, microseconds per call)
         ↓
5. Jellyfin populates jellyfin.db with everything
   Database now contains all series/seasons/episodes
         ↓
6. Users browse → Instant! Everything already in jellyfin.db
```

**Result:**
- API calls: Batched upfront (one-time cost)
- Jellyfin DB: Fully populated (ready for browsing)
- User experience: Instant browsing, no waiting

**Without this eager loading:**
- User clicks series → API call (5-10 seconds wait)
- User clicks season → API call (5-10 seconds wait)
- User clicks episode → API call (5-10 seconds wait)
- Terrible browsing experience

### Scenario 1: Fresh Start (Cache Enabled)

```
User opens Xtream Series
         ↓
1. Browser → Jellyfin: "GET /Channels/Items?channelId=..."
         ↓
2. Jellyfin → Plugin: GetChannelItems(query)
         ↓
3. Plugin checks SeriesCacheService:
   - Is cache populated? → Check _memoryCache.TryGetValue("series_cache_..._categories")
   - YES → Use cached categories
   - NO → Call API, then cache it
         ↓
4. Plugin checks if FlattenSeriesView is enabled:
   - YES → Call GetAllSeriesFlattened()
     ├── Get categories from cache ✓
     ├── For each category: Call API GetSeries(categoryId) ← NOT CACHED!
     └── Return List<ChannelItemInfo>
   - NO → Return categories as folders
         ↓
5. Plugin returns ChannelItemResult to Jellyfin
         ↓
6. Jellyfin processes items:
   - Checks if items exist in jellyfin.db
   - Creates new items or updates existing ones
   - Triggers metadata providers (TvDB, TvMaze)
   - Stores in database
         ↓
7. Jellyfin → Browser: Returns JSON with item data
         ↓
8. Browser renders UI
```

### Scenario 2: Cache Hit (Returning to Previously Viewed Season)

```
User clicks "Season 1" (already viewed before)
         ↓
1. Browser: Check if response is cached
   - YES → Show cached data immediately (may be stale!)
   - NO → Continue to step 2
         ↓
2. Browser → Jellyfin: "GET /Channels/Items?folderId=..."
         ↓
3. Jellyfin checks database:
   - Items exist in jellyfin.db? → Return from database (fast!)
   - Items don't exist? → Call plugin
         ↓
4. Plugin: GetChannelItems(folderId=season_guid)
   ├── Parse GUID to extract seriesId, seasonId
   ├── Check cache: GetCachedEpisodes(seriesId, seasonId)
   ├── Cache HIT → Return episodes from memory
   └── Cache MISS → Call API GetEpisodes(), cache, return
         ↓
5. Jellyfin → Browser: Return items
         ↓
6. Browser renders (uses server data, may update cache)
```

### Scenario 3: Category Changed (e.g., NLZIET → HISTORY PLAY)

```
User changes selected categories in plugin settings
         ↓
1. Settings saved → Plugin configuration file updated
         ↓
2. Plugin: CacheDataVersion changes (settings affect cache keys)
         ↓
3. Cache is invalidated (old cache keys no longer match)
         ↓
4. Background task triggers RefreshCacheAsync()
         ↓
5. Plugin fetches NEW category data from API
         ↓
6. User refreshes Xtream Series page:
   - Browser may still have old data cached
   - Jellyfin DB may still have old series stored
         ↓
7. Plugin returns NEW series list
         ↓
8. Jellyfin processes:
   - New items → Added to database
   - Old items not in new list → ???
     ├── Option A: Left in database (orphaned)
     ├── Option B: Removed after timeout/scan
   - Triggers "Removing item" log messages
         ↓
9. Browser shows new content (after hard refresh)
```

---

## Cache Keys and Versioning

### Cache Key Structure

```
series_cache_{CacheDataVersion}_v{CacheVersion}_{type}_{id}
             └──────┬──────────┘   └────┬────┘  └─┬─┘ └┬┘
                    │                   │         │    └─ Item ID (seriesId, seasonId, etc.)
                    │                   │         └───── Type (categories, seriesinfo, episodes, season)
                    │                   └──────────────── Cache version (increments on invalidation)
                    └──────────────────────────────────── Plugin config hash
```

### CacheDataVersion

Calculated in `Plugin.cs`:
```csharp
public string CacheDataVersion
{
    get
    {
        // Hash of settings that affect cached data
        string settingsHash = GetSettingsHash(Configuration);
        return $"{settingsHash}";
    }
}
```

**When it changes:**
- Categories selection changed
- Server URL changed
- Credentials changed
- Series filtering rules changed

**When it DOESN'T change:**
- Cache refresh frequency changed (doesn't affect data)
- UI settings changed

**Effect when changed:**
- Old cache keys become inaccessible
- New cache refresh uses new key prefix
- Old data remains in memory until GC or expiration

### Cache Version

Managed by `SeriesCacheService`:
```csharp
private int _cacheVersion = 0;

public void InvalidateCache()
{
    _cacheVersion++;  // Increments on "Clear Cache"
    // Old keys: series_cache_abc_v0_*
    // New keys: series_cache_abc_v1_*
}
```

**Effect:**
- Instant invalidation without memory cleanup
- Old data remains in memory but is inaccessible
- Next cache refresh uses new version number

---

## When Things Get Out of Sync

### Problem 1: Browser Shows Empty/Old Content

**Symptoms:**
- Seasons show but episodes don't appear
- Old series still visible after changing categories
- Page shows spinner indefinitely

**Cause:**
- Browser cache has old API responses
- Browser doesn't know server data changed

**Solution:**
1. **Hard refresh:** Ctrl+Shift+R (Windows/Linux) or Cmd+Shift+R (Mac)
2. **Clear browser cache:** DevTools → Network → Disable cache
3. **Incognito mode:** Test without any cache

### Problem 2: Jellyfin DB Has Stale Items

**Symptoms:**
- Old series appear in search results
- Items show up in library but shouldn't
- "Removing item" log spam during library refresh

**Cause:**
- Jellyfin created items in database from old plugin data
- Plugin changed categories but Jellyfin DB wasn't updated
- Orphaned items remain in database

**What happens:**
```
1. User had NLZIET enabled (193 series)
   → Jellyfin DB has 193 Series items

2. User switches to HISTORY PLAY (5 series)
   → Plugin returns 5 new series
   → Jellyfin DB still has 193 old items

3. Jellyfin library manager:
   - Sees new items → Adds them
   - Sees old items not in plugin response → Marks for removal
   - Logs: "Removing item, Type: Series, Name: ..."
   - Deletes old items (can take several minutes)
```

**Solutions:**
1. **Wait:** Let Jellyfin finish removing old items (may take 5-10 minutes)
2. **Library scan:** Dashboard → Libraries → Scan Library
3. **Nuclear option:** Delete `/config/data/library.db` (loses ALL Jellyfin data!)

### Problem 3: Plugin Cache Miss with Caching Enabled

**Symptoms:**
- GetAllSeriesFlattened returns 0 series
- Logs show: "GetAllSeriesFlattened found 1 categories, got 0 series"
- UI shows empty even though cache has data

**Cause:**
- **Cache doesn't store series lists per category!**
- Cache only stores: categories, SeriesStreamInfo, episodes, seasons
- When `GetAllSeriesFlattened()` calls `GetSeries(categoryId)`, it ALWAYS hits API
- If series aren't selected in plugin config, API returns empty

**Code analysis:**
```csharp
// SeriesCacheService.cs - What gets cached
_memoryCache.Set($"{cachePrefix}categories", categoryList, options);           // ✓ Categories
_memoryCache.Set($"{cachePrefix}seriesinfo_{series.SeriesId}", info, options); // ✓ Series metadata
_memoryCache.Set($"{cachePrefix}episodes_{seriesId}_{seasonId}", episodes);    // ✓ Episodes
_memoryCache.Set($"{cachePrefix}season_{seriesId}_{seasonId}", season);        // ✓ Season info

// What's NOT cached:
// ✗ List of series IDs per category
```

```csharp
// SeriesChannel.cs - GetAllSeriesFlattened
foreach (Category category in categories) {
    // This ALWAYS calls the API, even with caching enabled!
    IEnumerable<Series> series = await StreamService.GetSeries(category.CategoryId);
    items.AddRange(series.Select(CreateChannelItemInfo));
}
```

```csharp
// StreamService.cs - GetSeries filters by configuration
public async Task<IEnumerable<Series>> GetSeries(int categoryId) {
    // Check plugin configuration
    if (!Configuration.Series.ContainsKey(categoryId)) {
        return new List<Series>();  // Returns empty if category not configured!
    }

    // Fetch from API
    List<Series> series = await xtreamClient.GetSeriesByCategoryAsync(categoryId);

    // Filter by selected series in config
    return series.Where(s => IsConfigured(Configuration.Series, s.CategoryId, s.SeriesId));
}
```

**Configuration structure:**
```xml
<Series>
  <Item>
    <Key>580</Key>  <!-- Category ID (HISTORY PLAY) -->
    <Value>
      <ArrayOfInt>  <!-- List of selected series IDs -->
        <int>29923</int>  <!-- Must contain series IDs! -->
        <int>51079</int>
      </ArrayOfInt>
    </Value>
  </Item>
</Series>
```

**Solutions:**
1. **Check plugin settings:** Expand category dropdown and select individual series
2. **Re-save settings:** Sometimes UI doesn't update config properly
3. **Check XML config:** Verify `/config/plugins/configurations/Jellyfin.Xtream.xml`
4. **Fix in code:** Cache series lists per category (enhancement needed)

---

## Troubleshooting

### Check Plugin Cache Status

**Via logs:**
```bash
grep "Cache refresh completed" /config/log/log_*.log
# Output: Cache refresh completed: 5 series, 10 seasons, 96 episodes across 1 categories
```

**Via API (if exposed):**
```bash
# Check if cache is populated
curl http://localhost:8096/Xtream/status
```

### Check Jellyfin Database

**Count channel items:**
```bash
sqlite3 /config/data/library.db "
SELECT Type, COUNT(*)
FROM TypedBaseItems
WHERE ChannelId IS NOT NULL
GROUP BY Type;
"
```

**Find orphaned items:**
```bash
sqlite3 /config/data/library.db "
SELECT Id, Type, Name, ChannelId
FROM TypedBaseItems
WHERE ChannelId = '5d774c35-8567-46d3-a950-9bb8227a0c5d'  -- Plugin GUID
ORDER BY Type, Name;
"
```

### Check Plugin Configuration

```bash
cat /config/data/plugins/configurations/Jellyfin.Xtream.xml | grep -A 10 "<Series>"
```

### Monitor Real-Time Caching

```bash
tail -f /config/log/log_*.log | grep "SeriesCacheService\|GetChannelItems\|GetEpisodes"
```

### Enable Verbose Logging

In `Jellyfin.Xtream/SeriesChannel.cs`, the diagnostic logging is already active:
```csharp
logger.LogInformation("GetChannelItems called - FolderId: {FolderId}", query.FolderId ?? "(root)");
logger.LogInformation("GetAllSeriesFlattened called");
logger.LogInformation("GetAllSeriesFlattened found {Count} categories", categories.Count());
logger.LogInformation("GetEpisodes called - seriesId: {SeriesId}, seasonId: {SeasonId}", seriesId, seasonId);
logger.LogInformation("GetEpisodes returning {Count} episodes", items.Count);
```

---

## Best Practices

### For Users

1. **Enable caching** unless you have a specific reason not to
2. **Use hard refresh** (Ctrl+Shift+R) after changing settings
3. **Wait for cache refresh** to complete before navigating (check logs)
4. **Select series individually** when enabling a category
5. **Don't spam "Clear Cache"** - it triggers full API refresh every time

### For Developers

1. **Check cache first** before making API calls
2. **Handle cache misses gracefully** - fallback to API
3. **Log cache hits/misses** for debugging
4. **Use version-based invalidation** instead of deleting keys
5. **Consider caching series lists** per category (current limitation)

### For Troubleshooting

1. **Check logs first** - they tell you exactly what's happening
2. **Verify configuration** - is the series selected?
3. **Check all three layers** - plugin cache, Jellyfin DB, browser cache
4. **Hard refresh browser** before assuming plugin bug
5. **Wait for Jellyfin cleanup** - removing old items takes time

---

## Future Improvements

### Potential Enhancements

1. **Cache series lists per category:**
   ```csharp
   _memoryCache.Set($"{cachePrefix}serieslist_{categoryId}", seriesList, options);
   ```
   This would eliminate API calls in `GetAllSeriesFlattened()`.

2. **Expose cache status API:**
   ```http
   GET /Xtream/cache/status
   {
     "isPopulated": true,
     "lastRefresh": "2026-01-27T00:15:00Z",
     "seriesCount": 193,
     "categoryCount": 1
   }
   ```

3. **Automatic browser cache invalidation:**
   - Include cache version in API responses
   - Browser detects mismatch and auto-refreshes

4. **Jellyfin DB cleanup on category change:**
   - Plugin signals Jellyfin to remove old items immediately
   - Reduces "orphaned item" confusion

5. **Configuration validation:**
   - Warn user if category enabled but no series selected
   - Auto-select all series when enabling category

---

## Related Documents

- **[EAGER_CACHING_REQUIREMENTS.md](./EAGER_CACHING_REQUIREMENTS.md)** - Formal requirements specification
- **[EAGER_CACHING_TEST_CASES.md](./EAGER_CACHING_TEST_CASES.md)** - Test cases for validation
- **[CACHE_INVALIDATION_CHALLENGE.md](./CACHE_INVALIDATION_CHALLENGE.md)** - Cache invalidation analysis

---

## Summary

The caching architecture involves **three layers working together for eager loading**:

| Layer | Storage | Persistence | Purpose | Real Value |
|-------|---------|-------------|---------|------------|
| Plugin Cache | RAM (IMemoryCache) | Lost on restart | **Staging buffer** | Batch API calls, serve Jellyfin fast |
| Jellyfin DB | SQLite (library.db) | Persistent | **Primary cache** | Pre-populated for instant browsing |
| Browser Cache | Browser storage | Per-session | **UI cache** | Reduce HTTP requests |

**The Real Value of Plugin Cache:**

The plugin cache is NOT redundant - it's **essential for eager loading**:

1. **Without plugin cache:**
   - Jellyfin calls GetChannelItems() 700 times
   - Each call → API call to Xtream
   - Result: Hours to populate, rate limiting, timeouts

2. **With plugin cache (current implementation):**
   - RefreshCacheAsync() batches 700 API calls upfront (10-20 min)
   - Jellyfin calls GetChannelItems() 700 times
   - Each call → Cache lookup (microseconds)
   - Result: Jellyfin DB populated in minutes, instant browsing

**Key takeaway:** The plugin cache is the **minimum complexity needed** to achieve eager loading without hitting API rate limits. It's not about redundancy - it's about batching API calls and serving Jellyfin quickly to pre-populate jellyfin.db for instant user browsing.
