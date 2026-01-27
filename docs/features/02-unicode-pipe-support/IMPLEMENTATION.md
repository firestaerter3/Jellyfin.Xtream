# Unicode Pipe Support - Implementation Details

## Document Info
- **Status:** Implemented
- **Version:** 0.9.4.x
- **Last Updated:** 2026-01-27
- **Related:** [REQUIREMENTS.md](./REQUIREMENTS.md), [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## Implementation Approach

This feature required a minimal code change - updating a single regex pattern to recognize Unicode pipe characters in addition to ASCII pipes.

### Implementation Timeline

1. **Initial Implementation** (commit `cc8425a`)
   - Added Unicode pipe support
   - Updated XML documentation
   - Used alternation pattern (had escaping issue)

2. **Bug Fix** (commit `19826f3`)
   - Fixed regex escaping issue
   - Changed from alternation to character class
   - Added Unicode code point documentation

---

## Code Changes

### Files Modified

| File | Changes |
|------|---------|
| `Jellyfin.Xtream/Service/StreamService.cs` | Modified TagRegex() pattern |

**Total:** 1 file, ~5 lines changed

---

### Jellyfin.Xtream/Service/StreamService.cs

#### Location: Lines 449-456

**Before:**
```csharp
[GeneratedRegex(@"\[([^\]]+)\]|\|([^\|]+)\|")]
private static partial Regex TagRegex();
```

**After:**
```csharp
// Matches tags in brackets [TAG] or pipe-delimited |TAG| (with optional spaces and Unicode pipe variants)
// Pipe variants: | (U+007C), │ (U+2502), ┃ (U+2503), ｜ (U+FF5C)
[GeneratedRegex(@"\[([^\]]+)\]|[|│┃｜]\s*([^|│┃｜]+?)\s*[|│┃｜]")]
private static partial Regex TagRegex();
```

#### Regex Pattern Breakdown

```regex
\[([^\]]+)\]|[|│┃｜]\s*([^|│┃｜]+?)\s*[|│┃｜]
└────┬────┘ └─────────────────┬─────────────┘
 Bracket     Pipe-delimited (with Unicode support)
  tags
```

**Part 1: Bracket tags (unchanged)**
- `\[` - Literal opening bracket
- `([^\]]+)` - Capture group 1: anything not a closing bracket
- `\]` - Literal closing bracket

**Part 2: Pipe tags (enhanced)**
- `[|│┃｜]` - Character class matching any pipe variant
- `\s*` - Optional whitespace (handles `| TAG |`)
- `([^|│┃｜]+?)` - Capture group 2: non-greedy match of anything not a pipe
- `\s*` - Optional trailing whitespace
- `[|│┃｜]` - Closing pipe (any variant)

---

#### Documentation Update: Lines 96-107

**Added comments to ParseName() documentation:**

```csharp
/// <summary>
/// Parses tags in the name of a stream entry.
/// The name commonly contains tags of the forms:
/// <list>
/// <item>[TAG]</item>
/// <item>|TAG|</item>
/// <item>| TAG | (with spaces, e.g., | NL |)</item>
/// </list>
/// Supports Unicode pipe variants (│, ┃, ｜) in addition to ASCII pipe.
/// These tags are parsed and returned as separate strings.
/// The returned title is cleaned from tags and trimmed.
/// </summary>
```

---

## Bug Fix Details

### Original Issue

The initial implementation used alternation with a non-capturing group:

```regex
(?:\||│|┃|｜)\s*([^|│┃｜]+?)\s*(?:\||│|┃|｜)
```

**Problem:** The `\|` (escaped pipe) inside `(?:...)` caused regex parsing issues. The pipe character has special meaning in regex (alternation), and escaping it inside a non-capturing group with other literal characters created confusion.

### Fix Applied

Changed to character class notation:

```regex
[|│┃｜]\s*([^|│┃｜]+?)\s*[|│┃｜]
```

**Why this works:**
- Inside `[...]` character class, `|` is a literal character (no escaping needed)
- Character classes match a single character from the set
- Simpler and more reliable than alternation for single-character matching

---

## API Compatibility

### Public API: No Changes

The `ParseName()` method signature is unchanged:

```csharp
public static ParsedName ParseName(string name)
```

### Return Type: No Changes

```csharp
public readonly struct ParsedName
{
    public string Title { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; }
}
```

### Behavior Changes

| Input | Before | After |
|-------|--------|-------|
| `[US] Title` | Title: "Title", Tags: ["US"] | Same |
| `\|HD\| Title` | Title: "Title", Tags: ["HD"] | Same |
| `┃NL┃ Title` | Title: "┃NL┃ Title", Tags: [] | Title: "Title", Tags: ["NL"] |
| `│4K│ Movie` | Title: "│4K│ Movie", Tags: [] | Title: "Movie", Tags: ["4K"] |

---

## Integration Points

### Callers of ParseName()

`ParseName()` is called throughout the codebase to clean stream titles:

1. **SeriesChannel.cs**
   - `CreateChannelItemInfo(Series series)` - Series titles
   - `CreateChannelItemInfo(seriesId, series, seasonId)` - Season titles
   - `CreateChannelItemInfo(series, season, episode)` - Episode titles

2. **VodChannel.cs**
   - `CreateChannelItemInfo(StreamInfo stream)` - Movie titles

3. **StreamService.cs**
   - `CreateChannelItemInfo()` - Generic stream titles
   - `GetMediaSourceInfo()` - Media source names

**Impact:** All these locations automatically benefit from Unicode pipe support with no code changes required.

---

## Build Configuration

No build configuration changes required.

The `[GeneratedRegex]` attribute uses .NET's source generator:
- Compiles regex at build time
- No runtime regex compilation
- Requires .NET 7.0+ (project uses .NET 9.0)

---

## Testing Notes

### Manual Test Procedure

1. Configure an Xtream provider that uses Unicode pipes
2. Browse to Series channel
3. Verify series titles are cleaned (no `┃NL┃` visible)
4. Verify tags are extracted (visible in item details if exposed)
5. Verify sorting works correctly (alphabetical by clean title)

### Test Data Examples

Provider naming patterns observed:
- `┃NL┃ Breaking Bad ┃HD┃`
- `│ MULTI │ Game of Thrones`
- `｜4K｜ Avatar`
- `| EN | The Office`

All should clean to just the show name.

---

## Performance Impact

**No measurable impact:**
- Regex is compiled at build time
- Character class matching is O(1)
- Same algorithmic complexity as before

---

## Deployment Notes

**No special deployment considerations:**
- Standard plugin DLL update
- No database changes
- No configuration migration
- Works immediately after plugin restart

---

## Related Commits

| Commit | Description |
|--------|-------------|
| `cc8425a` | Add support for Unicode pipe characters in tag parsing |
| `19826f3` | Fix Unicode pipe tag parsing regex pattern |

**Total changes:**
- 1 file changed
- 5 insertions, 1 deletion

---

## References

- [REQUIREMENTS.md](./REQUIREMENTS.md) - Feature requirements
- [ARCHITECTURE.md](./ARCHITECTURE.md) - Design decisions
- [TEST_PLAN.md](./TEST_PLAN.md) - Test cases
- [CHANGELOG.md](./CHANGELOG.md) - Version history
