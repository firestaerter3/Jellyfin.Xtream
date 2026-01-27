# Unicode Pipe Support - Changelog

## Document Info
- **Feature:** Unicode Pipe Support
- **Last Updated:** 2026-01-27
- **Related:** [REQUIREMENTS.md](./REQUIREMENTS.md), [IMPLEMENTATION.md](./IMPLEMENTATION.md)

---

## Version History

### v0.9.4.x (2026-01-24) - Initial Release

**Commits:**
- `cc8425a` - Add support for Unicode pipe characters in tag parsing
- `19826f3` - Fix Unicode pipe tag parsing regex pattern

**Changes:**

#### Added
- Support for Unicode pipe characters in tag parsing:
  - `│` (U+2502) Box Drawings Light Vertical
  - `┃` (U+2503) Box Drawings Heavy Vertical
  - `｜` (U+FF5C) Fullwidth Vertical Line
- Optional whitespace handling inside tags (e.g., `┃ NL ┃`)
- Documentation comments with Unicode code points

#### Fixed
- Regex escaping issue with alternation pattern
- Changed from `(?:\||│|┃|｜)` to character class `[|│┃｜]`

#### Technical Details
- **File changed:** `Jellyfin.Xtream/Service/StreamService.cs`
- **Lines changed:** ~5 (minimal change)
- **Breaking changes:** None
- **API changes:** None

---

## Migration Notes

### Upgrading to v0.9.4.x

**No migration required:**
- Feature is automatic once plugin is updated
- No configuration changes needed
- No database changes

**Expected behavior change:**
- Titles with Unicode pipe tags (┃, │, ｜) will now be cleaned
- Previously these would display with tags intact
- Sorting may change for affected titles (now sorts by clean title)

---

## Breaking Changes

None. This is a backward-compatible enhancement.

---

## Deprecations

None.

---

## Known Issues

| Issue | Status | Workaround |
|-------|--------|------------|
| Empty title if name is only tags | By design | Provider issue - report to IPTV provider |

---

## Future Versions

### Potential v1.0.0 Enhancements

- Additional Unicode pipe variants if providers use them
- Unit tests for ParseName() when test framework is added

---

## References

- [REQUIREMENTS.md](./REQUIREMENTS.md) - Feature requirements
- [ARCHITECTURE.md](./ARCHITECTURE.md) - Design decisions
- [IMPLEMENTATION.md](./IMPLEMENTATION.md) - Code changes
- [TEST_PLAN.md](./TEST_PLAN.md) - Test cases
