# Feature 03: Missing Episodes Fix - Implementation

## Document Info
- **Feature:** Missing Episodes Fix
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document details the code changes implementing the missing episodes fix.

---

## Files Modified

| File | Changes | Purpose |
|------|---------|---------|
| `Service/StreamService.cs` | ~35 lines added | Two-phase episode lookup |

---

## Code Changes

### StreamService.cs - GetEpisodes Method (Lines 340-388)

**Before (Original Code):**
```csharp
public async Task<IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>>> GetEpisodes(
    int seriesId, int seasonId, CancellationToken cancellationToken)
{
    SeriesStreamInfo series = await xtreamClient.GetSeriesStreamsBySeriesAsync(...);

    // ... authorization check ...

    Season? season = series.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);

    // PROBLEM: Only looks up by dictionary key
    if (!series.Episodes.TryGetValue(seasonId, out var episodes))
    {
        return new List<...>();  // Returns empty if key doesn't exist
    }

    return episodes.Select(e => new Tuple<...>(series, season, e));
}
```

**After (Fixed Code):**
```csharp
public async Task<IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>>> GetEpisodes(
    int seriesId, int seasonId, CancellationToken cancellationToken)
{
    SeriesStreamInfo series = await xtreamClient.GetSeriesStreamsBySeriesAsync(
        Plugin.Instance.Creds, seriesId, cancellationToken).ConfigureAwait(false);

    int categoryId = series.Info.CategoryId;
    if (!IsConfigured(Plugin.Instance.Configuration.Series, categoryId, seriesId))
    {
        return new List<Tuple<SeriesStreamInfo, Season?, Episode>>();
    }

    Season? season = series.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);

    List<Tuple<SeriesStreamInfo, Season?, Episode>> result = new();

    if (series.Episodes != null)
    {
        // Phase 1: Primary lookup by dictionary key
        if (series.Episodes.TryGetValue(seasonId, out var episodes)
            && episodes != null && episodes.Count > 0)
        {
            foreach (var episode in episodes)
            {
                result.Add(new Tuple<SeriesStreamInfo, Season?, Episode>(
                    series, season, episode));
            }
        }

        // Phase 2: Fallback search by episode.Season property
        // Handles cases where episodes are stored under wrong dictionary key
        foreach (var kvp in series.Episodes)
        {
            if (kvp.Value != null && kvp.Key != seasonId) // Skip already-checked key
            {
                foreach (var episode in kvp.Value)
                {
                    // Match by Season property, not dictionary key
                    if (episode.Season == seasonId)
                    {
                        // Avoid duplicates by checking EpisodeId
                        if (!result.Any(r => r.Item3.EpisodeId == episode.EpisodeId))
                        {
                            result.Add(new Tuple<SeriesStreamInfo, Season?, Episode>(
                                series, season, episode));
                        }
                    }
                }
            }
        }
    }

    return result;
}
```

---

## Key Implementation Details

### 1. Null Safety

```csharp
if (series.Episodes != null)
{
    // ... process episodes
}
```

Prevents NullReferenceException if API returns null Episodes.

### 2. Phase 1: Primary Lookup

```csharp
if (series.Episodes.TryGetValue(seasonId, out var episodes)
    && episodes != null && episodes.Count > 0)
```

- Uses dictionary key for O(1) lookup
- Handles most providers correctly
- Additional null/empty checks for robustness

### 3. Phase 2: Fallback Search

```csharp
foreach (var kvp in series.Episodes)
{
    if (kvp.Value != null && kvp.Key != seasonId) // Skip already-checked
```

- Iterates ALL dictionary entries
- Skips the key already checked in Phase 1
- Checks each episode's `Season` property

### 4. Deduplication

```csharp
if (!result.Any(r => r.Item3.EpisodeId == episode.EpisodeId))
{
    result.Add(...);
}
```

- Uses `EpisodeId` for identity comparison
- Prevents same episode appearing twice
- Handles edge case of episode in multiple keys

---

## Example Scenario

**Series: "Ali Bouali"**
- seriesId: 12345
- User browses Season 1 (seasonId: 1)

**API Response:**
```json
{
  "episodes": {
    "0": [
      {"id": 101, "season": 1, "episode_num": 1, "title": "Ep 1"},
      {"id": 102, "season": 1, "episode_num": 2, "title": "Ep 2"},
      {"id": 103, "season": 1, "episode_num": 3, "title": "Ep 3"},
      {"id": 104, "season": 1, "episode_num": 4, "title": "Ep 4"}
    ]
  }
}
```

**Execution:**

1. **Phase 1:** `TryGetValue(1, ...)` → Key 1 not found → result = []

2. **Phase 2:** Iterate all keys
   - Key 0 (≠ seasonId 1): Check each episode
     - Episode 101: Season=1 ✓ → Add to result
     - Episode 102: Season=1 ✓ → Add to result
     - Episode 103: Season=1 ✓ → Add to result
     - Episode 104: Season=1 ✓ → Add to result

3. **Result:** 4 episodes returned

**Before fix:** 0 episodes (key 1 not found)
**After fix:** 4 episodes (found via Season property)

---

## Testing Verification

To verify the fix works:

1. Enable debug logging in Jellyfin
2. Browse a series known to have the issue
3. Check logs for episode retrieval
4. Verify episodes appear in UI

---

## Backwards Compatibility

The fix is fully backwards compatible:

| Provider Type | Before | After |
|---------------|--------|-------|
| Correct keys | Works | Works (Phase 1) |
| Wrong keys | Missing episodes | Works (Phase 2) |
| Mixed keys | Partial | Works (Both phases) |

No configuration changes or migrations required.

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Architecture](ARCHITECTURE.md) - Design decisions
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history
