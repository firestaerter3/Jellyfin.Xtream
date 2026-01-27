# Clear Cache DB Cleanup Feature - Architecture

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Related:** [REQUIREMENTS.md](./REQUIREMENTS.md), [IMPLEMENTATION.md](./IMPLEMENTATION.md)

---

## 1. Overview

The Clear Cache DB Cleanup feature ensures that clicking "Clear Cache" in the plugin settings performs a complete cleanup of both the plugin's in-memory cache and Jellyfin's database. This prevents orphaned items from appearing in the Jellyfin UI after the cache is cleared.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Plugin Configuration UI                       │
│                      (Clear Cache Button)                        │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTP POST
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│                     XtreamController                             │
│                   ClearSeriesCache() Endpoint                    │
│                                                                  │
│  1. Check if refresh running (GetStatus)                         │
│  2. Cancel refresh if running (CancelRefresh)                    │
│  3. Invalidate cache (InvalidateCache)                           │
│  4. Trigger Jellyfin refresh (CancelIfRunningAndQueue)           │
│  5. Return JSON response                                         │
└───────────────────────────┬─────────────────────────────────────┘
                            │
          ┌─────────────────┼─────────────────┐
          ↓                 ↓                 ↓
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ SeriesCacheService│ │   TaskService    │ │ Jellyfin LiveTv  │
│                   │ │                   │ │     System       │
│ - GetStatus()     │ │ - CancelIfRunning │ │                   │
│ - CancelRefresh() │ │   AndQueue()      │ │ - RefreshChannels │
│ - InvalidateCache()│ │                   │ │   ScheduledTask   │
└─────────────────┘ └─────────────────┘ └─────────────────┘
                                                  │
                                                  ↓
                                        ┌─────────────────┐
                                        │  jellyfin.db    │
                                        │ (Channel Items) │
                                        └─────────────────┘
```

---

## 2. Components

### Component 1: XtreamController.ClearSeriesCache()
**File:** `Api/XtreamController.cs` (lines 247-281)

**Responsibility:** API endpoint for clearing cache with Jellyfin DB cleanup

**Key Logic:**
```csharp
[HttpPost("SeriesCacheClear")]
public ActionResult<object> ClearSeriesCache()
{
    // 1. Check status
    var (isRefreshing, _, _, _, _) = Plugin.Instance.SeriesCacheService.GetStatus();

    string message = "Cache cleared successfully.";

    // 2. Cancel if running
    if (isRefreshing)
    {
        Plugin.Instance.SeriesCacheService.CancelRefresh();
        message = "Cache cleared. Refresh was cancelled.";
    }

    // 3. Invalidate cache
    Plugin.Instance.SeriesCacheService.InvalidateCache();

    // 4. Trigger Jellyfin cleanup
    try
    {
        Plugin.Instance.TaskService.CancelIfRunningAndQueue(
            "Jellyfin.LiveTv",
            "Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask");
        message += " Jellyfin channel refresh triggered to clean up jellyfin.db.";
    }
    catch
    {
        message += " Warning: Could not trigger Jellyfin cleanup.";
    }

    return Ok(new { Success = true, Message = message });
}
```

**Design Decisions:**
- **Order matters:** Cancel → Invalidate → Trigger
- **Non-blocking:** Returns immediately, Jellyfin refresh runs async
- **Graceful degradation:** Failures in steps don't prevent others

---

### Component 2: SeriesCacheService.InvalidateCache()
**File:** `Service/SeriesCacheService.cs` (lines 471-478)

**Responsibility:** Invalidate all cached data by incrementing version

**Implementation:**
```csharp
public void InvalidateCache()
{
    _cacheVersion++;
    _currentProgress = 0.0;
    _currentStatus = "Cache invalidated";
    _lastRefreshComplete = null;
    _logger?.LogInformation("Cache invalidated (version incremented to {Version})", _cacheVersion);
}
```

**Why Version Increment?**
- All cache keys include version: `series_cache_{dataVersion}_v{_cacheVersion}_`
- Incrementing version makes old keys inaccessible
- Old entries expire naturally (24-hour TTL)
- No need to enumerate/delete entries (IMemoryCache limitation)

---

### Component 3: SeriesCacheService.CancelRefresh()
**File:** `Service/SeriesCacheService.cs` (lines 457-465)

**Responsibility:** Cancel any running cache refresh operation

**Implementation:**
```csharp
public void CancelRefresh()
{
    if (_isRefreshing && _refreshCancellationTokenSource != null)
    {
        _logger?.LogInformation("Cancelling cache refresh...");
        _currentStatus = "Cancelling...";
        _refreshCancellationTokenSource.Cancel();
    }
}
```

**Cancellation Flow:**
1. CancelRefresh() calls `_refreshCancellationTokenSource.Cancel()`
2. RefreshCacheAsync() checks `_refreshCancellationTokenSource.Token` periodically
3. ThrowIfCancellationRequested() throws OperationCanceledException
4. RefreshCacheAsync() catch block sets status to "Cancelled"

---

### Component 4: TaskService.CancelIfRunningAndQueue()
**File:** `Service/TaskService.cs` (part of Plugin services)

**Responsibility:** Cancel and re-queue Jellyfin scheduled tasks

**Purpose in Clear Cache:**
- Triggers Jellyfin's `RefreshChannelsScheduledTask`
- This task calls `GetChannelItems()` on all channel plugins
- Plugin returns empty/fresh data (cache invalidated)
- Jellyfin updates `jellyfin.db` to match plugin data
- Orphaned items removed from DB

---

## 3. Data Flow

### Scenario: Clear Cache (No Active Refresh)

```
1. User clicks "Clear Cache" button
   │
2. HTTP POST → /Xtream/SeriesCacheClear
   │
3. Controller checks: isRefreshing = false
   │
4. Controller calls: InvalidateCache()
   │  └─ _cacheVersion++ (e.g., 5 → 6)
   │  └─ Cache keys change: "series_cache_v5_" → "series_cache_v6_"
   │
5. Controller calls: TaskService.CancelIfRunningAndQueue()
   │  └─ Triggers: RefreshChannelsScheduledTask
   │
6. Return response: { Success: true, Message: "..." }
   │
7. (Background) Jellyfin executes RefreshChannelsScheduledTask
   │  └─ Calls GetChannelItems() on SeriesChannel
   │  └─ Plugin returns empty results (no cache for v6)
   │  └─ Jellyfin updates jellyfin.db
   │  └─ Orphaned items removed
   │
8. User refreshes browser → sees clean library
```

### Scenario: Clear Cache (During Active Refresh)

```
1. User clicks "Clear Cache" while refresh running
   │
2. HTTP POST → /Xtream/SeriesCacheClear
   │
3. Controller checks: isRefreshing = true
   │
4. Controller calls: CancelRefresh()
   │  └─ Sets _refreshCancellationTokenSource.Cancel()
   │  └─ RefreshCacheAsync() throws OperationCanceledException
   │  └─ Status: "Cancelled"
   │
5. Controller calls: InvalidateCache()
   │  └─ _cacheVersion++
   │
6. Controller calls: TaskService.CancelIfRunningAndQueue()
   │
7. Return response: { Success: true, Message: "Cache cleared. Refresh was cancelled. ..." }
   │
8. (Background) Jellyfin refresh proceeds as above
```

---

## 4. Design Decisions

### Decision 1: Order of Operations (Cancel → Invalidate → Trigger)

**Context:** What order should cache clear operations execute?

**Decision:** Cancel refresh first, then invalidate, then trigger Jellyfin

**Rationale:**
- **Cancel first:** Prevents race condition where refresh writes to old cache
- **Invalidate second:** New cache version prevents stale reads
- **Trigger last:** Jellyfin refresh uses new (empty) cache state

**Alternative considered:** Invalidate first, then cancel
- **Rejected:** Could cause brief window where refresh writes to new version

---

### Decision 2: Asynchronous Jellyfin Refresh

**Context:** Should we wait for Jellyfin refresh to complete?

**Decision:** Return immediately, let Jellyfin refresh run async

**Rationale:**
- **User experience:** Immediate feedback (no waiting)
- **Jellyfin pattern:** Scheduled tasks run asynchronously
- **Reliability:** No timeout concerns

**Trade-off:**
- User might refresh browser before Jellyfin finishes
- Mitigation: Message indicates refresh was "triggered" (not completed)

---

### Decision 3: Graceful Degradation on Errors

**Context:** What if TaskService trigger fails?

**Decision:** Report warning but still succeed

**Rationale:**
- Cache was cleared (primary goal)
- Jellyfin trigger is enhancement (secondary)
- User can manually refresh if needed

**Alternative considered:** Fail entire operation
- **Rejected:** Would require retry, worse UX

---

### Decision 4: Version-Based Cache Invalidation

**Context:** How to invalidate cache without enumerating keys?

**Decision:** Increment version number in cache key prefix

**Rationale:**
- `IMemoryCache` doesn't support key enumeration
- Version increment makes all old keys orphaned
- Old entries expire naturally (24-hour TTL)
- Simple, atomic operation

**Alternative considered:** Track all keys and delete individually
- **Rejected:** Complex, error-prone, memory overhead

---

## 5. Cache Key Strategy

### Key Format
```
series_cache_{CacheDataVersion}_v{_cacheVersion}_{itemType}_{itemId}
```

### Examples
```
series_cache_1234567890_v5_categories
series_cache_1234567890_v5_serieslist_42
series_cache_1234567890_v5_seriesinfo_100
series_cache_1234567890_v5_episodes_100_2
```

### Invalidation Effect
```
Before: _cacheVersion = 5
  - All lookups use prefix "...v5_"
  - Cache populated with v5 entries

InvalidateCache() called
  - _cacheVersion = 6

After: _cacheVersion = 6
  - All lookups use prefix "...v6_"
  - v5 entries unreachable (orphaned)
  - v5 entries expire after 24 hours
```

---

## 6. Jellyfin Integration

### RefreshChannelsScheduledTask

**Task ID:** `Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask`
**Assembly:** `Jellyfin.LiveTv`

**What it does:**
1. Iterates all channel plugins
2. Calls `GetChannelItems()` on each plugin
3. Updates `jellyfin.db` with returned items
4. Removes items not returned (orphan cleanup)

### Trigger Mechanism
```csharp
Plugin.Instance.TaskService.CancelIfRunningAndQueue(
    "Jellyfin.LiveTv",
    "Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask");
```

- **CancelIfRunning:** If task already running, cancel it
- **Queue:** Add to Jellyfin task queue for immediate execution

---

## 7. Error Handling

### Error Scenarios

| Step | Error | Handling | Impact |
|------|-------|----------|--------|
| GetStatus | N/A | Always succeeds | None |
| CancelRefresh | Token already cancelled | Safe, no-op | None |
| InvalidateCache | Increment overflow | Extremely rare | Reset to 0 |
| CancelIfRunningAndQueue | TaskService null | Catch, warn | Jellyfin not cleaned |
| CancelIfRunningAndQueue | Task not found | Catch, warn | Jellyfin not cleaned |
| CancelIfRunningAndQueue | Jellyfin unavailable | Catch, warn | Jellyfin not cleaned |

### Error Recovery

**User workaround if Jellyfin cleanup fails:**
1. Go to Jellyfin Dashboard → Scheduled Tasks
2. Find "Refresh Channels" task
3. Click "Run" to manually trigger
4. Or: Restart Jellyfin server

---

## 8. Performance Characteristics

### Time Complexity
- **GetStatus:** O(1) - read flags
- **CancelRefresh:** O(1) - signal cancellation
- **InvalidateCache:** O(1) - increment integer
- **CancelIfRunningAndQueue:** O(1) - queue task

**Total:** O(1) - constant time

### Response Time
- **Typical:** < 100ms
- **Maximum:** < 500ms (network latency to Jellyfin)

### Memory Impact
- **Immediate:** None (old cache entries unreachable)
- **Eventual:** Old entries freed after 24-hour TTL

---

## 9. Security Considerations

- **Authentication:** Endpoint requires admin elevation
- **Authorization:** Uses `[Authorize(Policy = "RequiresElevation")]`
- **No data exposure:** Only clears cache, doesn't return sensitive data
- **No injection:** No user input in cache operations

---

## 10. Future Enhancements

### Potential Improvements

1. **Selective Clear**
   - Clear only specific categories
   - Clear only one series
   - Requires key tracking

2. **Confirmation Dialog**
   - "Are you sure?" before clearing
   - Frontend change only

3. **Progress Feedback**
   - Show Jellyfin refresh progress
   - Requires polling mechanism

4. **Clear VOD Cache**
   - When VOD caching implemented
   - Same pattern as series

---

## 11. References

- [REQUIREMENTS.md](./REQUIREMENTS.md) - Feature requirements
- [IMPLEMENTATION.md](./IMPLEMENTATION.md) - Implementation details
- [TEST_PLAN.md](./TEST_PLAN.md) - Test cases
- [Feature 04 - Eager Caching](../04-eager-caching/ARCHITECTURE.md) - Related caching architecture
- Jellyfin Scheduled Tasks: https://jellyfin.org/docs/general/server/tasks
