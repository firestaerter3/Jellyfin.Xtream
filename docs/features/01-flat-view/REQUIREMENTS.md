# Flat View Feature - Requirements

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Implemented in:** v0.9.1.0 (Series), v0.9.1.0 (VOD)
- **Related:** [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## 1. Executive Summary

### 1.1 Problem Statement

**User Pain Points:**
- **Excessive Navigation**: Users must navigate through multiple category folders to find content
  - Example: Home → Series → Categories → Action → Series List → Show Name
  - This requires 5 clicks just to reach a show
- **Poor Discovery**: Content is scattered across categories, making browsing tedious
- **Category Fatigue**: Large libraries with 10+ categories become overwhelming
- **Search Alternative**: Many users skip navigation entirely and use Jellyfin search instead

**Specific Scenario:**
> "I have 200 TV series across 12 categories. To browse my library, I have to:
> 1. Open Jellyfin Series channel
> 2. See 12 category folders (Action, Comedy, Drama, etc.)
> 3. Click each category one-by-one
> 4. Scroll through shows in each category
> 5. Go back and repeat for next category
>
> This is exhausting. I just want to see all my shows in one alphabetical list."

### 1.2 Solution

Implement **Flat View** feature that bypasses category folders and displays all content directly:

**Before Flat View:**
```
Series Channel
├── Action (folder)
│   ├── Breaking Bad
│   ├── The Wire
│   └── ...
├── Comedy (folder)
│   ├── The Office
│   ├── Parks and Rec
│   └── ...
└── Drama (folder)
    ├── Mad Men
    └── ...
```

**After Flat View (enabled):**
```
Series Channel
├── Breaking Bad
├── Mad Men
├── Parks and Rec
├── The Office
├── The Wire
└── ... (all series alphabetically sorted)
```

**Impact:** 5-click navigation reduced to 2 clicks (Home → Series → Show)

### 1.3 Key Benefits

1. **Faster Browsing**: Eliminate category navigation overhead
2. **Better Discovery**: See entire library at once, sorted alphabetically
3. **Consistent UX**: Matches how most streaming services display content
4. **Optional**: Can be disabled for users who prefer category organization

---

## 2. User Stories

### US-1: Quick Series Access
**As a** Jellyfin user with a large series library
**I want to** see all my TV shows in one alphabetical list
**So that** I can quickly find and play content without clicking through categories

**Acceptance Criteria:**
- When FlattenSeriesView is enabled, all series appear in a single list
- Series are sorted alphabetically by title
- Categories are not shown as folders
- Clicking a series goes directly to seasons/episodes

---

### US-2: Quick Movie Access
**As a** Jellyfin user with a large movie library
**I want to** see all my movies in one alphabetical list
**So that** I can quickly browse and select movies without category navigation

**Acceptance Criteria:**
- When FlattenVodView is enabled, all movies appear in a single list
- Movies are sorted alphabetically by title
- Categories are not shown as folders
- Clicking a movie plays it directly

---

### US-3: Category Preservation (When Disabled)
**As a** user who prefers organized categories
**I want to** disable flat view and browse by category
**So that** I can maintain my preferred navigation structure

**Acceptance Criteria:**
- When FlattenSeriesView/FlattenVodView is disabled, categories appear as folders
- Original category-based navigation works unchanged
- No performance degradation compared to pre-flat-view versions

---

### US-4: Easy Configuration
**As a** Jellyfin administrator
**I want to** easily toggle flat view on/off from plugin settings
**So that** I can choose the navigation style without modifying code

**Acceptance Criteria:**
- Checkbox in plugin settings for "Flatten Series View"
- Checkbox in plugin settings for "Flatten VOD View"
- Changes take effect immediately (no restart required)
- Clear description of what each setting does

---

## 3. Functional Requirements

### FR-1: Flat Series View

**ID:** FR-1
**Priority:** High
**Description:** When enabled, bypass category folders and display all series directly.

**Acceptance Criteria:**
- [ ] Configuration option `FlattenSeriesView` (boolean, default: false)
- [ ] When enabled, `GetChannelItems("")` returns all series (not categories)
- [ ] Series from all selected categories are aggregated
- [ ] Series are sorted alphabetically by name
- [ ] Duplicate series (same ID) are deduplicated
- [ ] Category selection in settings still filters which series appear

---

### FR-2: Flat VOD View

**ID:** FR-2
**Priority:** High
**Description:** When enabled, bypass category folders and display all movies directly.

**Acceptance Criteria:**
- [ ] Configuration option `FlattenVodView` (boolean, default: false)
- [ ] When enabled, `GetChannelItems("")` returns all movies (not categories)
- [ ] Movies from all selected categories are aggregated
- [ ] Movies are sorted alphabetically by title
- [ ] Duplicate movies (same ID) are deduplicated
- [ ] Category selection in settings still filters which movies appear

---

### FR-3: Cache Integration

**ID:** FR-3
**Priority:** Medium
**Description:** Flat view should leverage caching for performance.

**Acceptance Criteria:**
- [ ] Check cache first for categories and content
- [ ] Fall back to API if cache miss
- [ ] Cache warm-up populates Jellyfin DB for instant browsing
- [ ] FlattenSeriesView setting triggers cache invalidation when changed

---

### FR-4: Backward Compatibility

**ID:** FR-4
**Priority:** Critical
**Description:** Original category-based navigation must work when flat view disabled.

**Acceptance Criteria:**
- [ ] When FlattenSeriesView = false, GetChannelItems("") returns categories
- [ ] When FlattenSeriesView = false, GetChannelItems("<categoryId>") returns series in category
- [ ] When FlattenVodView = false, VOD behaves identically to pre-flat-view versions
- [ ] No API changes that break existing Jellyfin clients

---

## 4. Non-Functional Requirements

### NFR-1: Performance

**ID:** NFR-1
**Description:** Flat view must not degrade performance compared to category view.

**Acceptance Criteria:**
- [ ] Flat view with 200 series: < 2 seconds to load (cache hit)
- [ ] Flat view with 500 series: < 5 seconds to load (cache hit)
- [ ] Memory usage: No more than 2x category view memory
- [ ] API calls: Same number of API calls as category view (when caching disabled)

**Rationale:** Flat view aggregates data that category view would fetch anyway

---

### NFR-2: Usability

**ID:** NFR-2
**Description:** Flat view should be intuitive and match user expectations.

**Acceptance Criteria:**
- [ ] Alphabetical sorting (not arbitrary API order)
- [ ] Title parsing removes tags (e.g., "[US]" prefix stripped)
- [ ] No empty results (if categories empty, show message)
- [ ] Consistent with Jellyfin UI conventions (no custom styling needed)

---

### NFR-3: Scalability

**ID:** NFR-3
**Description:** Flat view must handle large libraries efficiently.

**Acceptance Criteria:**
- [ ] 100 series: Instant load
- [ ] 500 series: < 5 seconds
- [ ] 1000+ series: Loads without errors or timeouts
- [ ] No pagination required (Jellyfin handles virtual scrolling)

---

### NFR-4: Configurability

**ID:** NFR-4
**Description:** Users should control flat view behavior without code changes.

**Acceptance Criteria:**
- [ ] Toggle via UI checkbox (no manual config file editing)
- [ ] Default: Disabled (backward compatible)
- [ ] Changes apply immediately (no Jellyfin restart)
- [ ] Independent controls for Series vs VOD

---

## 5. Configuration Requirements

### CR-1: Plugin Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `FlattenSeriesView` | bool | false | Show all series directly without category folders |
| `FlattenVodView` | bool | false | Show all movies directly without category folders |

### CR-2: Settings Location

**Configuration Files:**
- Backend: `Jellyfin.Xtream/Configuration/PluginConfiguration.cs`
- UI: `Jellyfin.Xtream/Configuration/Web/XtreamSeries.html` (series checkbox)
- UI: `Jellyfin.Xtream/Configuration/Web/XtreamVod.html` (VOD checkbox)

### CR-3: Category Selection Interaction

**Important:** Flat view respects category selection settings

**Example:**
```
Selected Categories: Action, Comedy (Drama unchecked)
FlattenSeriesView: Enabled

Result: Shows all series from Action + Comedy categories only
        (Drama series not shown even in flat view)
```

---

## 6. Error Handling Requirements

### EH-1: Empty Library

| Scenario | Behavior | User Impact |
|----------|----------|-------------|
| No categories selected | Show empty channel | User sees "No items" in Jellyfin |
| Selected categories empty | Show empty channel | User sees "No items" in Jellyfin |
| API returns no series | Show empty channel | User sees "No items" in Jellyfin |

### EH-2: API Errors

| Error | Handling | User Impact |
|-------|----------|-------------|
| Category fetch fails | Log error, return empty | Channel appears empty, error in logs |
| Series fetch fails (one category) | Skip category, continue others | Partial results shown |
| Series fetch fails (all categories) | Log error, return empty | Channel appears empty |

### EH-3: Malformed Data

| Issue | Handling | User Impact |
|-------|----------|-------------|
| Series missing name | Use SeriesId as name | Series appears with ID as title |
| Series missing ID | Skip series, log warning | Series not shown |
| Duplicate series IDs | Deduplicate by ID | Only one instance shown |

---

## 7. UI/UX Requirements

### UX-1: Settings UI

**Series Tab (`XtreamSeries.html`):**
```html
<label>
  <input type="checkbox" id="FlattenSeriesView" />
  Show all series directly without category folders
  <br><small>When enabled, all series from selected categories appear in one alphabetical list.</small>
</label>
```

**VOD Tab (`XtreamVod.html`):**
```html
<label>
  <input type="checkbox" id="FlattenVodView" />
  Show all movies directly without category folders
  <br><small>When enabled, all movies from selected categories appear in one alphabetical list.</small>
</label>
```

### UX-2: Jellyfin Library View

**Series Channel (Flat View Enabled):**
- Grid view with series posters
- Alphabetical order (A-Z)
- Clicking series → goes to series details (seasons/episodes)

**Series Channel (Flat View Disabled):**
- Grid view with category folders
- Clicking category → shows series in that category
- Clicking series → goes to series details

**VOD Channel (Flat View Enabled):**
- Grid view with movie posters
- Alphabetical order (A-Z)
- Clicking movie → plays movie

**VOD Channel (Flat View Disabled):**
- Grid view with category folders
- Clicking category → shows movies in that category
- Clicking movie → plays movie

---

## 8. Dependencies

### Dependency 1: Title Parsing (ParseName)
- **Location:** `Service/StreamService.cs:ParseName()`
- **Purpose:** Strips tags like `[US]`, `|HD|`, `┃NL┃` from titles
- **Impact on Flat View:** Ensures clean, consistent titles in alphabetical list
- **Example:**
  - Raw API title: `[US] Breaking Bad ┃HD┃`
  - Parsed title: `Breaking Bad`
  - Sorted correctly with other "B" titles

### Dependency 2: Series Caching
- **Location:** `Service/SeriesCacheService.cs`
- **Purpose:** Pre-fetches all series data for fast flat view loading
- **Impact:** Flat view with 200 series loads in ~500ms (cache) vs ~20s (API)
- **Note:** Flat view works without caching (slower)

### Dependency 3: Category Selection
- **Location:** `Configuration/PluginConfiguration.cs:Series` dictionary
- **Purpose:** Defines which categories to include
- **Impact:** Flat view respects category filters
- **Example:** If "Kids" category unchecked, kids shows don't appear in flat view

---

## 9. Future Considerations

### 9.1 Potential Enhancements

1. **Genre-Based Flat View**
   - **Idea:** Flatten by genre instead of categories
   - **Use Case:** User wants "All Action" from all categories
   - **Complexity:** Requires genre metadata parsing

2. **Search/Filter Integration**
   - **Idea:** Add search box in flat view
   - **Use Case:** User has 500 series, wants to filter by keyword
   - **Complexity:** Jellyfin may already provide this in UI

3. **Custom Sort Options**
   - **Idea:** Sort by date added, rating, year, etc.
   - **Use Case:** User wants to see newest shows first
   - **Complexity:** Requires additional metadata from API

4. **Hybrid View**
   - **Idea:** Categories as collapsible sections, not separate folders
   - **Use Case:** User wants organization but not navigation overhead
   - **Complexity:** Jellyfin channel API may not support sections

### 9.2 Known Limitations

1. **No Pagination**: Jellyfin channels don't support pagination
   - **Impact:** Very large libraries (1000+ items) may load slowly
   - **Mitigation:** Jellyfin UI uses virtual scrolling

2. **No Search**: Flat view doesn't add built-in search
   - **Impact:** Users must scroll or use Jellyfin's global search
   - **Mitigation:** Jellyfin provides search across all channels

3. **No Custom Grouping**: Can't group by genre, year, etc. in flat view
   - **Impact:** All content in one flat list
   - **Mitigation:** User can disable flat view and use categories

4. **Category Names Lost**: In flat view, user can't see which category a series belongs to
   - **Impact:** No visual indication of content organization
   - **Mitigation:** Tags (if parsed from title) may provide hints

---

## 10. Success Criteria

### Definition of Done

**Feature Complete When:**
- [ ] FlattenSeriesView setting implemented
- [ ] FlattenVodView setting implemented
- [ ] GetAllSeriesFlattened() method implemented
- [ ] GetAllStreamsFlattened() method implemented
- [ ] UI checkboxes added to configuration pages
- [ ] Alphabetical sorting working correctly
- [ ] Cache integration functional
- [ ] Backward compatibility verified
- [ ] Manual testing passed (all test cases in TEST_PLAN.md)
- [ ] Documentation complete

### User Acceptance Criteria

**Users Should Be Able To:**
- [ ] Enable flat view via plugin settings checkbox
- [ ] Browse all series/movies in one alphabetical list
- [ ] Find content faster than category navigation
- [ ] Disable flat view to restore category navigation
- [ ] Use flat view with or without caching enabled

### Performance Acceptance

**Benchmarks (200 series library):**
- [ ] Flat view load time: < 2s (cache hit)
- [ ] Flat view load time: < 30s (cache miss)
- [ ] No memory leaks after browsing
- [ ] No Jellyfin crashes

---

## 11. Out of Scope

**Explicitly NOT Included:**
- Custom sorting options (only alphabetical)
- Search/filter UI within flat view
- Genre-based flattening
- Multi-level hierarchies (categories + genres)
- Pagination for very large libraries
- Custom grouping/sections in flat view
- Export/import of flat view settings
- Per-user flat view preferences (plugin settings are global)

---

## 12. Glossary

| Term | Definition |
|------|------------|
| **Flat View** | Display mode that shows all content directly without category folders |
| **Category View** | Default display mode with category folders (pre-flat-view behavior) |
| **Jellyfin Channel** | Plugin-provided content source (Live TV, Series, VOD) |
| **Channel Item** | Individual piece of content shown in Jellyfin (series, movie, category) |
| **FolderId** | Jellyfin's parameter for navigation hierarchy (empty = root, non-empty = subfolder) |
| **ParsedName** | Title with tags stripped (e.g., `[US]` removed) for clean display |

---

## 13. References

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Flat view architecture and design decisions
- [IMPLEMENTATION.md](./IMPLEMENTATION.md) - Implementation details and code changes
- [TEST_PLAN.md](./TEST_PLAN.md) - Manual test cases
- Jellyfin Channel Plugin Guide: https://jellyfin.org/docs/general/server/plugins/channels/
- Xtream Codes API Reference: (provider-specific)
