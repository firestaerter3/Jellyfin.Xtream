# Project Context - Jellyfin Xtream Flat View Fork

> **For AI Assistants:** This file contains comprehensive project context. See also `CLAUDE.md` in the root for quick reference.

## Project Overview

This is a fork of [Jellyfin.Xtream](https://github.com/Kevinjil/Jellyfin.Xtream) with additional features:
- **Flat Series View** - Shows all series directly without category folders
- **Flat VOD View** - Shows all movies directly without category folders  
- **Upfront Caching** - Pre-fetches and caches all series data to eliminate lazy loading delays
- **Missing Episodes Fix** - Handles episodes stored under different season ID keys
- **Unicode Pipe Support** - Parses Unicode pipe variants (│, ┃, ｜) in addition to ASCII pipe

## Repository Structure

```
Jellyfin-Xtream-FlatView/
├── CLAUDE.md                    # Quick reference for AI assistants
├── CLAUDE.local.md              # Local notes (gitignored)
├── README.md                    # User-facing documentation
├── Makefile                     # Convenience commands (check, build, fix-whitespace)
├── build.yaml                   # Plugin metadata (version, changelog)
├── docs/
│   ├── PROJECT_CONTEXT.md       # This file - comprehensive context
│   ├── BUILD_ERRORS_PREVENTION.md  # Guide to prevent common build errors
│   ├── PR_PROPOSAL.md           # Strategy for upstream contributions
│   ├── DEVELOPMENT_NOTES.md     # Local development notes (gitignored)
│   └── REPOSITORY_SETUP.md     # Plugin repository setup guide
├── scripts/
│   ├── pre-commit-check.sh      # Pre-commit hook to catch build errors
│   └── README.md                # Scripts documentation
└── Jellyfin.Xtream/             # Main plugin code
    ├── Plugin.cs                # Main plugin class
    ├── SeriesChannel.cs         # Series channel implementation
    ├── VodChannel.cs            # VOD channel implementation
    ├── Service/
    │   ├── StreamService.cs     # Core API service, title parsing
    │   └── SeriesCacheService.cs # Upfront caching service
    └── Configuration/
        ├── PluginConfiguration.cs  # Plugin settings
        └── Web/                  # UI configuration pages
```

## Key Features & Implementation

### 1. Flat Series View

**Location:** `Jellyfin.Xtream/SeriesChannel.cs`
- **Method:** `GetAllSeriesFlattened()`
- **Configuration:** `PluginConfiguration.FlattenSeriesView` (boolean)
- **UI:** Checkbox in Series configuration page

**How it works:**
- When enabled, bypasses category folders and returns all series directly
- Series are sorted alphabetically
- Seasons and episodes remain organized under each series

### 2. Flat VOD View

**Location:** `Jellyfin.Xtream/VodChannel.cs`
- **Method:** `GetAllStreamsFlattened()`
- **Configuration:** `PluginConfiguration.FlattenVodView` (boolean)
- **UI:** Checkbox in VOD configuration page

**How it works:**
- Similar to flat series view, but for VOD (movies)
- All movies from selected categories appear directly

### 3. Upfront Caching

**Location:** `Jellyfin.Xtream/Service/SeriesCacheService.cs`
- **Purpose:** Eliminates lazy loading delays by pre-fetching all series data
- **Configuration:** `PluginConfiguration.SeriesCacheExpirationMinutes` (default: 60)
- **Lifecycle:** 
  - Cache refreshes on plugin startup
  - Cache refreshes when configuration changes
  - Cache expires after configured time

**How it works:**
- `SeriesCacheService` pre-fetches all categories, series, seasons, and episodes
- Data stored in `IMemoryCache` with expiration
- Channel methods check cache first, fallback to API if needed

### 4. Missing Episodes Fix

**Location:** `Jellyfin.Xtream/Service/StreamService.cs` → `GetEpisodes()`
- **Problem:** Episodes stored under different season ID keys than their `Season` property
- **Solution:** Two-step lookup:
  1. Try direct dictionary lookup by `seasonId`
  2. Fallback: iterate all episodes and filter by `episode.Season` property
- **Also:** Added configuration check for consistency

### 5. Unicode Pipe Support

**Location:** `Jellyfin.Xtream/Service/StreamService.cs` → `ParseName()`
- **Pattern:** `[GeneratedRegex(@"\[([^\]]+)\]|(?:\||│|┃|｜)\s*([^|│┃｜]+?)\s*(?:\||│|┃|｜)")]`
- **Supports:** ASCII pipe (`|`), Unicode box drawing (`│`, `┃`), fullwidth (`｜`)
- **Handles:** Spaces inside pipes (e.g., `| NL |`, `┃ NL ┃`)

## Build & Release Process

### Versioning
- **Plugin version** defined in `build.yaml`: `version: "X.Y.Z.W"`
- Git tags should match: `v0.9.2.0` → `version: "0.9.2.0"`
- This is what Jellyfin displays in the plugin catalog

### Building Locally
```bash
# Requires .NET 9.0 SDK
dotnet build --configuration Release

# Or use Makefile
make build
make check  # Runs fix-whitespace + build
```

### Release Workflow
1. Update version in `build.yaml`
2. Update changelog in `build.yaml`
3. Commit and push to `master`
4. Create git tag: `git tag v0.9.2.0`
5. Push tag: `git push origin v0.9.2.0`
6. Create GitHub release: `gh release create v0.9.2.0 --title "v0.9.2.0" --notes "..."`
7. GitHub Actions (`.github/workflows/publish.yaml`) automatically:
   - Builds the plugin
   - Uploads ZIP to the release
   - Generates checksums (md5, sha256)
   - Updates plugin manifest repository

### Code Analysis
- Project uses `TreatWarningsAsErrors` - all warnings fail the build
- StyleCop and other analyzers are enabled
- See `docs/BUILD_ERRORS_PREVENTION.md` for common issues and fixes

## Common Build Errors & Prevention

**Most Common Issues:**
1. **SA1028: Trailing whitespace** → `make fix-whitespace`
2. **SA1208: Using directive order** → VS Code: "Organize Imports"
3. **SA1615/SA1611: Missing XML docs** → Add `<returns>` and `<param>` tags
4. **CA1063: IDisposable pattern** → Implement full pattern with `Dispose(bool)`
5. **CA2007: ConfigureAwait** → Always use `.ConfigureAwait(false)`
6. **CS1503: Logger type mismatch** → Use `ILoggerFactory.CreateLogger<T>()`

**Prevention:**
- Install pre-commit hook: `make install-hooks`
- Run checks before commit: `make check`
- See `docs/BUILD_ERRORS_PREVENTION.md` for comprehensive guide

## Git Remotes

- **origin:** `https://github.com/rolandb5/Jellyfin.Xtream.git` (your fork)
- **upstream:** `https://github.com/Kevinjil/Jellyfin.Xtream.git` (original repo)

## Key Files Reference

| File | Purpose |
|------|---------|
| `build.yaml` | Plugin metadata, version, changelog (what Jellyfin sees) |
| `Jellyfin.Xtream.csproj` | .NET project file, assembly version |
| `Plugin.cs` | Main plugin class, initialization, cache service setup |
| `SeriesChannel.cs` | Series channel implementation, flat view logic |
| `VodChannel.cs` | VOD channel implementation, flat view logic |
| `Service/StreamService.cs` | Core service, API calls, `ParseName()` for title cleaning |
| `Service/SeriesCacheService.cs` | Upfront caching service |
| `Configuration/PluginConfiguration.cs` | Plugin settings (FlattenSeriesView, FlattenVodView, SeriesCacheExpirationMinutes) |
| `.github/workflows/publish.yaml` | CI/CD workflow for building and publishing |

## Title Parsing (ParseName)

Located in `Service/StreamService.cs`, the `ParseName()` method strips tags from titles:
- `[TAG]` - Square brackets
- `|TAG|` - ASCII pipe
- `┃TAG┃` - Unicode box drawing heavy vertical (U+2503)
- `│TAG│` - Unicode box drawing light vertical (U+2502)
- `｜TAG｜` - Unicode fullwidth vertical line (U+FF5C)
- Handles spaces inside pipes: `| TAG |`, `┃ NL ┃`

## Jellyfin Plugin Installation

Users add this repository URL to Jellyfin's plugin repositories:
```
https://rolandb5.github.io/Jellyfin.Xtream/repository.json
```

## Development Workflow

### Before Committing
```bash
# Run all checks (recommended)
make check

# Or manually
make fix-whitespace
dotnet build --configuration Release --no-incremental
```

### Editor Configuration
- **VS Code:** `.vscode/settings.json` (auto-trim whitespace, organize imports)
- **Visual Studio:** Enable "Remove trailing whitespace on save"

### Testing
1. Build: `dotnet build --configuration Release`
2. Copy DLL to Jellyfin plugins folder
3. Restart Jellyfin
4. Enable features in plugin configuration
5. Verify behavior matches expectations

## Known Issues & Limitations

- Plugin uses same GUID as original (users can't install both simultaneously)
- Flat view shows all content from all selected categories (no filtering by category in flat view)
- Cache expiration is configurable but defaults to 1 hour

## Future Improvements

- [ ] Add option to filter by category even in flat view
- [ ] Support for other country code patterns
- [ ] Option to customize prefix removal patterns
- [ ] Better error handling and logging
- [ ] Performance optimization for large catalogs

## Resources

- **Original Plugin:** https://github.com/Kevinjil/Jellyfin.Xtream
- **Jellyfin Plugin Docs:** https://jellyfin.org/docs/general/development/plugins/
- **Fork Repository:** https://github.com/rolandb5/Jellyfin.Xtream
- **Plugin Repository:** https://rolandb5.github.io/Jellyfin.Xtream/repository.json

---

*Last updated: 2026-01-24*
