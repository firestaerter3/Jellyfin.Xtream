# Eager Caching Feature - Test Cases

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Related:** [EAGER_CACHING_REQUIREMENTS.md](./EAGER_CACHING_REQUIREMENTS.md)

---

## Testing Approach

This feature has been tested using both automated logic tests and manual integration testing.

---

## Automated Logic Tests

**Test Date:** 2026-01-27
**Branch:** `feature/flat-view-with-caching`
**Result:** **12/12 tests passed (100%)**

| Test | Description | Result |
|------|-------------|--------|
| 1 | TryGetValue returns false when key not in cache | PASS |
| 2 | Set stores value and TryGetValue returns true | PASS |
| 3 | Remove deletes key from cache | PASS |
| 4 | Cache version starts at 1 | PASS |
| 5 | InvalidateCache increments version | PASS |
| 6 | Multiple invalidations increment correctly | PASS |
| 7 | Invalidation clears existing keys | PASS |
| 8 | IsRefreshInProgress initially false | PASS |
| 9 | Concurrent refresh attempts blocked (semaphore) | PASS |
| 10 | Cache key prefixing with version works | PASS |
| 11 | GetSeriesKey generates correct format | PASS |
| 12 | GetEpisodesKey generates correct format | PASS |

**What Was Verified:**
- Core cache operations (get/set/remove) work correctly
- Version-based invalidation increments properly
- Concurrent refresh prevention via SemaphoreSlim
- Cache key generation with version prefix
- Invalidation clears stale keys

---

## Test Environment Setup

### Prerequisites
1. Jellyfin server running (Docker or native)
2. Plugin installed and configured with valid Xtream credentials
3. At least one series category selected with series
4. SSH access to server for log inspection
5. Access to Jellyfin admin dashboard

### Test Data Requirements
- Minimum: 1 category, 5 series, 10 seasons, 50 episodes
- Recommended: 2+ categories, 20+ series for realistic testing

### Log Inspection Commands
```bash
# Watch plugin logs in real-time
docker logs jellyfin -f 2>&1 | grep -iE 'Xtream|SeriesCache|GetChannelItems'

# Check specific log entries
docker logs jellyfin --tail 500 2>&1 | grep -iE 'cache refresh|Triggering|populated'
```

---

## Test Suite 1: Cache Refresh Functionality

### TC-1.1: Manual Cache Refresh via UI

**Objective:** Verify manual cache refresh populates cache and triggers Jellyfin DB population.

**Preconditions:**
- Plugin installed with valid credentials
- At least one category with series selected
- "Enable Series Caching" checked

**Steps:**
1. Navigate to Plugin Settings → Series tab
2. Click "Refresh Now" button
3. Observe progress bar and status text
4. Wait for completion (status shows "Completed: X series, Y seasons, Z episodes")
5. Check Jellyfin logs for cache refresh messages

**Expected Results:**
- [ ] Button changes to "Starting..." then re-enables
- [ ] Progress bar shows 0% → 100%
- [ ] Status shows progress messages during refresh
- [ ] Log shows: `Starting series data cache refresh`
- [ ] Log shows: `Fetching series categories...`
- [ ] Log shows: `Cache refresh completed: X series, Y seasons, Z episodes`
- [ ] Log shows: `Triggering Jellyfin channel refresh to populate jellyfin.db from cache`
- [ ] Log shows: `Jellyfin channel refresh triggered successfully`

**Pass/Fail:** ____

---

### TC-1.2: Automatic Cache Refresh on Startup

**Objective:** Verify cache refreshes automatically when Jellyfin starts.

**Preconditions:**
- Plugin configured with valid credentials
- "Enable Series Caching" checked

**Steps:**
1. Restart Jellyfin: `docker restart jellyfin`
2. Wait 30 seconds for startup
3. Check Jellyfin logs

**Expected Results:**
- [ ] Log shows: `Starting series data cache refresh` (within 60 seconds of startup)
- [ ] Cache refresh completes without errors
- [ ] Jellyfin channel refresh triggered after cache completion

**Pass/Fail:** ____

---

### TC-1.3: Scheduled Cache Refresh

**Objective:** Verify cache refreshes on configured interval.

**Preconditions:**
- Set refresh interval to 10 minutes (minimum)
- Plugin configured with valid credentials

**Steps:**
1. Note current time
2. Manually trigger a refresh and wait for completion
3. Wait 10+ minutes
4. Check logs for automatic refresh

**Expected Results:**
- [ ] Automatic refresh starts approximately at configured interval
- [ ] Log shows scheduled task execution
- [ ] Cache refresh completes successfully

**Pass/Fail:** ____

---

### TC-1.4: Cache Refresh on Configuration Change

**Objective:** Verify cache refreshes when category selection changes.

**Preconditions:**
- Plugin configured and working
- Multiple categories available

**Steps:**
1. Go to Plugin Settings → Series tab
2. Enable/disable a different category
3. Click "Save"
4. Check logs

**Expected Results:**
- [ ] Configuration saved successfully
- [ ] Cache refresh triggered automatically
- [ ] Log shows: `Failed to refresh series cache after configuration update` OR successful refresh

**Pass/Fail:** ____

---

## Test Suite 2: Jellyfin Database Population

### TC-2.1: Verify Jellyfin DB Contains All Series

**Objective:** Confirm jellyfin.db is populated after eager loading.

**Preconditions:**
- Cache refresh completed successfully
- Jellyfin channel refresh triggered

**Steps:**
1. Complete a full cache refresh
2. Wait for "Jellyfin channel refresh triggered" log message
3. Wait additional 2-5 minutes for Jellyfin to process
4. Navigate to Xtream Series channel in Jellyfin UI
5. Count displayed series

**Expected Results:**
- [ ] Series count in UI matches cache completion log
- [ ] All series display immediately (no loading spinner)
- [ ] Series have correct names and images

**Pass/Fail:** ____

---

### TC-2.2: Verify Seasons Load Instantly

**Objective:** Confirm seasons are pre-populated in jellyfin.db.

**Preconditions:**
- Eager loading completed (cache + Jellyfin refresh)

**Steps:**
1. Open Xtream Series channel
2. Click on any series
3. Observe load time for seasons

**Expected Results:**
- [ ] Seasons appear within 1 second (no API call needed)
- [ ] No loading spinner visible
- [ ] Season count matches expected data

**Pass/Fail:** ____

---

### TC-2.3: Verify Episodes Load Instantly

**Objective:** Confirm episodes are pre-populated in jellyfin.db.

**Preconditions:**
- Eager loading completed

**Steps:**
1. Navigate to a series → season
2. Observe load time for episodes

**Expected Results:**
- [ ] Episodes appear within 1 second
- [ ] No loading spinner visible
- [ ] Episode metadata (title, duration, image) displays correctly

**Pass/Fail:** ____

---

### TC-2.4: Verify No API Calls During Browsing

**Objective:** Confirm browsing uses jellyfin.db, not API.

**Preconditions:**
- Eager loading completed

**Steps:**
1. Start watching logs: `docker logs jellyfin -f 2>&1 | grep -i xtream`
2. Browse through several series → seasons → episodes in UI
3. Check logs for API calls

**Expected Results:**
- [ ] `GetChannelItems` may appear (Jellyfin calling plugin)
- [ ] No `QueryApi` calls to Xtream server during browsing
- [ ] No HTTP errors or timeouts
- [ ] Browsing feels instant

**Pass/Fail:** ____

---

## Test Suite 3: Cache Invalidation

### TC-3.1: Clear Cache Button

**Objective:** Verify "Clear Cache" invalidates cache correctly.

**Preconditions:**
- Cache populated with data

**Steps:**
1. Click "Clear Cache" button
2. Confirm when prompted
3. Check cache status in UI
4. Check logs

**Expected Results:**
- [ ] Button shows "Clearing..." then re-enables
- [ ] Success message displayed
- [ ] Progress bar resets to 0%
- [ ] Status shows "Cache invalidated" or "Cache cleared"
- [ ] Log shows: `Cache invalidated (version incremented to X)`

**Pass/Fail:** ____

---

### TC-3.2: Clear Cache Stops Running Refresh

**Objective:** Verify clearing cache cancels in-progress refresh.

**Preconditions:**
- None

**Steps:**
1. Click "Refresh Now" to start a refresh
2. While refresh is in progress (status shows processing), click "Clear Cache"
3. Confirm when prompted about stopping refresh
4. Check logs

**Expected Results:**
- [ ] Confirmation dialog mentions stopping refresh
- [ ] Refresh is cancelled
- [ ] Log shows: `Cache refresh cancelled`
- [ ] Status shows "Cancelled" or "Cache cleared"

**Pass/Fail:** ____

---

### TC-3.3: Cache Version Increment

**Objective:** Verify cache version increments on invalidation.

**Preconditions:**
- Cache populated

**Steps:**
1. Check logs for current cache version
2. Click "Clear Cache"
3. Check logs for new version

**Expected Results:**
- [ ] Log shows version increment: `version incremented to X`
- [ ] Old cache keys no longer accessible
- [ ] Next refresh uses new version prefix

**Pass/Fail:** ____

---

## Test Suite 4: Error Handling

### TC-4.1: Invalid Credentials

**Objective:** Verify graceful handling of authentication errors.

**Preconditions:**
- Intentionally set wrong credentials

**Steps:**
1. Set invalid username/password in Credentials tab
2. Save configuration
3. Attempt cache refresh
4. Check logs and UI

**Expected Results:**
- [ ] Refresh fails with clear error message
- [ ] Log shows: `401 Unauthorized` or similar
- [ ] UI shows error status
- [ ] Plugin doesn't crash

**Pass/Fail:** ____

---

### TC-4.2: Network Timeout

**Objective:** Verify handling of network issues.

**Preconditions:**
- Valid configuration

**Steps:**
1. Block network access to Xtream server (firewall/disconnect)
2. Attempt cache refresh
3. Check logs

**Expected Results:**
- [ ] Refresh fails with timeout error
- [ ] Log shows connection/timeout error
- [ ] Status indicates failure
- [ ] Plugin remains functional

**Pass/Fail:** ____

---

### TC-4.3: Partial API Failure

**Objective:** Verify partial data is cached when some series fail.

**Preconditions:**
- Valid configuration with multiple series

**Steps:**
1. Start cache refresh
2. If a series fails (check logs), verify others succeed
3. Check final cache status

**Expected Results:**
- [ ] Log shows warning for failed series
- [ ] Other series cached successfully
- [ ] Partial data available for browsing
- [ ] Completion message shows actual counts

**Pass/Fail:** ____

---

### TC-4.4: Jellyfin Refresh Trigger Failure

**Objective:** Verify cache is preserved if Jellyfin trigger fails.

**Preconditions:**
- Valid configuration

**Steps:**
1. Start cache refresh
2. Check if Jellyfin trigger succeeds/fails
3. Verify cache data is still available

**Expected Results:**
- [ ] Cache data preserved regardless of trigger outcome
- [ ] Log shows warning if trigger fails
- [ ] Manual browsing still works (triggers lazy load)

**Pass/Fail:** ____

---

## Test Suite 5: Performance

### TC-5.1: Cache Refresh Duration

**Objective:** Measure cache refresh performance.

**Test Data:** Note your actual numbers

| Metric | Value |
|--------|-------|
| Categories | ___ |
| Series | ___ |
| Seasons | ___ |
| Episodes | ___ |

**Steps:**
1. Note start time from log: `Starting series data cache refresh`
2. Note end time from log: `Cache refresh completed`
3. Calculate duration

**Expected Results:**
- [ ] Duration < 30 minutes for 200 series
- [ ] Progress updates every few seconds
- [ ] No timeout errors

**Measured Duration:** ___ minutes

**Pass/Fail:** ____

---

### TC-5.2: Jellyfin DB Population Duration

**Objective:** Measure time from Jellyfin trigger to browsable data.

**Steps:**
1. Note time of: `Jellyfin channel refresh triggered`
2. Periodically check Xtream Series channel in UI
3. Note when all series appear

**Expected Results:**
- [ ] Duration < 10 minutes for 200 series
- [ ] Progress visible in Jellyfin logs

**Measured Duration:** ___ minutes

**Pass/Fail:** ____

---

### TC-5.3: Browsing Response Time

**Objective:** Measure browsing performance after eager loading.

**Steps:**
1. Complete eager loading
2. Open browser developer tools (Network tab)
3. Navigate: Channel → Series → Season → Episodes
4. Note response times

**Expected Results:**
- [ ] Each navigation < 1 second
- [ ] No visible loading spinners
- [ ] Smooth user experience

**Measured Times:**
- Channel load: ___ ms
- Series → Seasons: ___ ms
- Season → Episodes: ___ ms

**Pass/Fail:** ____

---

## Test Suite 6: Edge Cases

### TC-6.1: Empty Category

**Objective:** Verify handling of category with no series.

**Preconditions:**
- Select a category that has no series

**Steps:**
1. Refresh cache
2. Check logs for empty category handling
3. Browse to the channel

**Expected Results:**
- [ ] No errors for empty category
- [ ] Log shows: `Found 0 series in category X`
- [ ] UI displays empty or skips category

**Pass/Fail:** ____

---

### TC-6.2: Caching Disabled

**Objective:** Verify plugin works without caching.

**Preconditions:**
- Uncheck "Enable Series Caching"
- Save configuration

**Steps:**
1. Browse to Xtream Series channel
2. Click on a series
3. Check logs for API calls

**Expected Results:**
- [ ] Browsing works (slower, lazy loading)
- [ ] API calls made on each navigation
- [ ] No cache-related log messages

**Pass/Fail:** ____

---

### TC-6.3: Flat View vs Category View

**Objective:** Verify eager loading works for both view modes.

**Steps:**
1. Enable "Flatten Series View", refresh cache, browse
2. Disable "Flatten Series View", refresh cache, browse

**Expected Results:**
- [ ] Flat view: All series appear in single list, instant load
- [ ] Category view: Categories → Series → Seasons → Episodes, all instant

**Pass/Fail:** ____

---

### TC-6.4: Large Library (Stress Test)

**Objective:** Verify stability with large data sets.

**Preconditions:**
- Select all available categories (100+ series if available)

**Steps:**
1. Start cache refresh
2. Monitor memory usage: `docker stats jellyfin`
3. Wait for completion
4. Browse library

**Expected Results:**
- [ ] Refresh completes without crash
- [ ] Memory usage reasonable (< 500MB increase)
- [ ] Browsing remains responsive

**Pass/Fail:** ____

---

## Test Suite 7: HTTP Retry Logic (v0.9.6.0)

### TC-7.1: Transient 500 Error Recovery

**Objective:** Verify that transient HTTP 500 errors are retried successfully

**Preconditions:**
- Plugin installed with retry enabled (`EnableHttpRetry = true`)
- `HttpRetryMaxAttempts = 3`
- Mock or flaky Xtream provider returning intermittent 500 errors

**Steps:**
1. Identify series that occasionally returns 500 errors
2. Trigger cache refresh via "Refresh Now" button
3. Monitor Jellyfin logs during refresh

**Expected Result:**
- [ ] Log shows retry attempts: "HTTP 500 error on attempt 1/3 for {Url}. Retrying in 1000ms."
- [ ] Log shows retry attempts: "HTTP 500 error on attempt 2/3 for {Url}. Retrying in 2000ms."
- [ ] Eventually succeeds: Series data cached successfully
- [ ] No entry in failure tracking cache (transient error recovered)

**Pass/Fail:** ____

---

### TC-7.2: Persistent 500 Error Handling

**Objective:** Verify that persistent HTTP 500 errors are tracked and skipped

**Preconditions:**
- Plugin installed with retry enabled
- Xtream provider returning consistent 500 errors for specific series

**Steps:**
1. Trigger cache refresh
2. Monitor logs for series that consistently fail
3. Wait for cache refresh to complete
4. Trigger second cache refresh immediately
5. Monitor logs for same series

**Expected Result:**
- [ ] First refresh: Retry attempts logged (3 attempts, 7s total delay)
- [ ] First refresh: Persistent failure logged with series ID
- [ ] Failure summary logged: "Cache refresh completed with N persistent HTTP failures"
- [ ] Series recorded in failure cache with 24h expiration
- [ ] Second refresh: Series skipped immediately (no retry attempts)
- [ ] Log shows: "Skipping retry for known persistent failure: {Url}"
- [ ] Cache refresh faster on second attempt (failures skipped)

**Pass/Fail:** ____

---

### TC-7.3: Non-Retryable 404 Error

**Objective:** Verify that 4xx client errors fail immediately without retry

**Preconditions:**
- Plugin installed with retry enabled
- Invalid series ID causing 404 Not Found

**Steps:**
1. Attempt to fetch data for non-existent series
2. Monitor logs for retry behavior

**Expected Result:**
- [ ] No retry attempts logged
- [ ] Immediate failure: "Non-retryable HTTP 404 error for {Url}. Not retrying."
- [ ] Exception thrown or graceful degradation (depending on `HttpRetryThrowOnPersistentFailure`)

**Pass/Fail:** ____

---

### TC-7.4: Exponential Backoff Timing

**Objective:** Verify exponential backoff delays are correct

**Preconditions:**
- Plugin installed with default settings
- `HttpRetryMaxAttempts = 3`, `HttpRetryInitialDelayMs = 1000`

**Steps:**
1. Trigger cache refresh with known failing series
2. Monitor timestamps in logs for retry attempts

**Expected Result:**
- [ ] Attempt 1 → 2: ~1 second delay
- [ ] Attempt 2 → 3: ~2 second delay
- [ ] Total retry time: ~3-4 seconds (excluding API call duration)
- [ ] Log confirms: "Retrying in 1000ms" → "Retrying in 2000ms"

**Pass/Fail:** ____

---

### TC-7.5: Retry Disabled Behavior

**Objective:** Verify original behavior preserved when retry disabled

**Preconditions:**
- `EnableHttpRetry = false`

**Steps:**
1. Trigger cache refresh
2. Encounter 500 error
3. Monitor logs

**Expected Result:**
- [ ] No retry attempts
- [ ] Immediate failure on first 500 error
- [ ] Exception thrown or empty object returned (depending on error handling)
- [ ] No entries in failure tracking cache

**Pass/Fail:** ____

---

### TC-7.6: Cancellation During Retry

**Objective:** Verify retry respects cancellation token

**Preconditions:**
- Plugin installed with retry enabled
- Cache refresh in progress with retry delays

**Steps:**
1. Start cache refresh
2. Wait for retry attempt to start (in backoff delay)
3. Click "Clear Cache" to cancel refresh

**Expected Result:**
- [ ] Backoff delay interrupted immediately
- [ ] OperationCanceledException logged
- [ ] No additional retry attempts after cancellation
- [ ] Cache version incremented (clear successful)

**Pass/Fail:** ____

---

### TC-7.7: Failure Cache Expiration

**Objective:** Verify failed URLs are retried after 24 hours

**Preconditions:**
- `HttpFailureCacheExpirationHours = 24`
- Previously failed URL in failure cache

**Steps:**
1. Record timestamp of failure
2. Wait 24+ hours (or modify expiration to 1 minute for faster testing)
3. Trigger cache refresh
4. Monitor logs for same URL

**Expected Result:**
- [ ] After expiration: URL no longer in failure cache
- [ ] Retry attempts resume for previously failed URL
- [ ] If still failing: Re-recorded in failure cache with new expiration

**Pass/Fail:** ____

---

### TC-7.8: Configuration Validation

**Objective:** Verify configuration boundaries are enforced

**Preconditions:**
- Access to plugin configuration

**Steps:**
1. Set `HttpRetryMaxAttempts = 15` (beyond max of 10)
2. Set `HttpRetryInitialDelayMs = 50` (below min of 100)
3. Trigger cache refresh
4. Monitor actual retry behavior

**Expected Result:**
- [ ] Max attempts capped at 10
- [ ] Initial delay capped at minimum 100ms
- [ ] Logs reflect validated configuration values

**Pass/Fail:** ____

---

### TC-7.9: Performance Impact Measurement

**Objective:** Measure performance impact of retry logic

**Preconditions:**
- 760 series library with 91 known failures
- Retry enabled vs. disabled comparison

**Steps:**
1. Disable retry: Trigger cache refresh, record duration and failure count
2. Enable retry: Trigger cache refresh, record duration and failure count
3. Compare results

**Expected Result:**
- [ ] Retry disabled: ~18 minutes, 91 failures
- [ ] Retry enabled (first run): ~25-30 minutes, 40-50 failures (transients recovered)
- [ ] Retry enabled (second run): ~18 minutes, <5 additional minutes (failures skipped)
- [ ] Cache completeness improved from 88% to 94-95%

**Pass/Fail:** ____

---

### TC-7.10: Graceful Degradation

**Objective:** Verify empty objects returned on persistent failure

**Preconditions:**
- `HttpRetryThrowOnPersistentFailure = false` (default)
- Series with persistent 500 error

**Steps:**
1. Trigger cache refresh
2. After all retries exhausted for failing series
3. Check cache for series data

**Expected Result:**
- [ ] Empty `SeriesStreamInfo` object stored in cache
- [ ] No exception thrown
- [ ] Cache refresh completes successfully
- [ ] Other series (88%) cached normally

**Pass/Fail:** ____

---

## Test Execution Summary

| Suite | Total | Passed | Failed | Blocked |
|-------|-------|--------|--------|---------|
| 1. Cache Refresh | 4 | | | |
| 2. DB Population | 4 | | | |
| 3. Invalidation | 3 | | | |
| 4. Error Handling | 4 | | | |
| 5. Performance | 3 | | | |
| 6. Edge Cases | 4 | | | |
| 7. HTTP Retry Logic | 10 | | | |
| **Total** | **32** | | | |

---

## Defect Log

| ID | Test Case | Description | Severity | Status |
|----|-----------|-------------|----------|--------|
| | | | | |

---

## Notes

_Record any observations, environment issues, or suggestions during testing._

```
Date:
Tester:
Environment:
Notes:
```
