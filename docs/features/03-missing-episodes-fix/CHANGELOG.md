# Feature 03: Missing Episodes Fix - Changelog

## Document Info
- **Feature:** Missing Episodes Fix
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Version History

### v0.9.4.x (2026-01-XX)

#### Bug Fix

**Missing Episodes Not Displaying**
- **Problem:** Episodes would not appear in Jellyfin despite existing in Xtream API
- **Affected Users:** Users with providers that store episodes under incorrect dictionary keys
- **Root Cause:** `GetEpisodes` method only looked up episodes by dictionary key, not by the episode's actual `Season` property
- **Symptom:** Seasons showed 0 episodes when they should have had content

**Solution:**
- Implemented two-phase lookup in `GetEpisodes` method
- Phase 1: Fast dictionary lookup by seasonId key (handles correct providers)
- Phase 2: Fallback search by `episode.Season` property (handles broken providers)
- Added deduplication to prevent same episode appearing twice

**Files Changed:**
- `Service/StreamService.cs` - GetEpisodes method (lines 340-388)

**Technical Details:**
```csharp
// Phase 1: Try dictionary key
if (series.Episodes.TryGetValue(seasonId, out var episodes))
    // Add episodes...

// Phase 2: Search all entries by Season property
foreach (var kvp in series.Episodes)
    if (episode.Season == seasonId && !alreadyAdded)
        // Add episode...
```

---

## Discovery

- **Reported:** User noticed missing episodes for "Ali Bouali" series
- **Comparison:** Dispatcharr showed 4 episodes, Jellyfin showed 0
- **Investigation:** Revealed Xtream API stored episodes under key=0 instead of key=1

---

## Impact

| Metric | Value |
|--------|-------|
| Users Affected | All users with affected providers |
| Severity | Medium (data not displayed, but not lost) |
| Risk | Low (additive fix, no regression potential) |

---

## Migration Notes

No migration required. Fix is automatic and backwards compatible.

---

## Open Questions (from PR_PROPOSAL.md)

> **Was this a real API structure issue or caching issue?**

Evidence suggests real API structure issue:
- Episodes appeared after fix was implemented
- No cache-related changes were made
- Behavior was consistent (not intermittent)

However, cannot be 100% certain without:
1. Testing original code against affected series
2. Inspecting actual API response
3. Confirming refresh wouldn't have fixed it

**Recommendation:** Keep the fix regardless - it's defensive and helps in both cases.

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Architecture](ARCHITECTURE.md) - Design decisions
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [PR Proposal](../../upstream/PR_PROPOSAL.md) - PR 1 details
