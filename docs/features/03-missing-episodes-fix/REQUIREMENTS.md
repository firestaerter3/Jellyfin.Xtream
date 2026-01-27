# Feature 03: Missing Episodes Fix - Requirements

## Document Info
- **Feature:** Missing Episodes Fix
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This feature fixes a bug where episodes would not appear in Jellyfin despite being available in the Xtream API. The issue occurs when the Xtream provider stores episodes under a dictionary key that doesn't match the episode's actual season number.

---

## Problem Statement

### Symptoms
- Series shows correct number of seasons
- One or more seasons show 0 episodes
- Episodes exist in the Xtream provider (visible in other apps like Dispatcharr)
- No errors in logs - episodes silently missing

### Root Cause
The Xtream API returns episodes in a dictionary keyed by season ID:
```json
{
  "episodes": {
    "1": [...],  // Season 1 episodes
    "2": [...]   // Season 2 episodes
  }
}
```

However, some providers store episodes under inconsistent keys:
```json
{
  "episodes": {
    "0": [        // Key is 0, but episodes have Season=1
      { "season": 1, "episode_num": 1, ... },
      { "season": 1, "episode_num": 2, ... }
    ]
  }
}
```

The original code only looked up episodes by dictionary key, missing episodes stored under wrong keys.

---

## User Stories

### US-1: View All Episodes
**As a** Jellyfin user
**I want** to see all episodes for a series
**So that** I can watch any episode available from my provider

**Acceptance Criteria:**
- All episodes returned by Xtream API are visible in Jellyfin
- Episodes appear under their correct season
- No duplicate episodes shown

---

## Functional Requirements

### FR-1: Primary Lookup
- **FR-1.1:** First attempt to get episodes using dictionary key matching seasonId
- **FR-1.2:** If found, add all episodes from that key to results

### FR-2: Fallback Search
- **FR-2.1:** After primary lookup, search ALL dictionary entries
- **FR-2.2:** For each entry, check each episode's `Season` property
- **FR-2.3:** If `episode.Season` matches requested seasonId, include episode
- **FR-2.4:** Skip entries already checked in primary lookup

### FR-3: Duplicate Prevention
- **FR-3.1:** Before adding episode from fallback, check if already in results
- **FR-3.2:** Use `EpisodeId` for duplicate detection
- **FR-3.3:** Never show same episode twice

---

## Non-Functional Requirements

### NFR-1: Performance
- **NFR-1.1:** Fallback search only iterates episodes once
- **NFR-1.2:** No additional API calls required
- **NFR-1.3:** Duplicate check uses efficient lookup

### NFR-2: Backwards Compatibility
- **NFR-2.1:** Fix works with correctly-keyed episodes (no regression)
- **NFR-2.2:** No configuration changes required
- **NFR-2.3:** No database migration needed

---

## Test Cases

| ID | Scenario | Expected Result |
|----|----------|-----------------|
| TC-1 | Episodes correctly keyed (key=1, season=1) | Episodes appear normally |
| TC-2 | Episodes miskeyed (key=0, season=1) | Episodes still appear under Season 1 |
| TC-3 | Mixed keys (some correct, some wrong) | All episodes appear, no duplicates |
| TC-4 | Empty episodes dictionary | Empty result, no errors |

---

## Dependencies

- Xtream API returning `SeriesStreamInfo` with `Episodes` dictionary
- Episode objects containing `Season` property
- Episode objects containing `EpisodeId` for deduplication

---

## Related Documentation

- [Architecture](ARCHITECTURE.md) - Design decisions
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history

---

## References

- `Service/StreamService.cs:340-388` - GetEpisodes method
- PR_PROPOSAL.md - PR 1: Missing Episodes Fix
