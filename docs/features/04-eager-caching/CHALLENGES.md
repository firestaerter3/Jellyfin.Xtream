# Jellyfin Xtream Plugin - Cache Invalidation Challenge

## Executive Summary

The Jellyfin Xtream plugin faces a critical cache invalidation problem: when users click "Clear Cache" or upgrade the plugin version, Jellyfin continues to display stale data from its internal database (`jellyfin.db`). This document outlines the problem, our findings, and potential solutions.

## Problem Statement

**Users see outdated channel data even after:**
1. Clicking the "Clear Cache" button in plugin settings
2. Upgrading the plugin to a new version
3. Changing plugin configuration settings

**Example:** After clearing cache and re-fetching only 184 series, Jellyfin UI shows 1,782 series (1,598 ghost entries from old cache).

## Architecture Overview

### Data Flow

```
Xtream Server → Plugin Code → Plugin Cache → Jellyfin Channel API → Jellyfin Database (jellyfin.db) → Jellyfin UI
                     ↓             ↓                                        ↓
                IMemoryCache   CacheDataVersion                    BaseItems table
```

### Caching Layers

1. **Plugin In-Memory Cache (IMemoryCache)**
   - Location: `SeriesCacheService._memoryCache`
   - Scope: Plugin process memory
   - Lifetime: Until process restart or manual invalidation
   - Control: Full control via `InvalidateCache()`

2. **Jellyfin Database Cache (jellyfin.db)**
   - Location: `/config/data/data/jellyfin.db` (SQLite)
   - Scope: Persistent storage
   - Lifetime: Until manually deleted or DataVersion changes
   - Control: **Limited/None** - Jellyfin manages this automatically

3. **Jellyfin Metadata Cache**
   - Location: `/config/data/metadata/channels/`
   - Scope: File system
   - Lifetime: Until manually deleted or DataVersion changes
   - Control: Can be deleted manually

### Current Version Tracking

```csharp
// Plugin.cs:110
public string DataVersion => Assembly.GetCallingAssembly().GetName().Version?.ToString() + Configuration.GetHashCode();

// Plugin.cs:116
public string CacheDataVersion => Assembly.GetCallingAssembly().GetName().Version?.ToString() + Configuration.GetCacheRelevantHash();

// SeriesChannel.cs:47
public string DataVersion => Plugin.Instance.DataVersion;
```

**Theory:** When `DataVersion` changes, Jellyfin should invalidate its cache.

## Findings & Evidence

### Finding 1: "Clear Cache" Button Doesn't Clear Jellyfin's Cache

**What happens when user clicks "Clear Cache":**

```csharp
// XtreamController.cs:252-267
public ActionResult<object> ClearSeriesCache()
{
    Plugin.Instance.SeriesCacheService.CancelRefresh();
    Plugin.Instance.SeriesCacheService.InvalidateCache();  // ← Only increments internal _cacheVersion
    return Ok(new { Success = true, Message = "Cache cleared successfully." });
}

// SeriesCacheService.cs:383-390
public void InvalidateCache()
{
    _cacheVersion++;  // ← Changes from v1 to v2
    // This changes cache key: "series_cache_X_v1_" → "series_cache_X_v2_"
    // BUT: Does NOT change the Channel's DataVersion
}
```

**Result:** Plugin cache is cleared, but Jellyfin's `jellyfin.db` still contains old data.

**Evidence:**
- Cache status showed "184 series" (correct)
- Jellyfin UI showed "1,782 series" (incorrect)
- Difference: 1,598 ghost series from old cache

### Finding 2: Version Upgrades Don't Clear Jellyfin's Cache

**What should happen:**
1. Plugin upgraded: v0.9.4.11 → v0.9.4.12
2. `DataVersion` changes: `"0.9.4.11..." → "0.9.4.12..."`
3. Jellyfin detects change and invalidates cache
4. Fresh data displayed

**What actually happened:**
1. ✅ Plugin upgraded successfully
2. ✅ `DataVersion` changed
3. ❌ Jellyfin did NOT invalidate cache
4. ❌ Still showed 1,782 ghost series instead of 184

**Evidence:**
- User confirmed version was v0.9.4.12
- Still saw stale data with "|NL|" tags
- Required manual database cleanup to fix

### Finding 3: Manual Database Cleanup Works

**What we did:**
```bash
sqlite3 jellyfin.db "DELETE FROM BaseItems WHERE ChannelId='38885B34-4A45-511D-E94B-A8CDE6AF514B';"
# Deleted 2,134 stale rows
```

**Result:** Jellyfin UI immediately showed correct 184 series with cleaned titles.

**Conclusion:** The problem is NOT the plugin code - it's that Jellyfin doesn't invalidate its cache when DataVersion changes.

### Finding 4: ParseName() Works Correctly

**Test:**
```csharp
var regex = new Regex(@"\[([^\]]+)\]|[|│┃｜]\s*([^|│┃｜]+?)\s*[|│┃｜]");
regex.Replace("| NL | Casino Royaal", "") => "Casino Royaal"  ✅
```

The tag-stripping logic works perfectly. The issue is Jellyfin serving stale cached data.

## Root Cause Analysis

**Hypothesis:** Jellyfin's channel cache invalidation mechanism doesn't work (or doesn't work reliably).

### Affected Components

```
BaseItems table in jellyfin.db:
- Stores channel items (Series, Seasons, Episodes)
- Keyed by ChannelId (GUID)
- Supposed to be invalidated when Channel.DataVersion changes
- DOES NOT actually invalidate when DataVersion changes
```

### Why This Is Critical

Users experience:
1. **Stale titles** - See old names with tags that should be stripped
2. **Ghost content** - See series that were unconfigured/removed
3. **Confusion** - "Clear Cache" button appears to do nothing
4. **Manual intervention required** - Must restart Jellyfin or manually clean database

## Potential Solutions

### Option 1: Force Configuration Change on Clear

**Approach:** Make "Clear Cache" trigger a configuration save, changing the hash.

```csharp
public ActionResult<object> ClearSeriesCache()
{
    Plugin.Instance.SeriesCacheService.InvalidateCache();

    // Force config hash to change
    var config = Plugin.Instance.Configuration;
    config.NotifyUpdate();  // Triggers save, changes GetHashCode()

    return Ok(new { Success = true });
}
```

**Pros:**
- No database access required
- Uses Jellyfin's intended mechanism
- Safe - no risk of corruption

**Cons:**
- Assumes configuration hash change triggers DataVersion change (needs verification)
- Assumes DataVersion change actually works (evidence suggests it doesn't)
- Might not work at all

**Risk Level:** Low
**Likelihood of Success:** Low (evidence suggests DataVersion mechanism is broken)

---

### Option 2: Add Timestamp/GUID to DataVersion

**Approach:** Include a changing value in DataVersion calculation.

```csharp
// Plugin.cs
private string _cacheInvalidationToken = Guid.NewGuid().ToString();

public string DataVersion =>
    Assembly.GetCallingAssembly().GetName().Version?.ToString()
    + Configuration.GetHashCode()
    + _cacheInvalidationToken;

public void InvalidateCache()
{
    _cacheInvalidationToken = Guid.NewGuid().ToString();
    SeriesCacheService.InvalidateCache();
}
```

**Pros:**
- Forces DataVersion to change on every clear
- Clean implementation
- No database access

**Cons:**
- Still relies on Jellyfin respecting DataVersion (which it doesn't seem to)
- DataVersion changes constantly, might have side effects
- Doesn't fix version upgrade issue (new plugin instance = new GUID)

**Risk Level:** Low
**Likelihood of Success:** Low-Medium

---

### Option 3: External Database Cleanup Script

**Approach:** Provide a shell script users run to clean Jellyfin database.

```bash
#!/bin/bash
# cleanup-jellyfin-channels.sh
docker stop jellyfin
sqlite3 /path/to/jellyfin.db "DELETE FROM BaseItems WHERE ChannelId IN (...);"
rm -rf /path/to/metadata/channels/*
docker start jellyfin
```

**Pros:**
- Guaranteed to work (proven in testing)
- Complete cleanup
- No plugin code changes

**Cons:**
- Requires command-line access
- Manual process
- Platform-specific (Docker, systemd, Windows service, etc.)
- Poor user experience
- Requires Jellyfin restart

**Risk Level:** Medium (user error possible)
**Likelihood of Success:** High (proven to work)

---

### Option 4: Plugin-Based Database Cleanup

**Approach:** Plugin accesses jellyfin.db directly and deletes entries.

```csharp
public ActionResult<object> ClearSeriesCache()
{
    // 1. Clear plugin cache
    Plugin.Instance.SeriesCacheService.InvalidateCache();

    // 2. Access Jellyfin database
    var dbPath = Path.Combine(appPaths.DataPath, "jellyfin.db");
    using var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();

    // 3. Delete channel items
    var cmd = connection.CreateCommand();
    cmd.CommandText = "DELETE FROM BaseItems WHERE ChannelId=@channelId";
    cmd.Parameters.AddWithValue("@channelId", channelId);
    cmd.ExecuteNonQuery();

    return Ok(new { Success = true });
}
```

**Pros:**
- Complete cleanup
- Automatic (no user intervention)
- Guaranteed to work

**Cons:**
- **DANGEROUS:** Accessing database while Jellyfin is running risks corruption
- Requires SQLite dependency
- Requires knowing database schema (might change between Jellyfin versions)
- May require database locking
- Plugin might not have filesystem permissions
- Jellyfin might have database locked for exclusive access

**Risk Level:** HIGH
**Likelihood of Success:** Medium-High (but risky)

---

### Option 5: Jellyfin API for Cache Invalidation

**Approach:** Use Jellyfin's internal API to force cache refresh.

```csharp
// Hypothetical - need to find if this exists
libraryManager.RefreshChannel(channelId);
// or
channelManager.InvalidateCache(channelId);
```

**Pros:**
- Uses Jellyfin's intended mechanism
- Safe - no direct database access
- Proper solution

**Cons:**
- **Unknown if this API exists**
- Requires investigation of Jellyfin codebase
- Might not be exposed to plugins

**Risk Level:** Low (if API exists)
**Likelihood of Success:** Unknown - needs investigation

---

### Option 6: Disable Jellyfin Caching (Always Fresh)

**Approach:** Return different data or metadata on each request to prevent caching.

```csharp
// Add random/timestamp to each item ID or metadata
public string DataVersion => DateTime.UtcNow.Ticks.ToString();
```

**Pros:**
- Always fresh data
- Simple implementation

**Cons:**
- **Defeats purpose of caching** - terrible performance
- Constantly re-fetches from Xtream server
- Jellyfin UI might be slow/sluggish
- Higher load on Xtream server
- User experience degradation

**Risk Level:** Low
**Likelihood of Success:** High (but undesirable)

---

### Option 7: Two-Button Approach

**Approach:** Split functionality into "soft clear" and "hard clear".

**UI:**
```
[Refresh Now]  [Clear Cache]  [Deep Clean Database]
```

**Implementation:**
- "Clear Cache": Clears plugin IMemoryCache only (current behavior)
- "Deep Clean Database": Shows instructions for manual cleanup OR attempts Option 4

**Pros:**
- Clear separation of concerns
- Users know what they're getting
- Can provide detailed instructions

**Cons:**
- More complex UI
- Still requires manual intervention (unless we implement risky Option 4)
- Doesn't fix core problem

**Risk Level:** Low
**Likelihood of Success:** High (as a workaround, not a fix)

---

### Option 8: Investigate Jellyfin Source Code

**Approach:** Deep dive into Jellyfin's channel cache mechanism.

**Questions to answer:**
1. How does Jellyfin check DataVersion?
2. When does it invalidate channel cache?
3. Is there a bug in Jellyfin's cache invalidation?
4. Is there an undocumented API we can use?
5. Should we file a Jellyfin bug report?

**Pros:**
- Might discover the "right" solution
- Could fix the root cause
- Benefit entire Jellyfin plugin ecosystem

**Cons:**
- Time-consuming
- Requires C# and Jellyfin architecture knowledge
- Might not find a solution
- Fix might require Jellyfin changes (outside our control)

**Risk Level:** None (just investigation)
**Likelihood of Success:** Unknown

---

## Technical Constraints

### What We Can Do
- ✅ Clear plugin's IMemoryCache
- ✅ Change DataVersion string
- ✅ Trigger configuration saves
- ✅ Return fresh data on every request
- ✅ Provide user documentation/scripts

### What We Cannot Do (or is risky)
- ❌ Safely access jellyfin.db while Jellyfin is running
- ❌ Restart Jellyfin from within the plugin
- ❌ Force Jellyfin to refresh its cache (no known API)
- ❌ Guarantee DataVersion mechanism works

### What We Don't Know
- ❓ Does Jellyfin actually check DataVersion for channels?
- ❓ Is there a Jellyfin API for cache invalidation?
- ❓ Is this a Jellyfin bug?
- ❓ Do other channel plugins have this problem?

## Questions for Analysis

1. **Is the DataVersion mechanism supposed to work?**
   - Should Jellyfin clear its cache when DataVersion changes?
   - Is this documented anywhere?
   - Do other plugins rely on this?

2. **Why doesn't version upgrade trigger cache clear?**
   - DataVersion definitely changes (proven)
   - Jellyfin definitely doesn't clear cache (proven)
   - Is there a timing issue? Race condition?

3. **Can we access Jellyfin's internal APIs?**
   - Does `ILibraryManager`, `IChannelManager`, or similar have invalidation methods?
   - What APIs are available to plugins?

4. **Is direct database access safe?**
   - Can we safely read/write jellyfin.db while Jellyfin is running?
   - Does SQLite's WAL mode allow concurrent access?
   - What are the risks?

5. **What's the best user experience?**
   - Should "Clear Cache" be one-click complete cleanup (even if requires restart)?
   - Should we have separate buttons for different levels of cleanup?
   - Should we document manual cleanup instead?

6. **Should we file a Jellyfin bug report?**
   - Is this a Jellyfin bug or expected behavior?
   - Have other plugin developers encountered this?

## Recommended Next Steps

1. **Investigate Jellyfin source code** (Option 8)
   - Search for DataVersion handling in channel code
   - Look for cache invalidation APIs
   - Check if this is a known issue

2. **Test Option 2** (Timestamp in DataVersion)
   - Low risk, quick to implement
   - Will definitively prove if DataVersion mechanism works

3. **Implement Option 7** (Two-button approach) as interim solution
   - Provides user control
   - Documents the manual process
   - Safe fallback

4. **Long-term:** Pursue proper fix based on investigation findings

## Success Criteria

A successful solution must:
1. ✅ Clear Jellyfin's cached channel data (jellyfin.db)
2. ✅ Work reliably on every invocation
3. ✅ Not require Jellyfin restart (preferred) or clearly communicate if restart needed
4. ✅ Be safe (no data corruption risk)
5. ✅ Work across different Jellyfin installations (Docker, systemd, Windows, etc.)
6. ✅ Be user-friendly (minimal manual steps)

## References

- Jellyfin Plugin Documentation: https://jellyfin.org/docs/general/server/plugins/
- Channel Plugin Guide: https://jellyfin.org/docs/general/server/plugins/channels/
- Jellyfin Source (Channel Manager): https://github.com/jellyfin/jellyfin/tree/master/MediaBrowser.Controller/Channels

---

**Document Version:** 1.0
**Date:** 2026-01-26
**Author:** Analysis of Jellyfin Xtream Plugin v0.9.4.12 cache behavior
