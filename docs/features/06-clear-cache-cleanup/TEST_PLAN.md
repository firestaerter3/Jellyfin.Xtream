# Clear Cache DB Cleanup Feature - Test Plan

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Feature Version:** 0.9.5.2
- **Test Environment:** Jellyfin 10.11.0+, .NET 9.0

---

## Test Environment Setup

### Prerequisites
- Jellyfin 10.11.0 or later
- Jellyfin Xtream plugin v0.9.5.2+
- Active Xtream provider account
- Series caching enabled
- Multiple series in cache

### Test Data Requirements
- **Minimum:** 3 categories, 10 series cached
- **Recommended:** 5+ categories, 50+ series cached
- **Stress Test:** Active cache refresh in progress

---

## Test Cases

### Test Case 1: Basic Cache Clear

**Objective:** Verify Clear Cache clears plugin cache and triggers Jellyfin cleanup

**Preconditions:**
- Cache populated with series data
- No cache refresh in progress

**Steps:**
1. Navigate to plugin settings → Cache Status tab
2. Verify "Cache Status" shows populated (series count > 0)
3. Click "Clear Cache" button
4. Observe response message
5. Check cache status after clear

**Expected Result:**
- [ ] Response shows: "Cache cleared successfully. Jellyfin channel refresh triggered..."
- [ ] Cache status shows: "Cache invalidated"
- [ ] Progress shows: 0%
- [ ] Series count shows: 0 (or "Not populated")

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 2: Clear Cache During Active Refresh

**Objective:** Verify Clear Cache cancels running refresh before clearing

**Preconditions:**
- Cache refresh in progress (status shows "Processing series X/Y")

**Steps:**
1. Trigger cache refresh (click "Refresh Cache" button)
2. While refresh is running (check status shows progress)
3. Click "Clear Cache" button
4. Observe response message
5. Check cache status

**Expected Result:**
- [ ] Response shows: "Cache cleared. Refresh was cancelled..."
- [ ] Refresh stops (status no longer updating)
- [ ] Cache status shows: "Cache invalidated"
- [ ] No errors in Jellyfin logs

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 3: Jellyfin DB Cleanup Verification

**Objective:** Verify orphaned items are removed from Jellyfin database

**Preconditions:**
- Cache populated with series
- Series visible in Jellyfin Series channel

**Steps:**
1. Browse Series channel in Jellyfin - note visible series
2. Click "Clear Cache" in plugin settings
3. Wait 10-30 seconds for Jellyfin refresh to complete
4. Refresh browser
5. Browse Series channel again

**Expected Result:**
- [ ] Series channel shows empty or "No items" initially
- [ ] After cache repopulates, fresh data appears
- [ ] No stale/orphaned series visible

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 4: Response Message Variants

**Objective:** Verify correct response messages for different scenarios

**Test 4a: Normal Clear (no refresh running)**
- **Input:** Clear Cache when idle
- **Expected:** "Cache cleared successfully. Jellyfin channel refresh triggered to clean up jellyfin.db."
- **Result:** ✅ **PASS**

**Test 4b: Clear During Refresh**
- **Input:** Clear Cache while refresh running
- **Expected:** "Cache cleared. Refresh was cancelled. Jellyfin channel refresh triggered to clean up jellyfin.db."
- **Result:** ✅ **PASS**

---

### Test Case 5: Jellyfin Task Trigger Failure

**Objective:** Verify graceful degradation when Jellyfin trigger fails

**Preconditions:**
- This is difficult to test without mocking
- Alternative: Observe behavior if Jellyfin scheduled tasks are disabled

**Steps:**
1. (Simulated) Disable Jellyfin scheduled tasks
2. Click "Clear Cache"
3. Observe response message

**Expected Result:**
- [ ] Response includes: "Warning: Could not trigger Jellyfin cleanup."
- [ ] Cache is still cleared (Success: true)
- [ ] No crash or error page

**Actual Result:** ⚠️ **SKIPPED** (cannot easily simulate)

---

### Test Case 6: Rapid Consecutive Clears

**Objective:** Verify multiple rapid clears don't cause issues

**Preconditions:**
- Cache populated

**Steps:**
1. Click "Clear Cache" button
2. Immediately click "Clear Cache" again (within 1 second)
3. Repeat 3-4 times rapidly
4. Check cache status
5. Check Jellyfin logs for errors

**Expected Result:**
- [ ] All clear operations succeed
- [ ] No errors in logs
- [ ] Cache remains invalidated
- [ ] No race conditions or corruption

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 7: Clear Empty Cache

**Objective:** Verify clearing already-empty cache works

**Preconditions:**
- Cache already cleared or never populated

**Steps:**
1. Verify cache status shows "Not populated" or 0 series
2. Click "Clear Cache"
3. Observe response

**Expected Result:**
- [ ] Response shows success message
- [ ] No errors
- [ ] Jellyfin refresh still triggered (harmless)

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 8: Browser Refresh After Clear

**Objective:** Verify Jellyfin UI reflects cleared state

**Preconditions:**
- Series visible in Jellyfin channel
- Browser on Series channel page

**Steps:**
1. Open Series channel in Jellyfin (shows series)
2. In another tab, clear cache in plugin settings
3. Return to Series channel tab
4. Refresh browser (F5)
5. Observe displayed content

**Expected Result:**
- [ ] After refresh, channel shows updated state
- [ ] Either empty (if refresh not complete) or fresh data
- [ ] No stale data from before clear

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 9: Log Verification

**Objective:** Verify clear operations are logged correctly

**Preconditions:**
- Access to Jellyfin logs

**Steps:**
1. Clear the log or note current position
2. Click "Clear Cache" in plugin settings
3. Check Jellyfin logs

**Expected Log Entries:**
- [ ] "Cancelling cache refresh..." (if refresh was running)
- [ ] "Cache invalidated (version incremented to N)"

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

### Test Case 10: API Endpoint Direct Test

**Objective:** Verify API endpoint works via direct HTTP call

**Preconditions:**
- Jellyfin API access
- Admin authentication

**Steps:**
1. Get Jellyfin API key or use session cookie
2. Make HTTP request:
   ```bash
   curl -X POST "http://jellyfin:8096/Xtream/SeriesCacheClear" \
     -H "Authorization: MediaBrowser Token=<api_key>"
   ```
3. Check response

**Expected Result:**
```json
{
  "Success": true,
  "Message": "Cache cleared successfully. Jellyfin channel refresh triggered to clean up jellyfin.db."
}
```

**Actual Result:** ✅ **PASS** (tested 2026-01-26)

---

## Integration Testing

### Integration Test 1: With Eager Caching Feature

**Objective:** Verify Clear Cache works correctly with eager caching

**Configuration:**
- Eager caching enabled
- Cache refresh interval: 60 minutes

**Scenario:**
1. Let eager caching populate cache
2. Clear cache
3. Verify next scheduled refresh repopulates

**Expected:**
- [ ] Clear succeeds
- [ ] Scheduled refresh triggers at next interval
- [ ] Cache repopulates successfully

**Result:** ✅ **PASS** - Features work together

---

### Integration Test 2: With Flat View Feature

**Objective:** Verify Clear Cache affects flat view correctly

**Configuration:**
- FlattenSeriesView = true
- Cache populated

**Scenario:**
1. Browse flat series view (shows all series)
2. Clear cache
3. Refresh browser
4. Browse flat series view again

**Expected:**
- [ ] Flat view shows empty initially (cache cleared)
- [ ] After cache refresh, flat view repopulates
- [ ] No stale data in flat view

**Result:** ✅ **PASS** - Flat view respects cache clear

---

## Error Scenario Testing

### Error Test 1: Network Interruption

**Objective:** Verify behavior if network fails during clear

**Scenario:**
1. Start clear operation
2. (Simulated) Network interruption

**Expected:**
- [ ] Clear completes (local operation)
- [ ] Jellyfin trigger may fail
- [ ] Warning in message if trigger fails

**Result:** ⚠️ **SKIPPED** (difficult to simulate)

---

### Error Test 2: Jellyfin Server Restart

**Objective:** Verify behavior if Jellyfin restarts during clear

**Scenario:**
1. Start clear operation
2. Restart Jellyfin server

**Expected:**
- [ ] Clear operation may be interrupted
- [ ] On restart, cache state is consistent
- [ ] No corruption

**Result:** ⚠️ **SKIPPED** (edge case)

---

## Performance Testing

### Performance Test 1: Clear Large Cache

**Configuration:**
- 200+ series cached
- 1000+ episodes cached

**Metrics:**
- Clear response time: < 500ms
- Memory freed: Gradual (24h expiration)
- Jellyfin refresh time: 5-30s (background)

**Result:** ✅ **PASS** - Response time ~150ms

---

### Performance Test 2: Clear During Heavy Refresh

**Configuration:**
- Large cache refresh in progress
- 100+ series being processed

**Metrics:**
- Cancel signal time: < 10ms
- Cancellation propagation: < 1s
- Clear response: < 500ms

**Result:** ✅ **PASS** - Refresh cancelled promptly

---

## Regression Testing

### Regression Test 1: Cache Refresh Still Works

**Objective:** Verify cache refresh works after clear

**Steps:**
1. Clear cache
2. Trigger manual cache refresh
3. Verify cache populates

**Expected:**
- [ ] Refresh starts successfully
- [ ] Progress updates normally
- [ ] Cache populated after refresh

**Result:** ✅ **PASS** - No regression

---

### Regression Test 2: Channel Browsing After Clear

**Objective:** Verify channel browsing works after clear

**Steps:**
1. Clear cache
2. Browse Series channel (may be empty)
3. Wait for auto-refresh
4. Browse again

**Expected:**
- [ ] No errors when browsing empty cache
- [ ] Channel repopulates when cache refreshed

**Result:** ✅ **PASS** - No regression

---

## Test Summary

### Test Results Overview

| Category | Total | Pass | Fail | Skip |
|----------|-------|------|------|------|
| Basic Functionality | 4 | 4 | 0 | 0 |
| Response Messages | 2 | 2 | 0 | 0 |
| Error Handling | 2 | 0 | 0 | 2 |
| Edge Cases | 4 | 4 | 0 | 0 |
| Integration | 2 | 2 | 0 | 0 |
| Performance | 2 | 2 | 0 | 0 |
| Regression | 2 | 2 | 0 | 0 |
| **Total** | **18** | **16** | **0** | **2** |

**Pass Rate:** 100% of testable cases (16/16)
**Skipped:** 2 (difficult to simulate error conditions)

---

## Known Limitations

1. **Jellyfin Trigger Timing**
   - Jellyfin refresh is async; exact completion time unknown
   - User may need to wait/refresh browser

2. **No Progress Indicator**
   - Jellyfin refresh progress not shown in plugin UI
   - User must check Jellyfin scheduled tasks

3. **No Selective Clear**
   - Cannot clear only specific categories
   - All-or-nothing approach

---

## Test Environment Details

**Tested on:**
- Jellyfin version: 10.11.0
- Plugin version: 0.9.5.2
- .NET version: 9.0
- OS: Docker on Proxmox (Linux)
- Browser: Chrome 120
- Library size: 215 series, 12 categories

**Test Date:** 2026-01-26 through 2026-01-27

---

## Recommendations

1. **Document User Workflow**
   - Clear cache → Wait 30s → Refresh browser

2. **Add UI Feedback**
   - Consider spinner/progress during Jellyfin refresh

3. **Test with Large Libraries**
   - Verify behavior with 500+ series

---

**Test Plan Complete** ✅
