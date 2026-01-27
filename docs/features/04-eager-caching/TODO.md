# Eager Caching - TODO

## Before PR Submission

- [ ] Cherry-pick all caching-related commits to clean PR branch
- [ ] Test against upstream Kevinjil/Jellyfin.Xtream master
- [ ] Verify backward compatibility with caching disabled
- [ ] Update build.yaml version and changelog
- [ ] Add CHANGELOG entry in PR description
- [ ] Document configuration options in plugin README
- [ ] Create performance benchmark documentation
- [ ] Add code comments to complex sections
- [ ] Run full manual test suite (see TEST_PLAN.md)

## Code Tasks

### High Priority

- [ ] Add error handling for Jellyfin task trigger timeout
  - **Current:** Fire-and-forget, no timeout
  - **Issue:** If Jellyfin hangs, no feedback to user
  - **Suggestion:** Add timeout and log warning

- [ ] Implement parallel series fetching with rate limiting
  - **Current:** Sequential (18 minutes for 200 series)
  - **Proposal:** SemaphoreSlim(10) for 10 concurrent requests
  - **Risk:** May hit Xtream provider rate limits
  - **Test:** Benchmark and measure API error rate

### Medium Priority

- [ ] Add cache statistics to settings UI
  - **Show:** Total cached series, episodes, memory usage, last refresh time
  - **Benefit:** Users can see cache effectiveness

- [ ] Implement cache warmup on series hover (prefetch)
  - **Idea:** When user hovers over series, prefetch season/episode data
  - **Benefit:** Even faster browsing for uncached series
  - **Trade-off:** More API calls, potential rate limiting

- [ ] Add "Cancel Refresh" button to UI
  - **Current:** Can only cancel via Clear Cache
  - **Benefit:** Better UX for long refreshes
  - **Implementation:** Call `_refreshCancellationTokenSource.Cancel()`

### Low Priority

- [ ] Optimize CacheDataVersion calculation
  - **Current:** Recomputes SHA256 on every cache access
  - **Suggestion:** Cache the hash value, recompute only on config change
  - **Impact:** Micro-optimization (~1µs per call)

- [ ] Add disk-based cache persistence
  - **Benefit:** Faster startup (no 18-minute warmup after restart)
  - **Trade-off:** Complexity (serialization, disk I/O, invalidation)
  - **Impact:** Low priority - restarts are rare

- [ ] Implement delta updates (incremental refresh)
  - **Benefit:** Faster refresh (only fetch changed data)
  - **Blocker:** Requires Xtream API support for "modified since" queries
  - **Impact:** Most Xtream APIs don't support this

## Documentation Tasks

- [ ] Add troubleshooting guide for common cache issues
  - "Cache refresh stuck at X%" - Check logs for API errors
  - "Old shows still appear" - Use Clear Cache button
  - "Cache not working" - Verify Enable Caching is checked

- [ ] Document memory usage guidelines
  - Small library (< 50 series): ~5MB
  - Medium library (50-200 series): ~15MB
  - Large library (200+ series): ~30MB+

- [ ] Add API endpoint documentation
  - POST /Xtream/RefreshCache
  - POST /Xtream/ClearCache
  - GET /Xtream/CacheStatus

- [ ] Create animated GIF for README showing cache refresh in action

## Testing Tasks

- [ ] Test with empty Xtream library (0 series)
- [ ] Test with very large library (500+ series)
- [ ] Test with malformed API responses (already handled in v0.9.5.3)
- [ ] Test cache behavior during Jellyfin restart
- [ ] Test concurrent manual refreshes (button spam)
- [ ] Test Clear Cache during active refresh
- [ ] Test configuration changes during active refresh

## Future Enhancements

### Intelligent Cache Invalidation

**Idea:** Only invalidate cache for changed categories/series
**Current:** Full refresh every time (all categories, all series)
**Proposal:**
1. Store hash of each category's series list
2. On refresh, compare hashes
3. Only fetch detailed data for changed series
4. Keep unchanged series in cache

**Benefit:** Much faster refresh (minutes → seconds)
**Complexity:** Moderate (need hash storage, comparison logic)
**Priority:** Medium

### Priority-Based Refresh

**Idea:** Refresh frequently-accessed content first
**Current:** Refresh all series in arbitrary order
**Proposal:**
1. Track series access count/time in cache metadata
2. Sort series by popularity before refresh
3. Fetch popular series first

**Benefit:** Users see their favorite content update faster
**Complexity:** Low (add access tracking)
**Priority:** Low

### Progressive Jellyfin DB Population

**Idea:** Trigger Jellyfin refresh incrementally as categories complete
**Current:** Wait for full cache refresh, then trigger once
**Proposal:**
1. Trigger Jellyfin refresh after each category completes
2. Jellyfin populates DB progressively
3. Users can start browsing before full refresh completes

**Benefit:** Faster perceived load time
**Complexity:** High (need to track category completion, avoid duplicate triggers)
**Priority:** Medium

### Cache Compression

**Idea:** Compress cached data to reduce memory usage
**Current:** Store raw objects in IMemoryCache
**Proposal:**
1. Serialize objects to JSON
2. Compress with Gzip
3. Store compressed bytes

**Benefit:** ~50-70% memory reduction
**Trade-off:** CPU overhead for compress/decompress
**Priority:** Low (memory is cheap, CPU matters more)

### Webhook Notifications

**Idea:** Notify external services when cache refresh completes
**Use Case:** Home automation (turn on TV notification "new shows available")
**Implementation:** POST to configurable webhook URL with cache stats
**Priority:** Low (niche feature)

## Blocked Items

- [ ] **BLOCKED:** Unit tests for SeriesCacheService
  - **Blocker:** Plugin has no existing test infrastructure
  - **Decision Needed:** Add xUnit project or rely on manual testing?
  - **Upstream:** Check if Kevin accepts PRs with tests

- [ ] **BLOCKED:** Cache metrics/analytics
  - **Blocker:** No telemetry framework in plugin
  - **Decision Needed:** Add StatsD, Prometheus, or custom?
  - **Upstream:** Jellyfin core may have metrics APIs to use

## Technical Debt

- [ ] Remove double-check pattern in RefreshCacheAsync
  - **Current:** SemaphoreSlim + boolean flag for thread safety
  - **Simpler:** Just use SemaphoreSlim (lock ensures _isRefreshing is safe)
  - **Reason for current:** Historical - boolean was added first, then semaphore
  - **Impact:** Low - works fine as-is

- [ ] Consolidate cache key building into helper method
  - **Current:** String concatenation throughout SeriesCacheService
  - **Better:** `BuildCacheKey(CacheKeyType type, params string[] parts)`
  - **Impact:** Low - current approach is clear and simple

- [ ] Extract progress reporting into separate class
  - **Current:** Progress tracked with fields in SeriesCacheService
  - **Better:** `CacheProgressTracker` class with thread-safe properties
  - **Impact:** Low - current approach works fine

## Questions for Code Review

1. **Version-based invalidation vs memory clearing**
   - Is version incrementing acceptable? (Pro: simple, Con: memory until GC)
   - Alternative: Track all cache keys in HashSet, clear explicitly

2. **Sequential vs parallel series fetching**
   - Should we parallelize by default? (Pro: faster, Con: rate limit risk)
   - Should this be configurable? (Pro: flexibility, Con: complexity)

3. **Fire-and-forget Jellyfin trigger**
   - Should we await TaskService.TriggerChannelRefreshTask()? (Pro: error handling, Con: slower API response)
   - Should we add completion callback? (Pro: UI feedback, Con: complexity)

4. **Cache expiration**
   - 24-hour absolute expiration sufficient? (Current: safety net)
   - Should we use sliding expiration? (Pro: keep hot data, Con: complexity)

5. **Error handling strategy**
   - Fail fast or continue with partial cache? (Current: continue)
   - Should malformed JSON fail the entire refresh? (Current: no - store empty data)

## PR Splitting Strategy

**Option 1: Monolithic PR**
- Include all caching features in one PR
- **Pro:** Complete feature, easier to review as whole
- **Con:** Large PR, harder to merge

**Option 2: Split into multiple PRs**

**PR 1:** Basic caching infrastructure (v0.9.4.6)
- SeriesCacheService.cs (basic cache CRUD)
- PluginConfiguration settings
- SeriesChannel cache fallback

**PR 2:** UI and manual controls (v0.9.4.10)
- config.html buttons
- XtreamController API endpoints
- Progress reporting

**PR 3:** True eager loading (v0.9.5.0)
- TaskService.cs
- Auto-trigger Jellyfin refresh
- Background startup refresh

**PR 4:** Bug fixes and enhancements (v0.9.5.2, v0.9.5.3)
- Malformed JSON handling
- Clear Cache DB cleanup
- Cancellation fixes

**Recommendation:** Option 2 - Split into 4 PRs
- Easier to review incrementally
- Can merge basic features first
- Allows upstream feedback to shape later PRs

## Maintenance Plan

### Regular Tasks

**Monthly:**
- Review cache performance metrics
- Check for memory leaks (monitor Docker container)
- Update documentation if caching behavior changes

**Per Release:**
- Test cache with new Jellyfin version
- Verify .NET memory cache APIs haven't changed
- Update benchmark numbers if performance changes

### Known Maintenance Areas

1. **Xtream API changes**
   - Watch for JSON schema changes
   - Update malformed JSON handlers if new patterns emerge

2. **Jellyfin API changes**
   - TaskService.TriggerChannelRefreshTask() relies on task name "Refresh Channels"
   - If Jellyfin renames/removes this task, code breaks

3. **.NET version upgrades**
   - IMemoryCache APIs may change (already happened in .NET 9)
   - CancellationToken patterns may evolve

### Monitoring Recommendations

**For Users:**
- Watch Jellyfin logs for cache errors
- Monitor Jellyfin CPU/memory during refresh
- Check cache refresh completion times (should stay consistent)

**For Developers:**
- Log cache hit/miss rates
- Track API error rates during refresh
- Monitor memory growth over time

---

## Checklist Before Marking Feature Complete

- [ ] All manual test cases pass (see TEST_PLAN.md)
- [ ] No regressions in non-caching mode
- [ ] Performance benchmarks documented
- [ ] Code comments on complex logic
- [ ] Logging covers all error paths
- [ ] Configuration validated on save
- [ ] UI provides clear feedback
- [ ] Documentation complete and accurate
- [ ] Upstream sync strategy defined
- [ ] Version bumped and changelog updated
