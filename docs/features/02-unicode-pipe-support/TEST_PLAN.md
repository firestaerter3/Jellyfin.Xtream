# Unicode Pipe Support - Test Plan

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Feature Version:** 0.9.4.x
- **Test Environment:** Jellyfin 10.11.0+, .NET 9.0

---

## Testing Approach

This feature has been tested using both automated logic tests and manual integration testing.

**Note:** The Jellyfin.Xtream project does not currently have a unit test framework configured. The automated tests below were executed as standalone C# verification scripts.

---

## Automated Logic Tests

**Test Date:** 2026-01-27
**Branch:** `feature/unicode-parsing-isolated`
**Result:** **12/12 tests passed (100%)**

| Test | Input | Expected | Result |
|------|-------|----------|--------|
| 1 | `┃NL┃ Breaking Bad` | `Breaking Bad` | PASS |
| 2 | `│HD│ Movie Name` | `Movie Name` | PASS |
| 3 | `｜4K｜ Film Title` | `Film Title` | PASS |
| 4 | `┃ NL ┃ Show Name` | `Show Name` | PASS |
| 5 | `┃NL┃ ┃HD┃ Series Name` | `Series Name` | PASS |
| 6 | `[US] \|EN\| ┃HD┃ The Show` | `The Show` | PASS |
| 7 | `\|HD\| Regular Title` | `Regular Title` | PASS |
| 8 | `[UK] Another Title` | `Another Title` | PASS |
| 9 | `Clean Title Name` | `Clean Title Name` | PASS |
| 10 | `Show Name ┃HD┃` | `Show Name` | PASS |
| 11 | `\| TAG \| Title` | `Title` | PASS |
| 12 | `│ HD │ Movie` | `Movie` | PASS |

**What Was Verified:**
- Heavy vertical pipe (U+2503) correctly stripped
- Light vertical pipe (U+2502) correctly stripped
- Fullwidth vertical line (U+FF5C) correctly stripped
- Whitespace inside tags trimmed properly
- Multiple tags stripped in sequence
- Mixed ASCII and Unicode tags handled
- ASCII pipe regression (still works)
- Bracket tag regression (still works)
- No-tag pass-through works
- Trailing tags stripped

---

## Test Environment Setup

### Prerequisites
- Jellyfin 10.11.0 or later
- Jellyfin Xtream plugin with Unicode pipe support
- Xtream provider with Unicode-tagged content (or manually create test data)

### Test Data Requirements
- Stream names containing `┃TAG┃` patterns
- Stream names containing `│TAG│` patterns
- Stream names containing `｜TAG｜` patterns
- Mix of Unicode and ASCII pipe tags

---

## Manual Testing

### Test Case 1: Heavy Vertical Pipe (┃)

**Objective:** Verify U+2503 Box Drawings Heavy Vertical is recognized

**Test Data:**
- Input: `┃NL┃ Breaking Bad`

**Steps:**
1. Add a series/movie with the test name to your library
2. Navigate to the content in Jellyfin
3. Observe the displayed title

**Expected Result:**
- ✅ Title displays as "Breaking Bad"
- ✅ "┃NL┃" is stripped
- ✅ Content sorts under "B"

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 2: Light Vertical Pipe (│)

**Objective:** Verify U+2502 Box Drawings Light Vertical is recognized

**Test Data:**
- Input: `│HD│ Movie Name`

**Steps:**
1. Add content with the test name
2. View in Jellyfin library
3. Verify title is cleaned

**Expected Result:**
- ✅ Title displays as "Movie Name"
- ✅ "│HD│" is stripped

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 3: Fullwidth Vertical Line (｜)

**Objective:** Verify U+FF5C Fullwidth Vertical Line is recognized

**Test Data:**
- Input: `｜4K｜ Film Title`

**Steps:**
1. Add content with the test name
2. View in Jellyfin library
3. Verify title is cleaned

**Expected Result:**
- ✅ Title displays as "Film Title"
- ✅ "｜4K｜" is stripped

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 4: Whitespace Inside Tags

**Objective:** Verify tags with internal whitespace are handled

**Test Data:**
- Input: `┃ NL ┃ Show Name`

**Steps:**
1. Add content with spaces inside the pipe delimiters
2. View in Jellyfin library
3. Verify title and tag extraction

**Expected Result:**
- ✅ Title displays as "Show Name"
- ✅ Tag extracted as "NL" (trimmed)

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 5: Multiple Unicode Tags

**Objective:** Verify multiple Unicode tags are all stripped

**Test Data:**
- Input: `┃NL┃ ┃HD┃ Series Name`

**Steps:**
1. Add content with multiple Unicode pipe tags
2. View in Jellyfin library
3. Verify all tags are removed

**Expected Result:**
- ✅ Title displays as "Series Name"
- ✅ Both "NL" and "HD" tags extracted

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 6: Mixed ASCII and Unicode Tags

**Objective:** Verify combination of bracket, ASCII pipe, and Unicode pipe tags

**Test Data:**
- Input: `[US] |EN| ┃HD┃ The Show`

**Steps:**
1. Add content with mixed tag formats
2. View in Jellyfin library
3. Verify all tags are removed

**Expected Result:**
- ✅ Title displays as "The Show"
- ✅ Tags extracted: ["US", "EN", "HD"]

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 7: ASCII Pipe Regression

**Objective:** Verify ASCII pipe tags still work (regression test)

**Test Data:**
- Input: `|HD| Regular Title`

**Steps:**
1. Add content with standard ASCII pipe tags
2. View in Jellyfin library
3. Verify tag is stripped

**Expected Result:**
- ✅ Title displays as "Regular Title"
- ✅ Behavior unchanged from before

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 8: Bracket Tag Regression

**Objective:** Verify bracket tags still work (regression test)

**Test Data:**
- Input: `[UK] Another Title`

**Steps:**
1. Add content with bracket tags
2. View in Jellyfin library
3. Verify tag is stripped

**Expected Result:**
- ✅ Title displays as "Another Title"
- ✅ Behavior unchanged from before

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 9: No Tags (Pass-through)

**Objective:** Verify titles without tags are unaffected

**Test Data:**
- Input: `Clean Title Name`

**Steps:**
1. Add content with no tags
2. View in Jellyfin library
3. Verify title is unchanged

**Expected Result:**
- ✅ Title displays as "Clean Title Name"
- ✅ No tags extracted

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 10: Sorting with Unicode Tags

**Objective:** Verify alphabetical sorting uses cleaned titles

**Test Data:**
- Content list:
  - `┃NL┃ Breaking Bad`
  - `Avatar`
  - `┃US┃ The Office`
  - `Dune`

**Steps:**
1. Enable flat view
2. Navigate to series/movies library
3. Observe sort order

**Expected Result:**
- ✅ Sort order: Avatar, Breaking Bad, Dune, The Office
- ✅ Tags don't affect sorting

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 11: Tag at End of Title

**Objective:** Verify tags at the end of titles are handled

**Test Data:**
- Input: `Show Name ┃HD┃`

**Steps:**
1. Add content with tag at end
2. View in Jellyfin library
3. Verify title is cleaned

**Expected Result:**
- ✅ Title displays as "Show Name"
- ✅ Trailing tag removed

**Actual Result:**
- ✅ **PASS** (tested 2026-01-24)

---

### Test Case 12: Only Tags (Edge Case)

**Objective:** Verify handling of input that is only tags

**Test Data:**
- Input: `┃NL┃ ┃HD┃`

**Steps:**
1. Observe behavior (may not be practical to add as content)
2. Check logs for errors

**Expected Result:**
- ✅ Title is empty string (trimmed)
- ✅ No errors or crashes

**Actual Result:**
- ⚠️ **EDGE CASE** - Results in empty title, provider-dependent behavior

---

## Integration Testing

### Integration Test 1: With Flat View

**Objective:** Verify Unicode pipe support works with flat view enabled

**Configuration:**
- FlattenSeriesView = true
- Unicode-tagged content present

**Expected:**
- ✅ All titles cleaned
- ✅ Sorting correct
- ✅ Navigation works

**Result:** ✅ **PASS**

---

### Integration Test 2: With Caching

**Objective:** Verify Unicode pipe support works with caching enabled

**Configuration:**
- EnableSeriesCaching = true
- Unicode-tagged content present

**Expected:**
- ✅ Cached titles are cleaned
- ✅ Cache refresh preserves clean titles

**Result:** ✅ **PASS**

---

## Regression Testing

### Regression Test 1: Existing Content Unchanged

**Objective:** Verify existing ASCII/bracket tagged content still works

**Steps:**
1. Verify existing library content
2. Check no titles have changed unexpectedly
3. Verify sorting unchanged for ASCII/bracket content

**Expected:**
- ✅ No regressions in existing functionality

**Result:** ✅ **PASS**

---

## Test Summary

### Test Results Overview

| Category | Total | Pass | Fail | Skip |
|----------|-------|------|------|------|
| Automated Tests | 12 | 12 | 0 | 0 |
| Manual Tests | 12 | 11 | 0 | 1 |
| Integration Tests | 2 | 2 | 0 | 0 |
| Regression Tests | 1 | 1 | 0 | 0 |
| **Total** | **27** | **26** | **0** | **1** |

**Pass Rate:** 96% (26/27)
**Skipped:** Edge case (only-tags input)

---

## Known Limitations

1. **Empty titles** - If a name consists only of tags, the result is an empty title
2. **Unmatched pipes** - Single pipes without closing are not treated as tags (by design)
3. **Nested tags** - `┃[TAG]┃` may produce unexpected results (edge case)

---

## Test Environment Details

**Tested on:**
- Jellyfin version: 10.11.0
- Plugin version: 0.9.4.x
- .NET version: 9.0
- OS: Docker on Proxmox (Linux)
- Provider: Test IPTV provider with Unicode tags

**Test Date:** 2026-01-24

---

## Recommendations

1. **Real-world testing** - Test with actual IPTV provider content
2. **Provider variety** - Different providers use different Unicode characters
3. **Log monitoring** - Check for regex errors in logs during testing

---

**Test Plan Complete** ✅

---

## References

- [REQUIREMENTS.md](./REQUIREMENTS.md) - Feature requirements
- [ARCHITECTURE.md](./ARCHITECTURE.md) - Design decisions
- [IMPLEMENTATION.md](./IMPLEMENTATION.md) - Code changes
- [CHANGELOG.md](./CHANGELOG.md) - Version history
