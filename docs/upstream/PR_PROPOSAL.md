# Pull Request Proposal for Upstream Contribution

This document outlines the proposed PR structure for contributing features and fixes back to the main repository: [Kevinjil/Jellyfin.Xtream](https://github.com/Kevinjil/Jellyfin.Xtream)

## Overview

This fork has added several features and bug fixes that could benefit the main repository:
1. Flat Series View feature
2. Flat VOD View feature  
3. Missing Episodes bug fix
4. Unicode pipe character support in tag parsing
5. Upfront caching with configurable expiration

## Proposed PR Structure

### **PR 1: Bug Fix - Missing Episodes**
**Priority: High (should go first)**

**Changes:**
- `Jellyfin.Xtream/Service/StreamService.cs` - `GetEpisodes` method
  - Added fallback to search all episodes by `Season` property when dictionary lookup fails
  - Handles cases where episodes might be stored under different season ID keys in the API response

**Why separate:**
- Critical bug fix that affects all users (both flat and non-flat views)
- Should be merged quickly and independently
- Low risk, high value

**Files changed:**
- `Jellyfin.Xtream/Service/StreamService.cs`

---

### **PR 2: Bug Fix - Configuration UI Error Handling**
**Priority: High**

**Changes:**
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.js` - Error handling improvements
  - Clear table before showing error to prevent stacking duplicate messages
  - Errors properly cleared when credentials are successfully configured
- `Jellyfin.Xtream/Configuration/Web/XtreamVod.js` - Added error handling
  - Previously failed silently without showing any error messages
  - Now shows helpful error message with troubleshooting steps
- `Jellyfin.Xtream/Configuration/Web/XtreamLive.js` - Added error handling
  - Previously failed silently without showing any error messages
  - Now shows helpful error message with troubleshooting steps

**Why separate:**
- Improves user experience when credentials are not configured
- Prevents confusing UI behavior (stacking errors)
- Makes it easier to troubleshoot configuration issues
- Low risk, high value for all users

**Files changed:**
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.js`
- `Jellyfin.Xtream/Configuration/Web/XtreamVod.js`
- `Jellyfin.Xtream/Configuration/Web/XtreamLive.js`

---

### **PR 3: Enhancement - Unicode Pipe Character Support in Tag Parsing**
**Priority: Medium**

**Changes:**
- `Jellyfin.Xtream/Service/StreamService.cs` - `ParseName` method
  - Updated regex to support Unicode pipe variants: `│` (U+2502), `┃` (U+2503), `｜` (U+FF5C)
  - In addition to ASCII pipe `|` (U+007C)

**Why separate:**
- Small, focused enhancement
- Low risk
- Improves compatibility with various Xtream providers that use Unicode characters

**Files changed:**
- `Jellyfin.Xtream/Service/StreamService.cs`

---

### **PR 4: Feature - Flat View (Series + VOD)**
**Priority: Medium**

**Changes:**
- `Jellyfin.Xtream/Configuration/PluginConfiguration.cs`
  - Add `FlattenSeriesView` property (default: false)
  - Add `FlattenVodView` property (default: false)
- `Jellyfin.Xtream/SeriesChannel.cs`
  - Add `GetAllSeriesFlattened` method
  - Update `GetChannelItems` to check flat view setting
- `Jellyfin.Xtream/VodChannel.cs`
  - Add `GetAllStreamsFlattened` method
  - Update `GetChannelItems` to check flat view setting
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.html`
  - Add checkbox for "Flatten Series View"
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.js`
  - Handle flat view checkbox state
- `Jellyfin.Xtream/Configuration/Web/XtreamVod.html`
  - Add checkbox for "Flatten VOD View"
- `Jellyfin.Xtream/Configuration/Web/XtreamVod.js`
  - Handle flat view checkbox state

**Why separate:**
- Self-contained feature
- Opt-in (backward compatible)
- No breaking changes
- Can be discussed/merged independently

**Files changed:**
- `Jellyfin.Xtream/Configuration/PluginConfiguration.cs`
- `Jellyfin.Xtream/SeriesChannel.cs`
- `Jellyfin.Xtream/VodChannel.cs`
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.html`
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.js`
- `Jellyfin.Xtream/Configuration/Web/XtreamVod.html`
- `Jellyfin.Xtream/Configuration/Web/XtreamVod.js`
- `build.yaml` (version bump)

---

### **PR 5: Feature - Upfront Caching with Configurable Expiration**
**Priority: Low (depends on series functionality being stable)**

**Changes:**
- `Jellyfin.Xtream/Service/SeriesCacheService.cs` (new file)
  - Pre-fetches and caches all series, seasons, and episodes
  - Configurable cache expiration time
- `Jellyfin.Xtream/Plugin.cs`
  - Initialize cache service
  - Trigger cache refresh on plugin load and config changes
- `Jellyfin.Xtream/Configuration/PluginConfiguration.cs`
  - Add `SeriesCacheExpirationMinutes` property (default: 60)
- `Jellyfin.Xtream/SeriesChannel.cs`
  - Use cached data with fallback to API calls
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.html`
  - Add input field for cache expiration (minutes)
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.js`
  - Handle cache expiration input

**Why separate:**
- Larger change (new service)
- Performance feature
- Should be added after core features are stable
- Can be optional/opt-in

**Files changed:**
- `Jellyfin.Xtream/Service/SeriesCacheService.cs` (new)
- `Jellyfin.Xtream/Plugin.cs`
- `Jellyfin.Xtream/Configuration/PluginConfiguration.cs`
- `Jellyfin.Xtream/SeriesChannel.cs`
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.html`
- `Jellyfin.Xtream/Configuration/Web/XtreamSeries.js`
- `build.yaml` (version bump)

---

## PR Order Recommendation

1. **PR 1: Missing Episodes Fix** (Critical bug fix)
2. **PR 2: Configuration UI Error Handling** (High priority UX fix)
3. **PR 3: Unicode Pipe Support** (Small enhancement)
4. **PR 4: Flat View Features** (Main feature)
5. **PR 5: Upfront Caching** (Performance feature)

---

## Open Questions / Action Items

### ⚠️ **ACTION ITEM: Missing Episodes - Real Bug or Caching Issue?**

**Question:** Was the missing episodes issue for "Ali Bouali" (and other series) a real API structure problem or just a caching issue?

**Context:**
- User reported missing episodes in series
- Dispatcharr showed 4 episodes for "Ali Bouali" Season 1
- Episodes were not appearing in Jellyfin
- Fix added fallback search by `episode.Season` property

**Analysis:**
- **If real API structure issue:**
  - Episodes have `episode.Season == 1` but stored under different dictionary key (e.g., key=0, key=2)
  - `GetSeasons` finds seasonId=1 from keys
  - `GetEpisodes(seriesId, 1)` fails because `TryGetValue(1, ...)` doesn't find them
  - Fix is necessary and valuable
  
- **If just caching issue:**
  - Episodes would appear after refresh/restart
  - No code change needed
  - Fix is defensive but not critical

**Evidence:**
- Episodes appeared after the fix was implemented
- However, we can't be 100% certain without:
  1. Testing original code against Ali Bouali series
  2. Checking actual API response structure
  3. Verifying if refresh/restart would have fixed it

**Recommendation:**
- Keep the fix in PR 1 (it's defensive and helps in both cases)
- But note in PR description that this may have been a caching issue
- Or: Test before submitting PR to confirm it's a real bug

**Status:** ⏸️ **TO BE DETERMINED** - Need to test/verify before finalizing PR

---

## Alternative: Combined Approach

If fewer PRs are preferred:

### **Option A: 3 PRs**
1. **PR 1: Bug Fixes** (Missing Episodes + Configuration UI Error Handling + Unicode Support)
2. **PR 2: Flat View Features** (Series + VOD)
3. **PR 3: Upfront Caching**

### **Option B: 2 PRs**
1. **PR 1: Bug Fixes & Enhancements** (Missing Episodes + Error Handling + Unicode + Flat View)
2. **PR 2: Performance** (Upfront Caching)

**Recommendation:** Stick with 5 separate PRs for easier review and independent merging.

---

## Notes

- All features are backward compatible (opt-in via configuration)
- No breaking changes
- Follows existing code patterns and style
- Includes UI updates for configuration
- Tested in production environment

---

## Next Steps

1. ⏸️ **Verify missing episodes issue** - Test if it's real bug or caching
2. Create feature branches from upstream master
3. Cherry-pick/apply changes to each branch
4. Test each PR independently
5. Create PRs with detailed descriptions
6. Submit in recommended order

---

*Last updated: 2026-01-25*
