# Feature 05: Malformed JSON Handling - Test Plan

## Document Info
- **Feature:** Malformed JSON Handling
- **Version:** 0.9.5.3
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document describes the test cases for verifying malformed JSON handling.

---

## Test Environment

- Jellyfin server with plugin installed
- Access to Jellyfin logs
- Xtream provider (ideally one with known malformed responses)

---

## Test Cases

### TC-1: Normal Response (Regression Test)

**Objective:** Verify normal responses still deserialize correctly.

**Prerequisites:**
- Plugin configured with valid Xtream credentials
- Series with valid data available

**Steps:**
1. Browse to a series with seasons and episodes
2. Expand seasons
3. View episode list

**Expected Results:**
- [ ] Series info loads correctly
- [ ] Seasons appear
- [ ] Episodes appear
- [ ] No warnings in logs about malformed JSON

**Status:**

---

### TC-2: Empty Array Response

**Objective:** Verify empty array `[]` is handled gracefully.

**Prerequisites:**
- Series that returns empty array (or mock/test scenario)

**Steps:**
1. Trigger load of series that returns `[]`
2. Check Jellyfin logs
3. Verify UI behavior

**Expected Results:**
- [ ] No crash or exception
- [ ] Warning logged: "returned array instead of object"
- [ ] Series appears empty (no seasons/episodes)
- [ ] Other series unaffected

**Status:**

---

### TC-3: Non-Empty Array Response

**Objective:** Verify non-empty array `[{...}]` is handled.

**Prerequisites:**
- Series that returns array with data (unusual edge case)

**Steps:**
1. Trigger load of series returning `[{"key": "value"}]`
2. Check logs
3. Verify behavior

**Expected Results:**
- [ ] No crash
- [ ] Warning logged
- [ ] Returns empty SeriesStreamInfo
- [ ] Plugin continues functioning

**Status:**

---

### TC-4: Whitespace Before Array

**Objective:** Verify leading whitespace doesn't break detection.

**Prerequisites:**
- Response with whitespace: `  []` or `\n[]`

**Steps:**
1. Trigger load of series with whitespace-prefixed response
2. Check logs

**Expected Results:**
- [ ] Whitespace is trimmed
- [ ] Array is detected
- [ ] Handled gracefully

**Status:**

---

### TC-5: Cache Refresh Resilience

**Objective:** Verify cache refresh completes despite malformed data.

**Prerequisites:**
- At least one series with malformed response
- Series caching enabled

**Steps:**
1. Trigger cache refresh (Refresh Now button)
2. Monitor progress
3. Wait for completion
4. Check logs

**Expected Results:**
- [ ] Refresh doesn't abort
- [ ] Progress continues past malformed series
- [ ] Refresh completes successfully
- [ ] Log shows which series had issues
- [ ] Other series are cached correctly

**Status:**

---

### TC-6: List Types Not Affected

**Objective:** Verify List<> types still work with array responses.

**Prerequisites:**
- Plugin configured with valid credentials

**Steps:**
1. Load category list (returns array)
2. Load series list (returns array)
3. Load VOD list (returns array)

**Expected Results:**
- [ ] Categories load (array expected and handled)
- [ ] Series list loads
- [ ] VOD list loads
- [ ] No false positive warnings

**Status:**

---

### TC-7: Error Logging Quality

**Objective:** Verify log messages are useful for debugging.

**Prerequisites:**
- Series that triggers malformed response

**Steps:**
1. Trigger malformed response
2. Find warning in logs
3. Verify log content

**Expected Results:**
- [ ] Log level is Warning (not Error)
- [ ] URL is included in message
- [ ] Series ID can be extracted from URL
- [ ] Message clearly describes the issue

**Status:**

---

### TC-8: Concurrent Requests

**Objective:** Verify handling works under concurrent load.

**Prerequisites:**
- Multiple series, some with malformed responses

**Steps:**
1. Start cache refresh (processes many series)
2. Simultaneously browse other series
3. Monitor for errors

**Expected Results:**
- [ ] No race conditions
- [ ] Both operations complete
- [ ] Malformed responses handled independently
- [ ] No deadlocks

**Status:**

---

## Diagnostic Procedures

### Finding Malformed Series

1. Enable debug logging in Jellyfin
2. Trigger cache refresh
3. Search logs for "returned array instead of object"
4. Extract series ID from logged URL

### Log Search Pattern

```bash
grep "returned array instead of object" jellyfin.log
```

### Verifying Fix

Before fix:
```
JsonSerializationException: Cannot deserialize the current JSON array...
```

After fix:
```
Xtream API returned array instead of object for SeriesStreamInfo (URL: ...)
```

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

---

## Known Affected Series

Document any series known to return malformed responses:

| Series ID | Provider | Response Type |
|-----------|----------|---------------|
| | | Empty array `[]` |
| | | |

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Architecture](ARCHITECTURE.md) - Detection design
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Changelog](CHANGELOG.md) - Version history
