# Setting Up a Plugin Repository

This guide explains how to set up a Jellyfin plugin repository so users can install your forked plugin directly through Jellyfin's plugin system.

## Overview

Jellyfin plugins can be distributed through custom repositories. When you add a repository URL to Jellyfin, it will:
1. Fetch the `repository.json` manifest file
2. Display available plugins in the Catalog
3. Allow users to install/update plugins with one click

## Option 1: Automatic Setup (Recommended)

The repository includes a GitHub Action that automatically generates and publishes the `repository.json` file when you create a GitHub Release.

### Prerequisites

1. Push your fork to GitHub
2. Enable GitHub Pages in your repository settings
3. Create a GitHub Release when you're ready to publish

### Steps

1. **Push to GitHub:**
   ```bash
   git remote add origin https://github.com/YOUR_USERNAME/Jellyfin.Xtream.git
   git push -u origin feature/flat-series-view
   ```

2. **Enable GitHub Pages:**
   - Go to your repository on GitHub
   - Settings → Pages
   - Source: Select "gh-pages" branch (or "Deploy from a branch")
   - Save

3. **Create a Release:**
   - Go to Releases → "Draft a new release"
   - Tag: `v0.9.0-flat-view` (or your version)
   - Title: "Flat Series View Feature"
   - Upload the built `Jellyfin.Xtream.dll` as a release asset
   - Publish release

4. **The GitHub Action will:**
   - Build the plugin
   - Generate `repository.json`
   - Publish it to the `gh-pages` branch
   - Make it available at: `https://YOUR_USERNAME.github.io/Jellyfin.Xtream/repository.json`

5. **Users can then add your repository:**
   - Repository Name: `Jellyfin Xtream (Flat View)`
   - Repository URL: `https://YOUR_USERNAME.github.io/Jellyfin.Xtream/repository.json`

## Option 2: Manual Setup

If you prefer to host the repository manually:

### 1. Build the Plugin

```bash
cd Jellyfin.Xtream
dotnet restore
dotnet build -c Release
```

The DLL will be in: `bin/Release/net9.0/Jellyfin.Xtream.dll`

### 2. Create a Release Package

Create a ZIP file containing:
- `Jellyfin.Xtream.dll`
- Optionally: `Jellyfin.Xtream.pdb` (for debugging)

### 3. Host the Files

Upload to a web server or GitHub Releases:
- `Jellyfin.Xtream.zip` (the plugin package)
- `repository.json` (the manifest)

### 4. Create repository.json

See `repository.json.example` in the root directory for a template.

The key fields:
- `Name`: Plugin name
- `Id`: Must match the GUID in `Plugin.cs` (currently `5d774c35-8567-46d3-a950-9bb8227a0c5d`)
- `Versions`: Array of version objects with download URLs

### 5. Update build.yaml

Update the version and description in `build.yaml`:

```yaml
name: "Jellyfin Xtream (Flat View)"
version: "0.9.0.0"
description: >
  Stream Live IPTV, Video On-Demand, and Series from an Xtream-compatible server.
  Includes flat series view feature to show all series directly without category folders.
```

## Important Notes

⚠️ **Plugin GUID**: The plugin uses the same GUID as the original (`5d774c35-8567-46d3-a950-9bb8227a0c5d`). This means:
- Users can't install both versions simultaneously
- Installing your fork will replace the original plugin
- Consider changing the GUID if you want both to coexist (requires code changes)

## Testing Your Repository

1. Add your repository URL to Jellyfin
2. Go to Plugins → Catalog
3. Look for "Jellyfin Xtream" under Live TV
4. Verify the version and description are correct
5. Install and test the flat view feature

## Troubleshooting

- **Repository not showing plugins**: Check that `repository.json` is accessible via HTTPS
- **Download fails**: Verify the download URL in `repository.json` is correct
- **Plugin doesn't load**: Check that the DLL matches the TargetAbi version of your Jellyfin server
- **GitHub Pages not working**: Ensure the `gh-pages` branch exists and contains `repository.json`
