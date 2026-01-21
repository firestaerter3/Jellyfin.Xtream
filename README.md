# Jellyfin Xtream Plugin - Flat Series View Fork

**Project Goal:** Fork and modify the Jellyfin Xtream plugin to add a "flat series view" feature that displays all series directly without category folders.

---

## 📁 Project Structure

```
Jellyfin-Xtream-FlatView/
├── README.md                                    # This file
├── docs/
│   └── JELLYFIN_XTREAM_FLAT_VIEW_IMPLEMENTATION.md  # Detailed implementation plan
└── scripts/
    └── fork_xtream_plugin.sh                   # Setup script for forking
```

---

## 🚀 Quick Start

### 1. Fork the Repository

1. Go to https://github.com/Kevinjil/Jellyfin.Xtream
2. Click "Fork" to create your own copy

### 2. Run Setup Script

```bash
cd "/path/to/repo/Documents/Coding Projects/Jellyfin-Xtream-FlatView"
./scripts/fork_xtream_plugin.sh
```

This will:
- Clone your forked repository
- Create a feature branch
- Analyze the codebase
- Set up the development environment

### 3. Review Implementation Plan

Read the detailed plan:
```bash
cat docs/JELLYFIN_XTREAM_FLAT_VIEW_IMPLEMENTATION.md
```

### 4. Start Development

Follow the implementation plan to:
- Add configuration property
- Modify series creation logic
- Update UI configuration
- Test thoroughly

---

## 📋 Feature Overview

### Current Behavior
```
Xtream Series
  └── Category Folder
      └── Series 1
      └── Series 2
```

### Desired Behavior (Flat View)
```
Xtream Series
  └── Series 1
  └── Series 2
  └── Series 3
  (all series directly, no category folders)
```

---

## 🛠️ Development Requirements

- **.NET SDK:** 8.0+ (match Jellyfin server version)
- **IDE:** Visual Studio 2022 or VS Code with C# extensions
- **Jellyfin Server:** Local instance for testing
- **Xtream Provider:** Test credentials for API access

---

## 📚 Resources

- **Original Repository:** https://github.com/Kevinjil/Jellyfin.Xtream
- **Jellyfin Plugin Docs:** https://jellyfin.org/docs/general/development/plugins/
- **Implementation Plan:** `docs/JELLYFIN_XTREAM_FLAT_VIEW_IMPLEMENTATION.md`

---

## 📝 Status

- [x] Project structure created
- [x] Implementation plan documented
- [x] Setup script created
- [ ] Repository forked
- [ ] Feature branch created
- [ ] Code analysis completed
- [ ] Configuration changes implemented
- [ ] Core logic implemented
- [ ] UI updates completed
- [ ] Testing completed
- [ ] Documentation updated
- [ ] Release created

---

**Last Updated:** January 21, 2026
