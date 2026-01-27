# Feature 03: Missing Episodes Fix - Architecture

## Document Info
- **Feature:** Missing Episodes Fix
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document describes the architectural approach to fixing the missing episodes bug, including data flow and the two-phase lookup strategy.

---

## Data Model

### Xtream API Response Structure

```
SeriesStreamInfo
├── Info: SeriesInfo
│   └── CategoryId, Name, Plot, Cast, etc.
├── Seasons: List<Season>
│   └── SeasonId, Name, SeasonNumber, etc.
└── Episodes: Dictionary<int, List<Episode>>
    ├── Key: int (supposed to be seasonId, but unreliable)
    └── Value: List<Episode>
        └── EpisodeId, Season, EpisodeNum, Title, etc.
```

### The Problem Illustrated

**Expected Structure:**
```
Episodes: {
  1: [Episode(Season=1, Num=1), Episode(Season=1, Num=2)],
  2: [Episode(Season=2, Num=1), Episode(Season=2, Num=2)]
}
```

**Actual Structure (some providers):**
```
Episodes: {
  0: [Episode(Season=1, Num=1), Episode(Season=1, Num=2)],  // Wrong key!
  2: [Episode(Season=2, Num=1), Episode(Season=2, Num=2)]
}
```

---

## Solution Architecture

### Two-Phase Lookup Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                    GetEpisodes(seriesId, seasonId)          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Phase 1: Primary Lookup (Fast Path)                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Episodes.TryGetValue(seasonId, out episodes)         │  │
│  │  If found: Add all episodes to result                 │  │
│  └───────────────────────────────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  Phase 2: Fallback Search (Handles Miskeyed Episodes)       │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  For each (key, episodes) in Episodes dictionary:     │  │
│  │    Skip if key == seasonId (already processed)        │  │
│  │    For each episode in episodes:                      │  │
│  │      If episode.Season == seasonId:                   │  │
│  │        If not already in result (by EpisodeId):       │  │
│  │          Add to result                                │  │
│  └───────────────────────────────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  Return combined result                                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Why Two Phases?

1. **Performance**: Most providers have correct keys. Phase 1 handles these efficiently with O(1) dictionary lookup.

2. **Correctness**: Phase 2 catches miskeyed episodes by checking the actual `Season` property.

3. **No Duplicates**: Skip already-checked keys and use EpisodeId deduplication.

---

## Data Flow

```
User browses Season 1 of "Ali Bouali"
              │
              ▼
┌─────────────────────────────────┐
│  GetEpisodes(seriesId=123,      │
│              seasonId=1)        │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  Fetch SeriesStreamInfo         │
│  from cache or API              │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  Phase 1: TryGetValue(1, ...)   │
│  Key 1 not found → 0 episodes   │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  Phase 2: Search all keys       │
│  Key 0: Episode.Season=1 ✓      │
│       → Add 4 episodes          │
│  Key 2: Episode.Season=2 ✗      │
│       → Skip                    │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  Return 4 episodes for Season 1 │
└─────────────────────────────────┘
```

---

## Design Decisions

### Decision 1: Fallback vs. Primary-Only

**Options Considered:**
1. Only use `episode.Season` property (ignore dictionary key)
2. Only use dictionary key (original behavior)
3. Use both with fallback (chosen)

**Rationale:** Option 3 provides best of both worlds:
- Fast for correct providers (dictionary lookup)
- Correct for broken providers (property check)
- No regression risk

### Decision 2: Deduplication Method

**Options Considered:**
1. HashSet of EpisodeIds
2. LINQ `.Any()` check on result list
3. Skip already-processed keys

**Rationale:** Combined approach:
- Skip processed keys (efficient for large dictionaries)
- LINQ check for actual duplicates (handles edge cases)

### Decision 3: No Configuration Option

**Rationale:**
- Fix is transparent and always beneficial
- No user action required
- No way for user to know if their provider has this issue

---

## Edge Cases

| Case | Handling |
|------|----------|
| Empty Episodes dictionary | Returns empty list |
| Null Episodes dictionary | Short-circuit returns empty |
| Episode in multiple keys | Deduplication prevents duplicates |
| Season property is 0 | Matched when looking for season 0 |
| All episodes under one key | Works - all found in fallback |

---

## Performance Impact

| Operation | Before | After |
|-----------|--------|-------|
| Correct provider | O(1) lookup | O(1) lookup + O(n) scan |
| Broken provider | 0 results | O(n) scan finds all |

The additional O(n) scan is negligible:
- Typical series has <500 episodes total
- Scan only iterates episodes, no API calls
- Phase 2 skips key already checked in Phase 1

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement and requirements
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history
