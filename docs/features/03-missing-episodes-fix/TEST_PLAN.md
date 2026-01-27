# Feature 03: Missing Episodes Fix - Test Plan

## Document Info
- **Feature:** Missing Episodes Fix
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document describes the test cases for verifying the missing episodes fix.

---

## Test Environment

- Jellyfin server with plugin installed
- Xtream provider with series content
- Access to provider's data via alternative app (e.g., Dispatcharr) for comparison

---

## Test Cases

### TC-1: Correctly Keyed Episodes (Regression Test)

**Objective:** Verify fix doesn't break providers with correct episode keys.

**Prerequisites:**
- Series with episodes correctly keyed (key matches season)

**Steps:**
1. Browse to a series
2. Select a season
3. View episode list

**Expected Results:**
- [ ] All episodes appear
- [ ] Episodes in correct order
- [ ] No duplicates
- [ ] No performance degradation

**Status:**

---

### TC-2: Miskeyed Episodes (Bug Fix Verification)

**Objective:** Verify episodes stored under wrong key are found.

**Prerequisites:**
- Series with episodes stored under wrong dictionary key
- Alternative app showing correct episode count for comparison

**Steps:**
1. In alternative app (e.g., Dispatcharr), note episode count for a series/season
2. In Jellyfin, browse to the same series
3. Select the same season
4. Count episodes shown

**Expected Results:**
- [ ] Episode count matches alternative app
- [ ] All episodes are playable
- [ ] Episodes are in correct order (by episode number)

**Status:**

---

### TC-3: Mixed Correct and Incorrect Keys

**Objective:** Verify both lookup methods work together.

**Prerequisites:**
- Series with some seasons keyed correctly, others incorrectly

**Steps:**
1. Browse series with multiple seasons
2. Check each season's episode count
3. Compare with alternative app

**Expected Results:**
- [ ] All seasons show correct episode counts
- [ ] No duplicates in any season
- [ ] Episodes from both lookup methods appear

**Status:**

---

### TC-4: Empty Episodes Dictionary

**Objective:** Verify graceful handling of series with no episodes.

**Prerequisites:**
- Series with empty episodes (or series placeholder)

**Steps:**
1. Browse to series with no episodes
2. Select a season (if any)

**Expected Results:**
- [ ] Empty episode list shown (no error)
- [ ] UI handles empty state gracefully
- [ ] No exceptions in logs

**Status:**

---

### TC-5: Season Property Value of 0

**Objective:** Verify episodes with Season=0 are handled correctly.

**Prerequisites:**
- Series with episodes that have Season=0 (specials, pilots, etc.)

**Steps:**
1. Browse series that has a "Season 0" or "Specials"
2. View episodes

**Expected Results:**
- [ ] Season 0 episodes appear under Season 0
- [ ] Not mixed with other seasons
- [ ] All specials are visible

**Status:**

---

### TC-6: Large Series (Performance Test)

**Objective:** Verify no performance issues with many episodes.

**Prerequisites:**
- Series with 100+ episodes across multiple seasons

**Steps:**
1. Browse to large series
2. Time how long season list takes to load
3. Time how long episode list takes to load

**Expected Results:**
- [ ] Season list loads in <2 seconds
- [ ] Episode list loads in <2 seconds
- [ ] No timeout errors
- [ ] Memory usage remains stable

**Status:**

---

### TC-7: Episode Playback After Fix

**Objective:** Verify found episodes are actually playable.

**Prerequisites:**
- Series with previously missing episodes

**Steps:**
1. Browse to series that previously had missing episodes
2. Select an episode that was missing before
3. Attempt to play the episode

**Expected Results:**
- [ ] Episode starts playing
- [ ] No playback errors
- [ ] Correct episode plays (not wrong one)

**Status:**

---

### TC-8: Duplicate Prevention

**Objective:** Verify no duplicate episodes appear.

**Prerequisites:**
- Series where same episode might appear under multiple keys

**Steps:**
1. Browse series
2. Select season
3. Look for any duplicate episode entries

**Expected Results:**
- [ ] Each episode appears exactly once
- [ ] No duplicate titles
- [ ] No duplicate episode numbers

**Status:**

---

## Diagnostic Steps

### How to Identify Affected Series

1. Compare episode counts between Jellyfin and alternative app
2. If Jellyfin shows fewer episodes, the series is affected
3. Check Jellyfin logs for episode retrieval patterns

### Log Analysis

Enable debug logging and look for:
```
GetEpisodes called for seriesId=X, seasonId=Y
Phase 1: Found N episodes by key
Phase 2: Found M additional episodes by Season property
```

(Note: Actual log messages depend on implementation)

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

Document any series known to have this issue for regression testing:

| Series Name | Provider | Seasons Affected |
|-------------|----------|------------------|
| Ali Bouali | [Provider] | Season 1 |
| | | |

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Architecture](ARCHITECTURE.md) - Design decisions
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Changelog](CHANGELOG.md) - Version history
