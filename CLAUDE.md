# Jellyfin Xtream Flat View - Quick Reference

> **For AI Assistants:** This is a quick reference. For comprehensive context, see `docs/PROJECT_CONTEXT.md`.

## Build & Release Process

### Versioning
- **Plugin version** is defined in `build.yaml` (line 4): `version: "X.Y.Z.W"`
- This is what Jellyfin displays in the plugin catalog
- Git tags (e.g., `v0.9.1.4`) should match the `build.yaml` version

### Building Locally
```bash
# Requires .NET 9.0 SDK
dotnet build Jellyfin.Xtream/Jellyfin.Xtream.csproj
```

### Release Workflow
1. Update version in `build.yaml`
2. Update changelog in `build.yaml`
3. Commit and push to `master`
4. Create git tag: `git tag v0.9.1.4`
5. Push tag: `git push origin v0.9.1.4`
6. Create GitHub release: `gh release create v0.9.1.4 --repo rolandb5/Jellyfin.Xtream --title "v0.9.1.4" --notes "..."`
7. GitHub Actions (`.github/workflows/build.yaml`) automatically:
   - Builds the plugin
   - Uploads `jellyfin-xtream-flat-view_X.Y.Z.W.zip` to the release
   - Generates checksums (md5, sha256)
   - Updates the plugin manifest repository

### Code Analysis
- Project uses `TreatWarningsAsErrors` - all warnings fail the build
- StyleCop and other analyzers are enabled
- Common issues:
  - CA1860: Use `Count > 0` instead of `Any()`
  - SA1028: Trailing whitespace

## Key Files

| File | Purpose |
|------|---------|
| `build.yaml` | Plugin metadata, version, changelog (what Jellyfin sees) |
| `Jellyfin.Xtream.csproj` | .NET project file, assembly version |
| `Service/StreamService.cs` | Core service, includes `ParseName()` for title cleaning |
| `VodChannel.cs` | VOD/Movies channel implementation |
| `SeriesChannel.cs` | Series channel implementation |
| `Configuration/PluginConfiguration.cs` | Plugin settings (FlattenSeriesView, FlattenVodView, etc.) |

## Title Parsing (ParseName)

Located in `Service/StreamService.cs`, the `ParseName()` method strips tags from titles:
- `[TAG]` - Square brackets
- `|TAG|` - ASCII pipe
- `┃TAG┃` - Unicode box drawing heavy vertical (U+2503)
- `│TAG│` - Unicode box drawing light vertical (U+2502)
- `｜TAG｜` - Unicode fullwidth vertical line (U+FF5C)
- Handles spaces inside pipes: `| TAG |`, `┃ NL ┃`

## Git Remotes

- `origin`: https://github.com/rolandb5/Jellyfin.Xtream.git (your fork)
- `upstream`: https://github.com/Kevinjil/Jellyfin.Xtream.git (original repo)

## Jellyfin Plugin Installation

Users add this repository URL to Jellyfin's plugin repositories:
```
https://rolandb5.github.io/Jellyfin.Xtream/repository.json
```

## Development & Testing Environment

**Test Jellyfin Instance:** See `CLAUDE.local.md` for server details (Proxmox container, credentials, deployment paths)

### Quick Deploy to Test Server
```bash
# Build plugin
dotnet build Jellyfin.Xtream/Jellyfin.Xtream.csproj --configuration Release

# Deploy to test server (see CLAUDE.local.md for server details)
sshpass -p '<password>' scp -o StrictHostKeyChecking=no \
  Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll \
  root@<server-ip>:'/root/apps/jellyfin/library/data/plugins/Jellyfin Xtream (Flat View)_<version>/'

# Restart Jellyfin container
sshpass -p '<password>' ssh -o StrictHostKeyChecking=no root@<server-ip> "docker restart jellyfin"
```

### Check Logs
```bash
# View recent logs
sshpass -p '<password>' ssh -o StrictHostKeyChecking=no root@<server-ip> \
  "docker logs jellyfin 2>&1 | tail -50"

# Monitor cache refresh progress
sshpass -p '<password>' ssh -o StrictHostKeyChecking=no root@<server-ip> \
  "docker logs jellyfin 2>&1 | grep 'Cache refresh\|Processing series' | tail -20"

# Check cache hit/miss rates
sshpass -p '<password>' ssh -o StrictHostKeyChecking=no root@<server-ip> \
  "docker logs jellyfin 2>&1 | grep 'cache HIT\|cache MISS' | tail -30"
```

## Documentation Structure

**Start Here:** `docs/INDEX.md` - Master documentation hub

### Quick Links
- **`CLAUDE.md`** (this file) - Quick reference for AI assistants
- **`docs/INDEX.md`** - Master documentation hub (start here)
- **`docs/reference/PROJECT_CONTEXT.md`** - Comprehensive project context
- **`docs/reference/BUILD_ERRORS_PREVENTION.md`** - Guide to prevent build errors
- **`docs/upstream/PR_PROPOSAL.md`** - Strategy for upstream contributions
- **`docs/upstream/PR_STATUS.md`** - Dashboard tracking PR readiness for all features
- **`docs/upstream/PR_WORKFLOW.md`** - Step-by-step PR submission guide
- **`docs/REORGANIZATION_SUMMARY.md`** - Documentation reorganization summary

### Feature Documentation (7 features)
Each feature has comprehensive documentation in `docs/features/<NN-feature-name>/`:
- `REQUIREMENTS.md` - What and why (user stories, functional requirements)
- `ARCHITECTURE.md` - How (design decisions, components, data flow)
- `IMPLEMENTATION.md` - Code changes, technical details
- `CONTEXT.md` - **AI assistant context** (gotchas, session handoff, critical code)
- `TEST_PLAN.md` - Manual test cases, performance benchmarks
- `TODO.md` - Outstanding tasks, future enhancements
- `CHANGELOG.md` - Version history, breaking changes

**Fully Documented:**
- 01-flat-view (partial - REQUIREMENTS.md complete)
- 04-eager-caching (complete - 8 documents) ⭐

**Templates Available:** 02, 03, 05, 06, 07

### Automation Scripts
```bash
# Generate new feature documentation scaffold
./scripts/docs/generate-feature.sh <num> <name>

# Validate all features have required docs
./scripts/docs/validate-structure.sh
```
