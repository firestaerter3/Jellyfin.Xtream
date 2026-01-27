# Feature 07: Config UI Error Handling - Test Plan

## Document Info
- **Feature:** Config UI Error Handling
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document describes the test cases for verifying the configuration UI error handling improvements.

---

## Test Environment

- Jellyfin server with plugin installed
- Browser with Developer Tools (F12) for console access
- Access to plugin configuration page (Dashboard > Plugins > Xtream Flat View)

---

## Test Cases

### TC-1: Clear Cache Button Recovery - Success Path

**Objective:** Verify button recovers after successful cache clear.

**Prerequisites:**
- Plugin configured with valid Xtream credentials
- Series caching enabled with populated cache

**Steps:**
1. Navigate to Series tab in plugin configuration
2. Click "Clear Cache" button
3. Confirm the action in dialog
4. Observe button state during operation
5. Wait for operation to complete

**Expected Results:**
- [ ] Button shows "Clearing..." during operation
- [ ] Button is disabled during operation
- [ ] Success alert is displayed
- [ ] Button returns to "Clear Cache" text
- [ ] Button is re-enabled
- [ ] Progress bar resets to 0%
- [ ] Status shows "Cache cleared"

**Status:**

---

### TC-2: Clear Cache Button Recovery - Error Path

**Objective:** Verify button recovers after failed cache clear.

**Prerequisites:**
- Plugin installed (credentials not required)

**Steps:**
1. Navigate to Series tab
2. Open browser Developer Tools (F12)
3. Go to Network tab, enable "Offline" mode or block requests
4. Click "Clear Cache" button
5. Confirm the action
6. Observe button state

**Expected Results:**
- [ ] Button shows "Clearing..." during attempt
- [ ] Error alert is displayed with message
- [ ] Button returns to "Clear Cache" text
- [ ] Button is re-enabled (not stuck)
- [ ] Console shows error details

**Status:**

---

### TC-3: Error Message Non-Stacking

**Objective:** Verify errors don't accumulate when switching tabs.

**Prerequisites:**
- Xtream credentials NOT configured (to trigger error)

**Steps:**
1. Navigate to Series tab (error should appear)
2. Switch to VOD tab (error should appear)
3. Switch back to Series tab
4. Switch back to VOD tab
5. Count error messages on each tab

**Expected Results:**
- [ ] Each tab shows exactly ONE error message
- [ ] Error messages don't stack/accumulate
- [ ] Previous error is cleared when tab is revisited

**Status:**

---

### TC-4: Progress Bar Updates

**Objective:** Verify real-time progress bar updates during cache refresh.

**Prerequisites:**
- Plugin configured with valid Xtream credentials
- Sufficient series data to observe progress (>100 series)

**Steps:**
1. Navigate to Series tab
2. Clear any existing cache
3. Click "Refresh Now" button
4. Observe progress bar and status text

**Expected Results:**
- [ ] Progress bar appears
- [ ] Progress bar fills from left to right
- [ ] Percentage increases over time
- [ ] Status text shows current operation (e.g., "Processing series 50 of 200")
- [ ] Status text is blue during refresh
- [ ] Status text turns green at 100%
- [ ] Progress bar animation is smooth (CSS transition)

**Status:**

---

### TC-5: Button States During Refresh

**Objective:** Verify correct button states during active refresh.

**Prerequisites:**
- Plugin configured with valid Xtream credentials

**Steps:**
1. Navigate to Series tab
2. Click "Refresh Now" button
3. Observe button states during refresh
4. Try clicking "Refresh Now" again
5. Verify "Clear Cache" button is clickable

**Expected Results:**
- [ ] "Refresh Now" button is disabled during refresh
- [ ] "Refresh Now" button text changes to "Starting..."
- [ ] Clicking disabled "Refresh Now" shows alert (if somehow clicked)
- [ ] "Clear Cache" button remains enabled
- [ ] "Clear Cache" can cancel the refresh

**Status:**

---

### TC-6: Clear Cache During Active Refresh

**Objective:** Verify cache can be cleared during active refresh.

**Prerequisites:**
- Plugin configured with valid Xtream credentials

**Steps:**
1. Start a cache refresh
2. While refresh is running, click "Clear Cache"
3. Observe confirmation dialog
4. Confirm the clear

**Expected Results:**
- [ ] Confirmation dialog mentions refresh in progress
- [ ] Dialog asks if user wants to stop refresh
- [ ] Refresh is cancelled
- [ ] Cache is cleared
- [ ] Success message mentions cancelled refresh

**Status:**

---

### TC-7: Status Polling Cleanup

**Objective:** Verify polling stops when leaving the tab.

**Prerequisites:**
- Plugin configured with valid Xtream credentials
- Browser Developer Tools open

**Steps:**
1. Navigate to Series tab
2. Open Network tab in Developer Tools
3. Observe requests to `/Xtream/SeriesCacheStatus`
4. Navigate to a different tab (e.g., VOD)
5. Wait 10 seconds
6. Check Network tab for new SeriesCacheStatus requests

**Expected Results:**
- [ ] Requests appear every ~2 seconds while on Series tab
- [ ] Requests STOP after leaving Series tab
- [ ] No memory leak from orphaned intervals

**Status:**

---

### TC-8: Error Display Content

**Objective:** Verify error messages are helpful and actionable.

**Prerequisites:**
- Xtream credentials NOT configured

**Steps:**
1. Navigate to Series tab
2. Read error message content

**Expected Results:**
- [ ] Error message is in red (#ff6b6b)
- [ ] Message mentions checking credentials
- [ ] Message mentions checking server accessibility
- [ ] Message references browser console for details
- [ ] Console shows detailed error (check F12)

**Status:**

---

### TC-9: Graceful Fallback When Status Unavailable

**Objective:** Verify Clear Cache works even if status endpoint fails.

**Prerequisites:**
- Plugin installed

**Steps:**
1. Navigate to Series tab
2. Block requests to `/Xtream/SeriesCacheStatus` (via DevTools)
3. Click "Clear Cache" button
4. Observe confirmation dialog

**Expected Results:**
- [ ] Default confirmation message is shown
- [ ] User can still proceed with clear
- [ ] Clear operation succeeds (if server accessible)
- [ ] Console shows error for status check failure

**Status:**

---

### TC-10: Console Logging for Debugging

**Objective:** Verify debug logs are present for troubleshooting.

**Prerequisites:**
- Plugin configured

**Steps:**
1. Open browser Developer Tools, Console tab
2. Navigate to Series tab
3. Click "Clear Cache" and confirm
4. Review console output

**Expected Results:**
- [ ] "clearCache() called" appears
- [ ] Response status is logged
- [ ] Result object is logged
- [ ] ".finally block - re-enabling button" appears
- [ ] Any errors are logged with stack traces

**Status:**

---

## Performance Tests

### PT-1: Polling Efficiency

**Objective:** Verify polling doesn't degrade performance.

**Steps:**
1. Open Series tab
2. Monitor CPU usage in Task Manager
3. Leave tab open for 5 minutes
4. Check for memory growth

**Expected Results:**
- [ ] CPU usage remains low (<5% attributed to Jellyfin)
- [ ] No significant memory growth
- [ ] UI remains responsive

---

## Test Results Summary

| Test Case | Result | Notes |
|-----------|--------|-------|
| TC-1 | | |
| TC-2 | | |
| TC-3 | | |
| TC-4 | | |
| TC-5 | | |
| TC-6 | | |
| TC-7 | | |
| TC-8 | | |
| TC-9 | | |
| TC-10 | | |
| PT-1 | | |

---

## Known Issues

1. **Polling continues briefly after tab switch** - Due to JavaScript event loop, one more poll may occur after `viewhide`. This is harmless.

2. **Progress bar may show 0% briefly** - If cache is cleared and status is polled before new state propagates.

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Functional requirements
- [Architecture](ARCHITECTURE.md) - UI patterns and data flow
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Changelog](CHANGELOG.md) - Version history
