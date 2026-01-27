# Eager Caching Feature - Requirements Document

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Related:** [CACHING_ARCHITECTURE.md](./CACHING_ARCHITECTURE.md)
- **Implemented in:** v0.9.5.0

---

## 1. Executive Summary

### 1.1 Problem Statement

Without eager caching, users experience slow browsing because:
- Each navigation (series → season → episodes) triggers API calls to the Xtream provider
- API calls take 5-10 seconds each
- Users must wait while data loads on every click
- Poor user experience, especially with large libraries (100+ series)

### 1.2 Solution

Implement **eager loading** that pre-populates Jellyfin's database (`jellyfin.db`) with all series, seasons, and episodes upfront, enabling instant browsing.

### 1.3 Key Insight

The plugin cache serves as a **staging buffer** between the slow Xtream API and Jellyfin's database:

```
Xtream API (slow) → Plugin Cache (RAM) → Jellyfin DB (persistent) → User browsing (instant)
```

---

## 2. Functional Requirements

### FR-1: Batch API Data Fetching

**ID:** FR-1
**Priority:** High
**Description:** The system shall fetch all series data from the Xtream API in a single batch operation.

**Acceptance Criteria:**
- [ ] Fetch all categories from API
- [ ] Fetch all series for each selected category
- [ ] Fetch all seasons for each series
- [ ] Fetch all episodes for each season
- [ ] Store all fetched data in plugin IMemoryCache
- [ ] Log progress during fetch operation

**Rationale:** Batching API calls upfront avoids rate limiting and allows serving Jellyfin quickly from cache.

---

### FR-2: Automatic Jellyfin Database Population

**ID:** FR-2
**Priority:** High
**Description:** After cache refresh completes, the system shall automatically trigger Jellyfin to populate its database from the cache.

**Acceptance Criteria:**
- [ ] Trigger `Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask` after cache refresh
- [ ] Log when Jellyfin refresh is triggered
- [ ] Handle errors gracefully if trigger fails
- [ ] Jellyfin calls `GetChannelItems()` for all items
- [ ] Plugin serves requests from cache (not API)
- [ ] `jellyfin.db` contains all series/seasons/episodes after completion

**Rationale:** This is the core of eager loading - pushing cached data into Jellyfin's persistent storage.

---

### FR-3: Cache Refresh Triggers

**ID:** FR-3
**Priority:** High
**Description:** The system shall support multiple cache refresh triggers.

**Acceptance Criteria:**
- [ ] Refresh on Jellyfin startup (if caching enabled and credentials configured)
- [ ] Refresh on scheduled interval (configurable, default 60 minutes)
- [ ] Refresh on manual "Refresh Now" button click
- [ ] Refresh on configuration save (when cache-relevant settings change)

---

### FR-4: Cache Data Structure

**ID:** FR-4
**Priority:** Medium
**Description:** The system shall cache data with versioned keys to support invalidation.

**Acceptance Criteria:**
- [ ] Cache key format: `series_cache_{CacheDataVersion}_v{CacheVersion}_{type}_{id}`
- [ ] Cache categories with key: `{prefix}categories`
- [ ] Cache series lists with key: `{prefix}serieslist_{categoryId}`
- [ ] Cache series info with key: `{prefix}seriesinfo_{seriesId}`
- [ ] Cache seasons with key: `{prefix}season_{seriesId}_{seasonId}`
- [ ] Cache episodes with key: `{prefix}episodes_{seriesId}_{seasonId}`
- [ ] 24-hour expiration as safety mechanism

---

### FR-5: Cache Invalidation

**ID:** FR-5
**Priority:** Medium
**Description:** The system shall support cache invalidation without memory cleanup.

**Acceptance Criteria:**
- [ ] "Clear Cache" button increments `_cacheVersion`
- [ ] Old cache keys become inaccessible (wrong version)
- [ ] Old data remains in memory until GC or expiration
- [ ] New cache refresh uses new version number
- [ ] Cancel running refresh when clearing cache

---

### FR-6: Progress Reporting

**ID:** FR-6
**Priority:** Low
**Description:** The system shall report cache refresh progress to the UI.

**Acceptance Criteria:**
- [ ] Report progress as percentage (0-100%)
- [ ] Report current status message
- [ ] Report start time and completion time
- [ ] Report whether refresh is in progress
- [ ] UI displays progress bar and status text

---

## 3. Non-Functional Requirements

### NFR-1: Performance

**ID:** NFR-1
**Description:** Cache refresh and Jellyfin population shall complete within acceptable time.

**Acceptance Criteria:**
- [ ] Cache refresh: < 30 minutes for 200 series
- [ ] Jellyfin DB population: < 10 minutes for 200 series
- [ ] Total eager loading time: < 40 minutes for full refresh
- [ ] Individual `GetChannelItems()` call from cache: < 100ms

---

### NFR-2: Memory Usage

**ID:** NFR-2
**Description:** Cache memory usage shall be proportional to data size.

**Acceptance Criteria:**
- [ ] Estimated: ~1KB per episode metadata
- [ ] 5000 episodes ≈ 5MB cache
- [ ] Memory released when cache version changes (via GC)

---

### NFR-3: Reliability

**ID:** NFR-3
**Description:** The system shall handle failures gracefully.

**Acceptance Criteria:**
- [ ] API failures logged with details
- [ ] Partial cache usable if some series fail
- [ ] Jellyfin trigger failure doesn't crash plugin
- [ ] Cancellation supported during refresh

---

### NFR-4: Backwards Compatibility

**ID:** NFR-4
**Description:** Eager caching shall not break existing functionality.

**Acceptance Criteria:**
- [ ] Non-flat view still works (category → series → seasons → episodes)
- [ ] Caching can be disabled in settings
- [ ] Plugin works without cache (falls back to API)

---

## 4. Architecture Requirements

### AR-1: Three-Layer Cache Architecture

```
┌─────────────────┐
│  Xtream API     │  Source of truth (remote, slow)
└────────┬────────┘
         │ Batched API calls (FR-1)
         ↓
┌─────────────────┐
│ Plugin Cache    │  Staging buffer (RAM, fast, non-persistent)
│ (IMemoryCache)  │  Purpose: Batch API calls, serve Jellyfin quickly
└────────┬────────┘
         │ Auto-trigger Jellyfin refresh (FR-2)
         ↓
┌─────────────────┐
│  Jellyfin DB    │  Primary cache (SQLite, persistent, fast)
│ (jellyfin.db)   │  Purpose: Instant user browsing
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│  User Browser   │  End-user experience
└─────────────────┘
```

### AR-2: Data Flow Sequence

```
1. RefreshCacheAsync() starts
2. Fetch categories from API → Store in cache
3. For each category:
   a. Fetch series list → Store in cache
   b. For each series:
      i. Fetch seasons → Store in cache
      ii. For each season:
          - Fetch episodes → Store in cache
4. Cache fully populated
5. Trigger Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask
6. Jellyfin calls GetChannelItems() ~N times
7. Plugin serves from cache (microseconds per call)
8. Jellyfin populates jellyfin.db
9. User browses → Data served from jellyfin.db (instant)
```

### AR-3: Key Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `SeriesCacheService` | `Service/SeriesCacheService.cs` | Cache management, refresh logic |
| `SeriesChannel` | `SeriesChannel.cs` | Serve channel items from cache |
| `TaskService` | `Service/TaskService.cs` | Trigger Jellyfin scheduled tasks |
| `Plugin` | `Plugin.cs` | Configuration, service initialization |

---

## 5. Configuration Requirements

### CR-1: Plugin Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableSeriesCaching` | bool | true | Enable/disable eager caching |
| `SeriesCacheExpirationMinutes` | int | 60 | Refresh interval (min: 10, max: 1380) |
| `FlattenSeriesView` | bool | false | Show all series in flat list |

### CR-2: Settings Impact on Cache

- `EnableSeriesCaching = false`: No cache refresh, API calls on demand
- `EnableSeriesCaching = true`: Eager loading enabled
- `SeriesCacheExpirationMinutes`: Controls background refresh frequency
- Category/series selection changes: Triggers cache refresh

---

## 6. Error Handling Requirements

### EH-1: API Errors

| Error | Handling | User Impact |
|-------|----------|-------------|
| 404 Not Found | Log error, skip item | Partial data |
| 401 Unauthorized | Log error, abort refresh | No data, user notified |
| Timeout | Log error, retry once | Delayed refresh |
| Rate limit | Log warning, slow down | Slower refresh |

### EH-2: Jellyfin Trigger Errors

| Error | Handling | User Impact |
|-------|----------|-------------|
| Task not found | Log warning, continue | Cache populated, DB not |
| Task already running | Log info, skip | No impact |
| Unknown error | Log error, continue | Cache populated, DB not |

---

## 7. Future Considerations

### 7.1 Potential Enhancements

1. **Incremental refresh**: Only fetch changed data (requires API support)
2. **Priority refresh**: Refresh frequently-accessed series first
3. **Disk-based cache**: Persist cache to survive restarts
4. **Cache warming on demand**: Pre-fetch when user hovers over item

### 7.2 Known Limitations

1. Cache lost on Jellyfin restart (mitigated by auto-refresh on startup)
2. No delta updates (full refresh each time)
3. Memory usage scales with library size
4. Jellyfin DB cleanup not automatic when categories removed

---

## 8. Glossary

| Term | Definition |
|------|------------|
| **Eager Loading** | Pre-fetching all data upfront before user requests it |
| **Lazy Loading** | Fetching data only when user requests it |
| **Staging Buffer** | Temporary storage used to batch operations |
| **jellyfin.db** | Jellyfin's SQLite database storing library metadata |
| **IMemoryCache** | .NET in-process memory cache |
| **CacheDataVersion** | Hash of configuration affecting cache keys |

---

## 9. References

- [CACHING_ARCHITECTURE.md](./CACHING_ARCHITECTURE.md) - Detailed cache architecture
- [CACHE_INVALIDATION_CHALLENGE.md](./CACHE_INVALIDATION_CHALLENGE.md) - Cache invalidation analysis
- Jellyfin Channel Plugin Guide: https://jellyfin.org/docs/general/server/plugins/channels/
