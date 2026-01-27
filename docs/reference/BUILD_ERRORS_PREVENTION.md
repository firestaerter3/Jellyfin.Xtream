# Build Errors Prevention Guide

This document outlines common build errors and how to prevent them before pushing code.

## Common Build Errors

### 1. Code Style Issues (StyleCop Analyzer)

#### SA1028: Trailing Whitespace
**Error:** `Code should not contain trailing whitespace`
**Cause:** Lines ending with spaces or tabs
**Prevention:**
- Configure your editor to show trailing whitespace
- Enable "Trim trailing whitespace on save"
- Use `.editorconfig` (already in repo)

**Fix:**
```bash
# Find and remove trailing whitespace
find . -name "*.cs" -exec sed -i '' 's/[[:space:]]*$//' {} \;
```

#### SA1208: Using Directive Order
**Error:** `Using directive for 'X' should appear before directive for 'Y'`
**Cause:** Using directives not in alphabetical/system namespace order
**Rule:** System namespaces first, then third-party, then project namespaces

**Correct Order:**
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;  // System.* first
using Jellyfin.Xtream.Client;   // Project namespaces
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.Logging;
```

**Prevention:**
- Use IDE "Organize Usings" feature (VS Code: `Ctrl+Shift+P` → "Organize Imports")
- Keep System.* namespaces together at top

---

### 2. XML Documentation (StyleCop Analyzer)

#### SA1615: Missing Return Value Documentation
**Error:** `Element return value should be documented`
**Fix:** Add `<returns>` tag to XML comments

```csharp
/// <summary>
/// Gets cached categories.
/// </summary>
/// <returns>Cached categories, or null if not available.</returns>
public IEnumerable<Category>? GetCachedCategories()
```

#### SA1611: Missing Parameter Documentation
**Error:** `The documentation for parameter 'X' is missing`
**Fix:** Add `<param name="X">` tag for all parameters

```csharp
/// <summary>
/// Gets cached episodes for a season.
/// </summary>
/// <param name="seriesId">The series ID.</param>
/// <param name="seasonId">The season ID.</param>
/// <returns>Cached episodes, or null if not available.</returns>
public IEnumerable<Episode>? GetCachedEpisodes(int seriesId, int seasonId)
```

**Prevention:**
- Always add XML documentation for public methods
- Use IDE snippets or extensions to generate XML docs automatically

---

### 3. Code Analysis (CA Rules)

#### CA1063: IDisposable Pattern
**Error:** `Provide an overridable implementation of Dispose(bool)`
**Cause:** Class implements `IDisposable` but doesn't follow the pattern

**Correct Pattern:**
```csharp
public class MyService : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lock?.Dispose();
        }
    }
}
```

**Prevention:**
- If class has disposable fields, implement full IDisposable pattern
- Or mark class as `sealed` if inheritance not needed

#### CA2007: ConfigureAwait
**Error:** `Consider calling ConfigureAwait on the awaited task`
**Cause:** Missing `.ConfigureAwait(false)` on async calls

**Fix:**
```csharp
// Bad
await SomeMethodAsync();

// Good
await SomeMethodAsync().ConfigureAwait(false);
```

**Prevention:**
- Always use `.ConfigureAwait(false)` in library code (not UI code)
- Use code analysis rule to enforce

#### CA1001: Disposable Fields
**Error:** `Type owns disposable field(s) but is not disposable`
**Cause:** Class has `SemaphoreSlim`, `Stream`, etc. but doesn't implement `IDisposable`

**Fix:** Implement `IDisposable` (see CA1063 above)

---

### 4. Type Mismatches

#### CS1503: Argument Type Mismatch
**Error:** `cannot convert from 'ILogger<A>' to 'ILogger<B>'`
**Cause:** Passing wrong generic logger type

**Fix:**
```csharp
// Bad
new Service(streamService, memoryCache, logger);  // logger is ILogger<Plugin>

// Good - use ILoggerFactory
new Service(streamService, memoryCache, loggerFactory.CreateLogger<Service>());
```

**Prevention:**
- Use `ILoggerFactory` to create correctly typed loggers
- Don't pass logger from one type to another

---

## Prevention Strategies

### 1. Local Build Before Push

**Always build locally before pushing:**
```bash
# Build and check for errors
dotnet build --configuration Release

# Or use the same build command as CI
dotnet build --configuration Release --no-incremental
```

### 2. Editor Configuration

**VS Code Settings (`.vscode/settings.json`):**
```json
{
  "files.trimTrailingWhitespace": true,
  "files.insertFinalNewline": true,
  "editor.formatOnSave": true,
  "omnisharp.enableRoslynAnalyzers": true,
  "omnisharp.enableEditorConfigSupport": true
}
```

**Visual Studio:**
- Enable "Remove trailing whitespace on save"
- Enable "Organize usings on save"
- Enable Code Analysis on build

### 3. Pre-commit Hook

Create `.git/hooks/pre-commit` (or use Husky for cross-platform):

```bash
#!/bin/sh
# Pre-commit hook to check for common issues

echo "Running pre-commit checks..."

# Check for trailing whitespace
if git diff --cached --check --diff-filter=ACM | grep -q "^+.*[[:space:]]$"; then
    echo "ERROR: Trailing whitespace detected!"
    echo "Run: find . -name '*.cs' -exec sed -i '' 's/[[:space:]]*$//' {} \\;"
    exit 1
fi

# Try to build
dotnet build --configuration Release --no-incremental > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed! Fix errors before committing."
    dotnet build --configuration Release --no-incremental
    exit 1
fi

echo "Pre-commit checks passed!"
exit 0
```

### 4. GitHub Actions Pre-check

Add a "lint" job that runs before build:

```yaml
lint:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'
    - name: Build (no restore) to check for errors
      run: dotnet build --configuration Release --no-restore
```

### 5. IDE Extensions

**Recommended Extensions:**
- **OmniSharp** (VS Code) - C# language support with Roslyn analyzers
- **EditorConfig** - Enforces `.editorconfig` rules
- **Code Spell Checker** - Catches typos
- **Error Lens** - Shows errors inline

### 6. Code Review Checklist

Before submitting PR, check:
- [ ] No trailing whitespace
- [ ] Using directives in correct order
- [ ] All public methods have XML documentation
- [ ] All parameters documented in XML comments
- [ ] `IDisposable` implemented correctly if needed
- [ ] `.ConfigureAwait(false)` used in library code
- [ ] Logger types match (use `ILoggerFactory` when needed)
- [ ] Local build succeeds: `dotnet build --configuration Release`

---

## Quick Fix Commands

### Remove Trailing Whitespace
```bash
# macOS/Linux
find . -name "*.cs" -exec sed -i '' 's/[[:space:]]*$//' {} \;

# Linux (GNU sed)
find . -name "*.cs" -exec sed -i 's/[[:space:]]*$//' {} \;
```

### Organize Usings (VS Code)
- `Ctrl+Shift+P` → "Organize Imports"
- Or use extension: "C# Fix Format"

### Generate XML Documentation
- VS Code: Install "C# XML Documentation Comments"
- Visual Studio: Type `///` above method to auto-generate

### Check Build Locally
```bash
# Full build (same as CI)
dotnet clean
dotnet restore
dotnet build --configuration Release --no-incremental
```

---

## Automated Solutions

### Option 1: Pre-commit Hook Script

Create `scripts/pre-commit-check.sh`:

```bash
#!/bin/bash
set -e

echo "🔍 Running pre-commit checks..."

# Check for trailing whitespace
echo "Checking for trailing whitespace..."
if git diff --cached --check --diff-filter=ACM | grep -q "^+.*[[:space:]]$"; then
    echo "❌ ERROR: Trailing whitespace detected!"
    echo "Fix with: find . -name '*.cs' -exec sed -i '' 's/[[:space:]]*$//' {} \\;"
    exit 1
fi

# Build check
echo "Building project..."
if ! dotnet build --configuration Release --no-incremental > /dev/null 2>&1; then
    echo "❌ ERROR: Build failed!"
    echo "Running full build to show errors:"
    dotnet build --configuration Release --no-incremental
    exit 1
fi

echo "✅ All checks passed!"
exit 0
```

Then create `.git/hooks/pre-commit`:
```bash
#!/bin/sh
exec bash scripts/pre-commit-check.sh
```

### Option 2: Makefile Targets

Create `Makefile`:

```makefile
.PHONY: check build clean fix-whitespace

check: fix-whitespace build
	@echo "✅ All checks passed!"

build:
	@echo "🔨 Building..."
	dotnet build --configuration Release --no-incremental

fix-whitespace:
	@echo "🧹 Removing trailing whitespace..."
	@find . -name "*.cs" -exec sed -i '' 's/[[:space:]]*$$//' {} \;

clean:
	dotnet clean
```

Usage: `make check` before committing

### Option 3: GitHub Actions Pre-check

Add to `.github/workflows/build.yaml`:

```yaml
name: "🔍 Pre-build Checks"

on:
  pull_request:
  push:
    branches: [master]

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Check for trailing whitespace
        run: |
          if git diff --check | grep -q "^+.*[[:space:]]$"; then
            echo "❌ Trailing whitespace found!"
            exit 1
          fi
      - name: Build (lint check)
        run: dotnet build --configuration Release --no-restore
```

---

## EditorConfig Rules

The `.editorconfig` file should include:

```ini
[*.cs]
# Remove trailing whitespace
trim_trailing_whitespace = true
insert_final_newline = true

# Indentation
indent_style = space
indent_size = 4

# Code style
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
```

---

## Summary

**Most Common Issues:**
1. ✅ Trailing whitespace (SA1028) - **Fix:** Enable trim on save
2. ✅ Using directive order (SA1208) - **Fix:** Use "Organize Imports"
3. ✅ Missing XML docs (SA1615/SA1611) - **Fix:** Add `<returns>` and `<param>` tags
4. ✅ IDisposable pattern (CA1063) - **Fix:** Implement full pattern
5. ✅ ConfigureAwait (CA2007) - **Fix:** Always use `.ConfigureAwait(false)`
6. ✅ Logger type mismatch - **Fix:** Use `ILoggerFactory`

**Best Practices:**
- Build locally before pushing
- Use pre-commit hooks
- Configure editor to auto-fix common issues
- Run `make check` or equivalent before committing
- Review this checklist before PRs

---

## Version Management

### Version Mismatch Between build.yaml and .csproj

**Problem:** Jellyfin UI shows wrong plugin version even though build.yaml is updated.

**Root Cause:** Jellyfin reads the version from the DLL's assembly metadata (defined in `.csproj`), **not** from `build.yaml` or `meta.json`.

**Symptoms:**
- `build.yaml` shows version X.Y.Z.W
- Jellyfin UI shows older version
- Logs show correct version loaded
- Plugin repository shows correct version

**Solution:** Update **THREE** locations when releasing a new version:

1. **build.yaml** - Lines 4 and 15+
```yaml
version: "0.9.5.3"
changelog: >
  v0.9.5.3 - Description
```

2. **Jellyfin.Xtream.csproj** - Lines 6-7 ⚠️ **CRITICAL**
```xml
<AssemblyVersion>0.9.5.3</AssemblyVersion>
<FileVersion>0.9.5.3</FileVersion>
```

3. **Git tag** - Create matching tag
```bash
git tag v0.9.5.3
git push origin v0.9.5.3
```

**Prevention Checklist:**
- [ ] Update `build.yaml` version
- [ ] Update `build.yaml` changelog
- [ ] Update `.csproj` AssemblyVersion **← Don't forget!**
- [ ] Update `.csproj` FileVersion **← Don't forget!**
- [ ] Rebuild: `dotnet build --configuration Release`
- [ ] Commit changes
- [ ] Create and push git tag
- [ ] Create GitHub release (triggers CI/CD)

**Why This Matters:**
- Users see version in Jellyfin UI from assembly metadata
- Auto-update compares assembly version
- Repository manifest uses build.yaml
- All three must match for consistency

### Plugin File Permissions (Docker/Linux)

**Problem:** Jellyfin fails to start with "Access denied" error after deploying plugin files.

**Error Message:**
```
System.UnauthorizedAccessException: Access to the path '/config/data/plugins/Jellyfin Xtream (Flat View)_X.Y.Z/meta.json' is denied.
```

**Root Cause:** Files copied to plugin directory are owned by `root`, but Jellyfin container runs as user `abc:abc`.

**Solution:**
```bash
# After copying files to plugin directory, fix ownership:
docker exec jellyfin chown -R abc:abc '/config/data/plugins/Jellyfin Xtream (Flat View)_X.Y.Z'
```

**Prevention:**
When deploying manually:
1. Copy DLL and meta.json to plugin directory
2. **Immediately** run `chown -R abc:abc` on the directory
3. Then restart Jellyfin

**Correct Deployment Process:**
```bash
# 1. Copy DLL
docker cp /tmp/Jellyfin.Xtream.dll jellyfin:'/config/data/plugins/Jellyfin Xtream (Flat View)_X.Y.Z/'

# 2. Copy meta.json
docker cp /tmp/meta.json jellyfin:'/config/data/plugins/Jellyfin Xtream (Flat View)_X.Y.Z/'

# 3. Fix permissions (CRITICAL!)
docker exec jellyfin chown -R abc:abc '/config/data/plugins/Jellyfin Xtream (Flat View)_X.Y.Z'

# 4. Restart Jellyfin
docker restart jellyfin
```

**Why This Happens:**
- Docker `cp` creates files as root
- Jellyfin security runs as non-root user `abc`
- Plugin manager needs read access to meta.json
- Plugin needs read access to DLL

---

## Quick Reference Card

**Before Every Commit:**
```bash
# Option 1: Use Makefile
make check

# Option 2: Run script directly
./scripts/pre-commit-check.sh

# Option 3: Manual checks
make fix-whitespace
dotnet build --configuration Release --no-incremental
```

**Common Fixes:**
- Trailing whitespace → `make fix-whitespace`
- Using order → VS Code: "Organize Imports"
- Missing XML docs → Add `<returns>` and `<param>` tags
- IDisposable → Implement full pattern with `Dispose(bool)`
- Logger type → Use `ILoggerFactory.CreateLogger<T>()`

**Install Pre-commit Hook:**
```bash
make install-hooks
```

---

*Last updated: 2026-01-27*
