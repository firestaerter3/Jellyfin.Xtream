# Jellyfin Xtream Plugin - Flat Series View Implementation Plan

**Date:** January 21, 2026  
**Version:** 2.0 (Revised)  
**Goal:** Fork and modify the Jellyfin Xtream plugin to show series directly without category folders

---

## 🎯 Feature Definition

### Current Behavior
```
Xtream Series (Library)
  └── Category Folder (e.g., "| NL | VIDEOLAND")
      └── Series 1 (Show)
          └── Season 1
              └── Episode 1
      └── Series 2 (Show)
```

### Desired Behavior (Flat View)
```
Xtream Series (Library)
  └── Series 1 (Show)
      └── Season 1
          └── Episode 1
  └── Series 2 (Show)
  └── Series 3 (Show)
  (all series from selected categories, no category folders)
```

### Key Clarifications
- ✅ **Remove category folders** - Series appear directly under library root
- ✅ **Keep season/episode hierarchy** - Seasons and episodes remain nested under shows
- ✅ **Respect category selection** - Only show series from enabled categories
- ✅ **Backward compatible** - Default OFF, existing users unaffected

---

## 📊 Pre-Implementation Analysis

### Current Plugin State (v0.8.0)
| Feature | Status |
|---------|--------|
| Series integration | ✅ Working (since v0.8.0) |
| Category selection | ✅ Can enable/disable categories |
| Individual series selection | ✅ Can select series within categories |
| Flat series view | ❌ Not implemented |
| Known issues | Episodes sometimes missing until metadata refresh |

### Existing Issues to Consider
- **#249**: "Series category completely empty"
- **#253**: "Many Series seasons have no episodes until Refresh Metadata"
- **#241**: "Select all" feature request
- No existing PR or fork implements flat view

### Why No Fork Exists Yet
- ~16 forks exist, but none address folder flattening
- Feature is novel and likely valuable to community

---

## 📋 Implementation Plan

### Phase 1: Environment Setup (Day 1)

#### 1.1 Fork & Clone
```bash
# Fork on GitHub first: https://github.com/Kevinjil/Jellyfin.Xtream
git clone https://github.com/YOUR_USERNAME/Jellyfin.Xtream.git
cd Jellyfin.Xtream
git remote add upstream https://github.com/Kevinjil/Jellyfin.Xtream.git
git fetch upstream
git checkout -b feature/flat-series-view
```

#### 1.2 Development Environment
| Requirement | Version |
|-------------|---------|
| .NET SDK | 8.0+ (match Jellyfin server) |
| IDE | Visual Studio 2022 or VS Code + C# Dev Kit |
| Jellyfin Server | Local dev instance for testing |
| Xtream Credentials | Test provider access |

#### 1.3 Verify Build
```bash
dotnet restore
dotnet build -c Debug
# Plugin DLL outputs to bin/Debug/
```

---

### Phase 2: Code Analysis (Day 1-2)

#### 2.1 Key Files to Examine

| File/Pattern | Purpose | What to Look For |
|--------------|---------|------------------|
| `PluginConfiguration.cs` | Settings storage | Where to add `FlattenSeriesView` |
| `*Channel*.cs` | Jellyfin IChannel impl | How library items are created |
| `*Series*.cs` | Series handling | Category→Series mapping |
| `XtreamClient.cs` or similar | API client | How categories/series are fetched |
| `*.html` files | Config UI pages | Where to add toggle |

#### 2.2 Critical Code Paths to Trace
```
User opens "Xtream Series" library
  → Plugin's IChannel.GetChildren() called
    → Fetches categories from Xtream API
    → Creates folder items for each category
    → User clicks category folder
      → GetChildren() called again with folder ID
        → Fetches series for that category
        → Creates series items
```

**Our modification point:** When `FlattenSeriesView = true`, skip category folder creation and return all series directly.

#### 2.3 Commands to Explore Codebase
```bash
# Find channel/provider implementations
grep -r "IChannel" --include="*.cs" .
grep -r "GetChildren" --include="*.cs" .

# Find category handling
grep -r "category" --include="*.cs" -i .
grep -r "folder" --include="*.cs" -i .

# Find configuration
grep -r "PluginConfiguration" --include="*.cs" .

# Find series creation
grep -r "series" --include="*.cs" -i | grep -i "create\|item\|child"
```

---

### Phase 3: Configuration Changes (Day 2)

#### 3.1 Add Configuration Property

**File:** `PluginConfiguration.cs` (or similar)

```csharp
/// <summary>
/// When enabled, shows all series directly without category folders.
/// </summary>
[XmlElement("FlattenSeriesView")]
public bool FlattenSeriesView { get; set; } = false;

/// <summary>
/// Optional: Specify which categories to flatten (empty = all).
/// Allows partial flattening if users want some categories grouped.
/// </summary>
[XmlElement("FlattenedCategoryIds")]
public List<int> FlattenedCategoryIds { get; set; } = new();
```

#### 3.2 Update Configuration UI

**File:** Configuration HTML page (e.g., `configPage.html`)

```html
<div class="inputContainer">
    <label class="emby-checkbox-label">
        <input type="checkbox" 
               is="emby-checkbox" 
               id="flattenSeriesView" 
               data-setting="FlattenSeriesView" />
        <span>Flatten Series View</span>
    </label>
    <div class="fieldDescription">
        When enabled, all series from selected categories appear directly 
        in the library without category folders. Seasons and episodes 
        remain organized under each series.
    </div>
</div>

<!-- Advanced: Partial flattening (optional enhancement) -->
<div class="inputContainer" id="flattenOptions" style="display:none;">
    <label>Flatten specific categories only:</label>
    <div class="fieldDescription">
        Leave empty to flatten all selected categories.
    </div>
    <!-- Category checkboxes populated dynamically -->
</div>
```

---

### Phase 4: Core Logic Implementation (Day 2-4)

#### 4.1 Modify GetChildren Logic

**Pseudocode for the key modification:**

```csharp
public async Task<ChannelItemResult> GetChildren(
    InternalChannelItemQuery query,
    CancellationToken cancellationToken)
{
    var config = Plugin.Instance.Configuration;
    
    // Root level request (no folder ID)
    if (string.IsNullOrEmpty(query.FolderId))
    {
        if (config.FlattenSeriesView)
        {
            // FLAT VIEW: Return all series directly
            return await GetAllSeriesFlattened(query, cancellationToken);
        }
        else
        {
            // ORIGINAL: Return category folders
            return await GetCategoryFolders(query, cancellationToken);
        }
    }
    
    // Sub-folder request (user clicked a category)
    // Original behavior - return series in that category
    return await GetSeriesInCategory(query.FolderId, query, cancellationToken);
}

private async Task<ChannelItemResult> GetAllSeriesFlattened(
    InternalChannelItemQuery query,
    CancellationToken cancellationToken)
{
    var items = new List<ChannelItemInfo>();
    var selectedCategories = GetSelectedCategories();
    
    foreach (var category in selectedCategories)
    {
        // Check if this category should be flattened
        if (ShouldFlattenCategory(category.Id))
        {
            var seriesInCategory = await _xtreamClient
                .GetSeriesAsync(category.Id, cancellationToken);
            
            foreach (var series in seriesInCategory)
            {
                items.Add(CreateSeriesItem(series));
            }
        }
    }
    
    // Sort alphabetically for consistent display
    items = items.OrderBy(i => i.Name).ToList();
    
    return new ChannelItemResult
    {
        Items = items,
        TotalRecordCount = items.Count
    };
}

private bool ShouldFlattenCategory(int categoryId)
{
    var config = Plugin.Instance.Configuration;
    
    // If no specific categories selected, flatten all
    if (config.FlattenedCategoryIds == null || 
        config.FlattenedCategoryIds.Count == 0)
    {
        return true;
    }
    
    // Otherwise, only flatten specified categories
    return config.FlattenedCategoryIds.Contains(categoryId);
}
```

#### 4.2 Handle Pagination & Performance

```csharp
private async Task<ChannelItemResult> GetAllSeriesFlattened(
    InternalChannelItemQuery query,
    CancellationToken cancellationToken)
{
    // Performance: Cache the full series list
    var cacheKey = "flattened_series_" + GetConfigHash();
    
    if (!_cache.TryGetValue(cacheKey, out List<ChannelItemInfo> items))
    {
        items = await FetchAllSeries(cancellationToken);
        
        // Cache for 5 minutes to avoid hammering API
        _cache.Set(cacheKey, items, TimeSpan.FromMinutes(5));
    }
    
    // Apply pagination if requested
    var startIndex = query.StartIndex ?? 0;
    var limit = query.Limit ?? items.Count;
    
    var pagedItems = items
        .Skip(startIndex)
        .Take(limit)
        .ToList();
    
    return new ChannelItemResult
    {
        Items = pagedItems,
        TotalRecordCount = items.Count
    };
}
```

#### 4.3 Cache Invalidation

```csharp
// When configuration changes, invalidate cache
public void OnConfigurationChanged()
{
    _cache.Remove("flattened_series_" + GetConfigHash());
}
```

---

### Phase 5: Testing (Day 4-5)

#### 5.1 Test Matrix

| Scenario | Expected Result | Status |
|----------|-----------------|--------|
| Flat view OFF | Category folders shown (original behavior) | ⬜ |
| Flat view ON, all categories | All series shown directly, no folders | ⬜ |
| Flat view ON, some categories | Only selected categories flattened | ⬜ |
| Empty categories | Gracefully handled, no errors | ⬜ |
| Single category selected | Works correctly | ⬜ |
| No categories selected | Empty or appropriate message | ⬜ |
| Large number of series (500+) | Performance acceptable, pagination works | ⬜ |
| Click series → seasons visible | Season hierarchy maintained | ⬜ |
| Click season → episodes visible | Episode hierarchy maintained | ⬜ |
| Metadata refresh | Still works with flat view | ⬜ |
| Toggle flat view on/off | Changes apply correctly | ⬜ |

#### 5.2 Client Testing

| Client | Status | Notes |
|--------|--------|-------|
| Jellyfin Web | ⬜ Required | Primary test target |
| Jellyfin Android TV | ⬜ Recommended | May have different rendering |
| Jellyfin Mobile (iOS/Android) | ⬜ Optional | If available |

#### 5.3 Performance Benchmarks

| Metric | Target | Measured |
|--------|--------|----------|
| Initial load (100 series) | < 2 seconds | ⬜ |
| Initial load (500 series) | < 5 seconds | ⬜ |
| Cached load | < 500ms | ⬜ |
| Memory usage | < 50MB additional | ⬜ |

---

### Phase 6: Documentation & Release (Day 5-6)

#### 6.1 Update README.md

```markdown
## New Feature: Flat Series View (v0.9.0)

By default, series are organized under category folders. Enable 
**Flat Series View** to see all series directly in your library 
without the intermediate folder level.

### How to Enable
1. Go to Dashboard → Plugins → Xtream → Settings
2. Enable "Flatten Series View" checkbox
3. Save and refresh your library

### Notes
- Seasons and episodes remain organized under each series
- Only series from enabled categories are shown
- You can optionally flatten only specific categories
```

#### 6.2 Create Release

```bash
# Tag the release
git tag -a v0.9.0-flat-view -m "Add flat series view feature"
git push origin v0.9.0-flat-view

# Build release
dotnet build -c Release
dotnet pack -c Release

# Create GitHub release with:
# - Release notes
# - Built plugin DLL/ZIP
# - Installation instructions
```

#### 6.3 Installation Instructions

```markdown
## Manual Installation

1. Download `Jellyfin.Xtream.dll` from Releases
2. Stop Jellyfin server
3. Copy DLL to `<jellyfin-data>/plugins/Jellyfin.Xtream/`
4. Start Jellyfin server
5. Configure in Dashboard → Plugins → Xtream

## From Custom Repository

Add this repository URL to your Jellyfin plugin repositories:
`https://raw.githubusercontent.com/YOUR_USERNAME/Jellyfin.Xtream/master/manifest.json`
```

---

## ⚠️ Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **API Rate Limiting** | Provider blocks requests | Implement caching (5-min TTL), batch requests |
| **Performance with many series** | Slow UI, memory issues | Pagination, lazy loading, limit initial fetch |
| **Breaking existing configs** | Users lose settings | Default OFF, migration path, backward compat |
| **Client UI assumptions** | Some clients expect folders | Test multiple clients, provide fallback |
| **Duplicate series names** | User confusion | Include category hint in metadata or sort |
| **Metadata gaps** | Missing episodes/seasons | Trigger background refresh, show placeholders |

---

## 🎯 Success Criteria

- [ ] All series appear directly when flat view enabled
- [ ] No category folders shown
- [ ] Season/episode hierarchy preserved
- [ ] Original behavior unchanged when disabled
- [ ] Performance acceptable (< 5s for 500 series)
- [ ] Works on Jellyfin Web and Android TV
- [ ] Configuration persists across restarts
- [ ] No regressions in existing functionality

---

## 🚀 Optional Enhancements (Future)

| Enhancement | Description | Priority |
|-------------|-------------|----------|
| **Selective flattening** | Flatten only specific categories | Medium |
| **Sort options** | Sort by name, date added, recently updated | Low |
| **Search integration** | Ensure search works with flat view | Medium |
| **Select all button** | Select all series in flat view | Low |
| **User preference** | Per-user flat view setting (not just admin) | Low |
| **Hybrid view** | "All Series" folder + category folders | Low |

---

## 📚 Resources

| Resource | URL |
|----------|-----|
| Original Repository | https://github.com/Kevinjil/Jellyfin.Xtream |
| Jellyfin Plugin Development | https://jellyfin.org/docs/general/development/plugins/ |
| IChannel Interface | Search Jellyfin SDK documentation |
| Xtream API Reference | (varies by provider) |
| GPL-3.0 License | Must maintain in fork |

---

## 📝 Quick Reference

### Key Commands
```bash
# Clone and setup
git clone https://github.com/YOUR_USERNAME/Jellyfin.Xtream.git
cd Jellyfin.Xtream
git checkout -b feature/flat-series-view

# Explore codebase
grep -r "GetChildren" --include="*.cs" .
grep -r "category" --include="*.cs" -i .

# Build
dotnet build -c Release

# Test locally
# Copy DLL to Jellyfin plugins folder, restart server
```

### Files to Modify
1. `PluginConfiguration.cs` - Add `FlattenSeriesView` property
2. `*Channel.cs` - Modify `GetChildren()` logic
3. Config HTML - Add checkbox UI
4. README.md - Document feature

---

**Last Updated:** January 21, 2026  
**Status:** Ready for implementation
