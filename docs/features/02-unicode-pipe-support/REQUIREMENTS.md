# Unicode Pipe Support - Requirements

## Document Info
- **Status:** Implemented
- **Version:** 0.9.4.x
- **Last Updated:** 2026-01-27
- **Related:** [ARCHITECTURE.md](./ARCHITECTURE.md), [IMPLEMENTATION.md](./IMPLEMENTATION.md)

---

## 1. Overview

### Problem Statement

IPTV providers often use Unicode box-drawing characters instead of ASCII pipes to delimit tags in stream names. The original tag parsing regex only handled ASCII pipes (`|`), causing titles like `┃NL┃ Breaking Bad` to display with the tags intact rather than being cleaned to `Breaking Bad`.

### Solution

Enhance the `ParseName()` regex to recognize Unicode pipe variants in addition to the ASCII pipe character.

---

## 2. User Stories

### US-01: Clean Display of Unicode-Tagged Content
**As a** Jellyfin user browsing IPTV content
**I want** series and movie names to be cleaned of Unicode pipe tags
**So that** I see clean titles like "Breaking Bad" instead of "┃NL┃ Breaking Bad"

**Acceptance Criteria:**
- Tags using `┃` (U+2503) are stripped from titles
- Tags using `│` (U+2502) are stripped from titles
- Tags using `｜` (U+FF5C) are stripped from titles
- Tags with spaces like `┃ NL ┃` are handled correctly
- Stripped tags are preserved for metadata purposes

### US-02: Consistent Sorting
**As a** user with flat view enabled
**I want** titles to be sorted by their clean names
**So that** "┃NL┃ Breaking Bad" sorts under "B", not under special characters

**Acceptance Criteria:**
- Alphabetical sorting uses the cleaned title
- Unicode-tagged titles sort alongside non-tagged titles correctly

---

## 3. Functional Requirements

### FR-01: Unicode Pipe Character Recognition
**Requirement:** The tag parser shall recognize the following pipe characters:

| Character | Unicode | Name |
|-----------|---------|------|
| `\|` | U+007C | Vertical Line (ASCII pipe) |
| `│` | U+2502 | Box Drawings Light Vertical |
| `┃` | U+2503 | Box Drawings Heavy Vertical |
| `｜` | U+FF5C | Fullwidth Vertical Line |

### FR-02: Whitespace Handling
**Requirement:** The tag parser shall handle optional whitespace inside pipe-delimited tags.

**Examples:**
- `|TAG|` → Tag: "TAG"
- `| TAG |` → Tag: "TAG"
- `┃NL┃` → Tag: "NL"
- `┃ NL ┃` → Tag: "NL"

### FR-03: Mixed Delimiter Support
**Requirement:** The tag parser shall handle tags with mismatched opening/closing delimiters from the same character class.

**Examples:**
- `|TAG┃` → Tag: "TAG" (mixed ASCII and heavy vertical)
- `│TAG|` → Tag: "TAG" (mixed light vertical and ASCII)

### FR-04: Tag Preservation
**Requirement:** Stripped tags shall be preserved and returned in the `ParsedName.Tags` collection.

**Example:**
- Input: `┃NL┃ ┃HD┃ Breaking Bad`
- Output Title: `Breaking Bad`
- Output Tags: `["NL", "HD"]`

### FR-05: Backward Compatibility
**Requirement:** Existing functionality for ASCII pipe tags and bracket tags shall remain unchanged.

**Examples:**
- `[US] Show Name` → Title: "Show Name", Tags: ["US"]
- `|HD| Movie` → Title: "Movie", Tags: ["HD"]

---

## 4. Non-Functional Requirements

### NFR-01: Performance
**Requirement:** The regex change shall not significantly impact parsing performance.

**Benchmark:** < 1ms per title parse (same as before)

### NFR-02: Maintainability
**Requirement:** The regex pattern shall be documented with Unicode code points for clarity.

---

## 5. Test Cases

See [TEST_PLAN.md](./TEST_PLAN.md) for detailed test cases.

### Summary of Test Scenarios

| Input | Expected Title | Expected Tags |
|-------|----------------|---------------|
| `┃NL┃ Breaking Bad` | `Breaking Bad` | `["NL"]` |
| `│HD│ Movie Name` | `Movie Name` | `["HD"]` |
| `｜4K｜ Film Title` | `Film Title` | `["4K"]` |
| `┃ NL ┃ Show` | `Show` | `["NL"]` |
| `[US] \|HD\| Title` | `Title` | `["US", "HD"]` |
| `┃NL┃ ┃HD┃ Title` | `Title` | `["NL", "HD"]` |

---

## 6. Out of Scope

- Parsing Unicode Block Elements characters (already handled separately in upstream code)
- Customizable tag delimiters (not needed)
- Tag filtering or hiding specific tags

---

## 7. Dependencies

- **Upstream:** StreamService.cs ParseName() method
- **No external dependencies**

---

## 8. References

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Design decisions
- [IMPLEMENTATION.md](./IMPLEMENTATION.md) - Code changes
- [TEST_PLAN.md](./TEST_PLAN.md) - Test cases
- [CHANGELOG.md](./CHANGELOG.md) - Version history
