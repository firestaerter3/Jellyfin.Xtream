# Clear Cache DB Cleanup Feature - Requirements

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Implemented in:** v0.9.5.2
- **Related:** [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## 1. Executive Summary

### 1.1 Problem Statement

**User Pain Points:**
- **Stale Data in Jellyfin**: When clearing the plugin cache, orphaned items remained in Jellyfin's database (`jellyfin.db`)
  - Example: Series deleted from Xtream provider still appeared in Jellyfin UI
- **Confusion**: Users expected "Clear Cache" to remove all cached content, but Jellyfin-side data persisted
- **Manual Workaround**: Users had to manually refresh channels or restart Jellyfin to see changes

**Specific Scenario:**
> "I clicked 'Clear Cache' in the plugin settings because my provider removed some series.
> The plugin cache was cleared, but when I browsed the Series channel, the old series
> were still there! I had to restart Jellyfin to get them removed."

### 1.2 Solution

Enhance the "Clear Cache" button to:
1. Invalidate the plugin's in-memory cache
2. Cancel any running cache refresh operation
3. Trigger Jellyfin's channel refresh task to clean up `jellyfin.db`

**After Fix:**
- Clear Cache invalidates plugin cache (existing)
- Clear Cache cancels running refresh (new)
- Clear Cache triggers Jellyfin DB cleanup (new)
- Orphaned items removed from Jellyfin UI

### 1.3 Key Benefits

1. **Complete Cleanup**: Single button clears both plugin cache and Jellyfin DB
2. **No Manual Steps**: No need to restart Jellyfin or manually refresh channels
3. **Immediate Effect**: Changes visible immediately in Jellyfin UI
4. **Safe Operation**: Cancels running refresh gracefully before clearing

---

## 2. User Stories

### US-1: Complete Cache Clear
**As a** Jellyfin administrator
**I want to** clear all cached data with a single button click
**So that** I can see fresh data from my Xtream provider without manual steps

**Acceptance Criteria:**
- When Clear Cache is clicked, plugin cache is invalidated
- When Clear Cache is clicked, Jellyfin refreshes channel items
- After refresh, only current provider content appears in Jellyfin
- No Jellyfin restart required

---

### US-2: Cancel Running Refresh
**As a** Jellyfin administrator
**I want to** cancel a running cache refresh when I click Clear Cache
**So that** I don't have stale data mixed with fresh data

**Acceptance Criteria:**
- If cache refresh is running, it is cancelled before clearing
- User is informed that refresh was cancelled
- No race conditions between clear and refresh operations

---

### US-3: User Feedback
**As a** Jellyfin administrator
**I want to** see confirmation that cache was cleared successfully
**So that** I know the operation completed and what actions were taken

**Acceptance Criteria:**
- Success message indicates cache was cleared
- If refresh was cancelled, message mentions it
- If Jellyfin cleanup was triggered, message mentions it
- If any step fails, message includes warning

---

## 3. Functional Requirements

### FR-1: Cache Invalidation

**ID:** FR-1
**Priority:** High
**Description:** Invalidate all cached series data by incrementing cache version.

**Acceptance Criteria:**
- [ ] `InvalidateCache()` method increments `_cacheVersion`
- [ ] Old cache entries no longer accessible via `CachePrefix`
- [ ] Status reset to "Cache invalidated"
- [ ] `_lastRefreshComplete` set to null

---

### FR-2: Cancel Running Refresh

**ID:** FR-2
**Priority:** High
**Description:** If cache refresh is in progress, cancel it gracefully before clearing.

**Acceptance Criteria:**
- [ ] Check `isRefreshing` flag before clearing
- [ ] Call `CancelRefresh()` if refresh running
- [ ] Wait for cancellation to be signaled
- [ ] Proceed with cache clear after cancellation

---

### FR-3: Trigger Jellyfin DB Cleanup

**ID:** FR-3
**Priority:** High
**Description:** After clearing cache, trigger Jellyfin to refresh channel items.

**Acceptance Criteria:**
- [ ] Call `TaskService.CancelIfRunningAndQueue()` to trigger refresh
- [ ] Jellyfin calls `GetChannelItems()` which returns empty/fresh data
- [ ] Jellyfin DB updates to remove orphaned items
- [ ] If trigger fails, show warning but don't fail entire operation

---

### FR-4: User Feedback

**ID:** FR-4
**Priority:** Medium
**Description:** Provide informative feedback about the clear operation.

**Acceptance Criteria:**
- [ ] Return JSON response with `Success` and `Message` fields
- [ ] Message describes what happened:
  - "Cache cleared successfully."
  - "Cache cleared. Refresh was cancelled."
  - "Jellyfin channel refresh triggered to clean up jellyfin.db."
  - "Warning: Could not trigger Jellyfin cleanup."
- [ ] Response is immediate (no blocking on refresh completion)

---

## 4. Non-Functional Requirements

### NFR-1: Immediate Response

**ID:** NFR-1
**Description:** Clear Cache operation should return immediately.

**Acceptance Criteria:**
- [ ] API response within 500ms
- [ ] Jellyfin refresh runs asynchronously (background)
- [ ] UI remains responsive during operation

---

### NFR-2: No Data Loss

**ID:** NFR-2
**Description:** Clear operation should not corrupt data or cause inconsistent state.

**Acceptance Criteria:**
- [ ] Cache version increment is atomic
- [ ] Cancellation is graceful (no partial writes)
- [ ] Jellyfin refresh eventually consistent

---

### NFR-3: Error Handling

**ID:** NFR-3
**Description:** Errors in one step should not prevent other steps.

**Acceptance Criteria:**
- [ ] If cancel fails, still proceed with clear
- [ ] If Jellyfin trigger fails, still report cache clear success
- [ ] Errors logged for debugging
- [ ] User sees warning but not failure

---

## 5. API Requirements

### API Endpoint

**Endpoint:** `POST /Xtream/SeriesCacheClear`
**Authentication:** Requires elevation (admin)
**Request Body:** None
**Response:**
```json
{
  "Success": true,
  "Message": "Cache cleared successfully. Jellyfin channel refresh triggered to clean up jellyfin.db."
}
```

### Response Variants

| Scenario | Success | Message |
|----------|---------|---------|
| Normal clear | true | "Cache cleared successfully. Jellyfin channel refresh triggered to clean up jellyfin.db." |
| Clear during refresh | true | "Cache cleared. Refresh was cancelled. Jellyfin channel refresh triggered to clean up jellyfin.db." |
| Jellyfin trigger failed | true | "Cache cleared successfully. Warning: Could not trigger Jellyfin cleanup." |

---

## 6. Error Handling Requirements

### EH-1: Cancellation Scenarios

| Scenario | Behavior | User Impact |
|----------|----------|-------------|
| No refresh running | Skip cancellation | Normal clear |
| Refresh running | Cancel refresh | Clear delayed by cancellation |
| Cancellation fails | Log error, continue | Clear proceeds |

### EH-2: Jellyfin Integration

| Error | Handling | User Impact |
|-------|----------|-------------|
| TaskService not available | Catch exception | Warning in message |
| Refresh task fails | Log error | Warning in message |
| Jellyfin unavailable | Catch exception | Warning in message |

---

## 7. Dependencies

### Dependency 1: SeriesCacheService
- **Location:** `Service/SeriesCacheService.cs`
- **Methods Used:**
  - `GetStatus()` - Check if refresh running
  - `CancelRefresh()` - Cancel running refresh
  - `InvalidateCache()` - Increment cache version
- **Impact:** Core cache management

### Dependency 2: TaskService
- **Location:** `Plugin.TaskService`
- **Methods Used:**
  - `CancelIfRunningAndQueue()` - Trigger Jellyfin refresh
- **Impact:** Jellyfin integration for DB cleanup

### Dependency 3: Feature 04 (Eager Caching)
- **Relationship:** Clear Cache relies on cache invalidation mechanism
- **Impact:** Must be compatible with eager caching refresh

---

## 8. Success Criteria

### Definition of Done

**Feature Complete When:**
- [ ] Clear Cache invalidates plugin cache
- [ ] Clear Cache cancels running refresh
- [ ] Clear Cache triggers Jellyfin DB cleanup
- [ ] User feedback includes all actions taken
- [ ] Error handling gracefully degrades
- [ ] Documentation complete

### User Acceptance Criteria

**Users Should Be Able To:**
- [ ] Click Clear Cache and see immediate response
- [ ] See orphaned items removed from Jellyfin UI
- [ ] Know what actions were performed (via message)
- [ ] Not need to restart Jellyfin after clearing

---

## 9. Out of Scope

**Explicitly NOT Included:**
- Selective cache clear (e.g., clear only one category)
- VOD cache clear (VOD not cached)
- User confirmation dialog before clear
- Automatic refresh after clear
- Clear cache on schedule

---

## 10. Glossary

| Term | Definition |
|------|------------|
| **Plugin Cache** | In-memory cache managed by `SeriesCacheService` |
| **Cache Version** | Integer incremented to invalidate all cache entries |
| **Jellyfin DB** | SQLite database (`jellyfin.db`) storing channel items |
| **Orphaned Items** | Entries in Jellyfin DB no longer in Xtream provider |
| **Channel Refresh** | Jellyfin task that re-fetches items from plugin |

---

## 11. References

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Clear cache architecture and data flow
- [IMPLEMENTATION.md](./IMPLEMENTATION.md) - Implementation details and code
- [TEST_PLAN.md](./TEST_PLAN.md) - Manual test cases
- [Feature 04 - Eager Caching](../04-eager-caching/REQUIREMENTS.md) - Related caching feature
