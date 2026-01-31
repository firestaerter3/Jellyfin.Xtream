# Jellyfin.Xtream (Flat View Fork)

A fork of [Jellyfin.Xtream](https://github.com/Kevinjil/Jellyfin.Xtream) with enhanced features for better browsing and metadata.

## What's Different in This Fork?

| Feature | Description |
|---------|-------------|
| **Flat View** | Browse all series/movies in a single alphabetical list instead of navigating through category folders |
| **Eager Caching** | Pre-fetches all series data at startup for instant browsing - no more waiting for API calls |
| **TVDb Artwork** | Automatically replaces low-quality provider images with high-quality artwork from TVDb |
| **Title Override Map** | Manually map series titles to TVDb IDs when automatic lookup fails |
| **Delta Sync** | Only syncs changed items to Jellyfin database, reducing refresh time |
| **Better Error Handling** | Configuration pages show helpful error messages instead of failing silently |

## Installation

Add this fork's repository to Jellyfin:

1. Open admin dashboard → `Plugins` → `Repositories` tab
2. Click `+` to add a repository
3. **Name:** `Jellyfin Xtream (Flat View)`
4. **URL:** `https://firestaerter3.github.io/jellyfin-plugin-repo/manifest.json`
5. Click Save

Install the plugin:

1. Go to `Plugins` → `Catalog` tab
2. Under `Live TV`, select `Jellyfin Xtream (Flat View)`
3. Click `Install` and restart Jellyfin

## Features

### Flat View Mode
Skip category folders and see all your content in one place:
- **Series:** Enable "Flatten Series View" in Series config tab
- **VOD/Movies:** Enable "Flatten VOD View" in Video On-Demand config tab

### Eager Caching
Pre-loads all series, seasons, and episodes at startup:
- Enable "Enable Series Caching" in Series config tab
- Set refresh interval (default: 60 minutes)
- Click "Refresh Cache" for manual refresh
- Cache persists across restarts

### TVDb Artwork Integration
Automatically fetches high-quality artwork during cache refresh:
- Enabled by default when caching is on
- Uses Jellyfin's TVDb plugin for lookups
- For series that can't be found, use Title Override Map:
  ```
  My Local Series Name=12345
  Another Series=67890
  ```
  (Enter TVDb series IDs, one mapping per line)

## Configuration

### Credentials
| Property | Description |
|----------|-------------|
| Base URL | Xtream API URL (e.g., `https://provider.example.com`) |
| Username | Your Xtream username |
| Password | Your Xtream password |

### Live TV / VOD / Series
1. Open the respective configuration tab
2. Enable "Show this channel to users"
3. Select categories/items you want available
4. (Optional) Enable Flat View for single-list browsing
5. (Optional) Enable Caching for instant loading
6. Click Save

## Upstream Contributions

All features in this fork are being contributed back to the original project:

| Feature | Upstream PR | Status |
|---------|-------------|--------|
| Flat View | [#283](https://github.com/Kevinjil/Jellyfin.Xtream/pull/283) | Under review |
| Unicode Pipe Support | [#281](https://github.com/Kevinjil/Jellyfin.Xtream/pull/281) | Under review |
| Malformed JSON Handling | [#282](https://github.com/Kevinjil/Jellyfin.Xtream/pull/282) | Under review |
| Missing Episodes Fix | [#284](https://github.com/Kevinjil/Jellyfin.Xtream/pull/284) | Under review |
| Config UI Errors | [#285](https://github.com/Kevinjil/Jellyfin.Xtream/pull/285) | Under review |
| Caching + Metadata | [#286](https://github.com/Kevinjil/Jellyfin.Xtream/pull/286) | Under review |

## Known Issues

### Credential Exposure
Jellyfin publishes remote paths in the API which include Xtream credentials. Use caution on shared servers.

## Links

- **This Fork:** https://github.com/firestaerter3/Jellyfin.Xtream
- **Original Project:** https://github.com/Kevinjil/Jellyfin.Xtream
- **Jellyfin:** https://jellyfin.org
