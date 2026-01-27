# Jellyfin Xtream Flat View - Documentation Index

Welcome to the documentation for the Jellyfin Xtream Flat View plugin. This documentation covers features, architecture, implementation details, and testing.

> **For maintainers:** Internal planning documents, AI context files, and strategic notes are in a separate private repository.

---

## 📚 Documentation Structure

### Features

Each feature is documented with a complete set of files covering requirements, architecture, implementation, and testing.

#### [01 - Flat View](features/01-flat-view/REQUIREMENTS.md)
**Status:** ✅ Implemented (v0.9.1.0)
**PR Ready:** 🟢 Ready for upstream submission
**Testing:** ✅ 8/8 automated + 29/33 manual tests pass
**Documentation:** ✅ Complete

Display all series/movies in a single alphabetical list without category folders.

- [Requirements](features/01-flat-view/REQUIREMENTS.md) - User stories and functional requirements
- [Architecture](features/01-flat-view/ARCHITECTURE.md) - Design decisions and data flow
- [Implementation](features/01-flat-view/IMPLEMENTATION.md) - Detailed code changes
- [Test Plan](features/01-flat-view/TEST_PLAN.md) - Automated and manual test cases
- [Changelog](features/01-flat-view/CHANGELOG.md) - Version history and evolution

#### [02 - Unicode Pipe Support](features/02-unicode-pipe-support/REQUIREMENTS.md)
**Status:** ✅ Implemented (v0.9.4.x)
**PR Ready:** 🟢 Ready for upstream submission
**Testing:** ✅ 12/12 automated + 14/15 manual tests pass
**Documentation:** ✅ Complete

Enhanced title parsing to handle Unicode pipe characters (┃, │, ｜) in addition to ASCII pipes.

- [Requirements](features/02-unicode-pipe-support/REQUIREMENTS.md) - User stories and functional requirements
- [Architecture](features/02-unicode-pipe-support/ARCHITECTURE.md) - Regex design and character handling
- [Implementation](features/02-unicode-pipe-support/IMPLEMENTATION.md) - Code changes (1 file, 5 lines)
- [Test Plan](features/02-unicode-pipe-support/TEST_PLAN.md) - Automated and manual test cases
- [Changelog](features/02-unicode-pipe-support/CHANGELOG.md) - Version history

#### [03 - Missing Episodes Fix](features/03-missing-episodes-fix/REQUIREMENTS.md)
**Status:** ✅ Implemented (v0.9.4.x)
**PR Ready:** 🟢 Ready for upstream submission
**Testing:** Manual tests defined
**Documentation:** ✅ Complete

Fixed bug where episodes wouldn't display due to Xtream API storing episodes under incorrect dictionary keys.

- [Requirements](features/03-missing-episodes-fix/REQUIREMENTS.md) - User stories and functional requirements
- [Architecture](features/03-missing-episodes-fix/ARCHITECTURE.md) - Two-phase lookup strategy
- [Implementation](features/03-missing-episodes-fix/IMPLEMENTATION.md) - Code changes in GetEpisodes method
- [Test Plan](features/03-missing-episodes-fix/TEST_PLAN.md) - Manual test cases
- [Changelog](features/03-missing-episodes-fix/CHANGELOG.md) - Version history

#### [04 - Eager Caching](features/04-eager-caching/REQUIREMENTS.md) ⭐ **Major Feature**
**Status:** ✅ Implemented (v0.9.5.0+)
**PR Ready:** 🟡 Needs upstream testing and splitting
**Testing:** ✅ 12/12 automated tests pass

Pre-fetch and cache all series data upfront, automatically populate Jellyfin database for instant browsing.

- [Requirements](features/04-eager-caching/REQUIREMENTS.md) - Comprehensive requirements document
- [Architecture](features/04-eager-caching/ARCHITECTURE.md) - Three-layer cache architecture
- [Implementation](features/04-eager-caching/IMPLEMENTATION.md) - Detailed code changes
- [Test Plan](features/04-eager-caching/TEST_PLAN.md) - Automated and manual test cases
- [Challenges](features/04-eager-caching/CHALLENGES.md) - Cache invalidation analysis
- [Changelog](features/04-eager-caching/CHANGELOG.md) - Complete version history

#### [05 - Malformed JSON Handling](features/05-malformed-json-handling/REQUIREMENTS.md)
**Status:** ✅ Implemented (v0.9.5.3)
**PR Ready:** 🟡 Bundle with Feature 04
**Testing:** Manual tests defined
**Documentation:** ✅ Complete

Handle cases where Xtream API returns malformed JSON (array instead of object for SeriesStreamInfo).

- [Requirements](features/05-malformed-json-handling/REQUIREMENTS.md) - User stories and functional requirements
- [Architecture](features/05-malformed-json-handling/ARCHITECTURE.md) - Detection and fallback design
- [Implementation](features/05-malformed-json-handling/IMPLEMENTATION.md) - Code changes in XtreamClient
- [Test Plan](features/05-malformed-json-handling/TEST_PLAN.md) - Manual test cases
- [Changelog](features/05-malformed-json-handling/CHANGELOG.md) - Version history

#### [06 - Clear Cache DB Cleanup](features/06-clear-cache-cleanup/REQUIREMENTS.md)
**Status:** ✅ Implemented (v0.9.5.2)
**PR Ready:** 🟡 Bundle with Feature 04
**Testing:** ✅ 16/16 testable cases pass
**Documentation:** ✅ Complete

Clear Cache button now triggers Jellyfin database cleanup to remove orphaned items.

- [Requirements](features/06-clear-cache-cleanup/REQUIREMENTS.md) - User stories and functional requirements
- [Architecture](features/06-clear-cache-cleanup/ARCHITECTURE.md) - Data flow and Jellyfin integration
- [Implementation](features/06-clear-cache-cleanup/IMPLEMENTATION.md) - Code changes and edge cases
- [Test Plan](features/06-clear-cache-cleanup/TEST_PLAN.md) - Manual test cases
- [Changelog](features/06-clear-cache-cleanup/CHANGELOG.md) - Version history

#### [07 - Config UI Error Handling](features/07-config-ui-error-handling/REQUIREMENTS.md)
**Status:** ✅ Implemented (v0.9.4.x)
**PR Ready:** 🟡 Bundle with Feature 04
**Testing:** Manual tests defined
**Documentation:** ✅ Complete

Improved error handling and user feedback in configuration UI (Clear Cache stuck, error stacking, progress display).

- [Requirements](features/07-config-ui-error-handling/REQUIREMENTS.md) - User stories and functional requirements
- [Architecture](features/07-config-ui-error-handling/ARCHITECTURE.md) - UI patterns and data flow
- [Implementation](features/07-config-ui-error-handling/IMPLEMENTATION.md) - Code changes across JS/C# files
- [Test Plan](features/07-config-ui-error-handling/TEST_PLAN.md) - Manual test cases
- [Changelog](features/07-config-ui-error-handling/CHANGELOG.md) - Version history

---

### Reference Documentation

General guides and reference materials for development and deployment.

- **[Project Context](reference/PROJECT_CONTEXT.md)** - Comprehensive project overview and history
- **[Build Errors Prevention](reference/BUILD_ERRORS_PREVENTION.md)** - Common build errors and solutions
- **[Repository Setup](reference/REPOSITORY_SETUP.md)** - How to set up the development environment
- **[Deployment Guide](reference/DEPLOYMENT.md)** - Docker deployment, file permissions, troubleshooting
- **[Debugging Guide](reference/DEBUGGING.md)** - How to debug the plugin, read logs, common patterns

---

### Upstream Contribution

Documentation for contributing features back to the original Kevinjil/Jellyfin.Xtream repository.

- **[PR Proposal](upstream/PR_PROPOSAL.md)** - Strategy for upstream contributions

> **For maintainers:** Detailed PR workflow, status tracking, and strategic planning documents are in the private repository.

---

### Automation & Tools

Scripts to maintain documentation quality and consistency.

- **[Generate Feature](../scripts/docs/generate-feature.sh)** - Scaffold new feature documentation from templates
- **[Validate Structure](../scripts/docs/validate-structure.sh)** - Validate all features have required docs

---

## 🎯 Quick Navigation

### For New Contributors
1. Start with [Project Context](reference/PROJECT_CONTEXT.md)
2. Read [Repository Setup](reference/REPOSITORY_SETUP.md)
3. Review [Build Errors Prevention](reference/BUILD_ERRORS_PREVENTION.md)
4. Pick a feature to explore from the list above

### For PR Preparation
1. Review [PR Proposal](upstream/PR_PROPOSAL.md)
2. Ensure feature documentation is complete
3. Test thoroughly before submission

### For Feature Development
1. Run `scripts/docs/generate-feature.sh <num> <name>` to scaffold docs
2. Fill in requirements, architecture, implementation details
3. Create test plan and verify all cases pass
4. Update TODO and track progress

### For Contributors
See [CONTRIBUTING.md](../CONTRIBUTING.md) for:
- Development setup
- Code style guidelines
- How to submit changes
- Testing requirements

---

## 📊 Feature Status Legend

- ✅ **Implemented** - Feature is coded and tested
- 🟢 **PR Ready** - Feature is ready for upstream submission
- 🟡 **Needs Work** - Feature needs cleanup, testing, or documentation
- 🔴 **Blocked** - Feature is blocked by dependencies or upstream decisions
- ⭐ **Major Feature** - Significant feature requiring careful review

---

## 🔗 External Resources

- **Jellyfin Plugin Guide**: https://jellyfin.org/docs/general/server/plugins/
- **Jellyfin Channel Plugin Guide**: https://jellyfin.org/docs/general/server/plugins/channels/
- **Original Plugin (Upstream)**: https://github.com/Kevinjil/Jellyfin.Xtream
- **This Fork**: https://github.com/rolandb5/Jellyfin.Xtream
- **Plugin Repository**: https://rolandb5.github.io/Jellyfin.Xtream/repository.json
- **Xtream Codes API**: (provider-specific documentation)

---

## 📝 Documentation Conventions

### File Naming
- `REQUIREMENTS.md` - User stories, functional/non-functional requirements
- `ARCHITECTURE.md` - Design decisions, components, data flow
- `IMPLEMENTATION.md` - Code changes, technical details
- `TEST_PLAN.md` - Manual test cases, performance benchmarks
- `CHANGELOG.md` - Version history, breaking changes

> **Note:** Additional internal documentation files (CONTEXT.md for AI assistants, TODO.md for task tracking) are maintained in a separate private repository.

### Document Structure
Each feature follows a consistent template:
1. **Document Info** - Metadata (version, date, status)
2. **Main Content** - Feature-specific content
3. **References** - Links to related documentation

### Linking
- Use relative links within documentation: `[Link](../other-doc.md)`
- Link to code with file paths and line numbers: `Service/CacheService.cs:123`
- Cross-reference related features: `See [Feature 04](../04-eager-caching/REQUIREMENTS.md)`

### Code Examples
```csharp
// Use C# syntax highlighting for code blocks
public async Task<Result> MethodName()
{
    // Include context comments
    return result;
}
```

### Status Indicators
Use emoji for quick visual scanning:
- ✅ Complete/Pass
- ❌ Missing/Fail
- ⚠️ Warning/Incomplete
- 💡 Tip/Suggestion
- 🔴 Critical/Blocker

---

## 🤝 Contributing to Documentation

### Adding a New Feature
1. Run `scripts/docs/generate-feature.sh <num> <name>`
2. Fill in all template sections
3. Add entry to this INDEX.md
4. Update PR_STATUS.md dashboard
5. Run `scripts/docs/validate-structure.sh`

### Updating Existing Docs
1. Update relevant feature documentation
2. Update "Last Updated" date in document info
3. Add entry to feature's CHANGELOG.md

### Documentation Reviews
Before submitting PRs:
- [ ] All required documents exist and complete
- [ ] No `[Feature Name]` or `TODO:` placeholders remain
- [ ] Links are valid and working
- [ ] Code examples are accurate
- [ ] Test plans are verified

---

## 📧 Documentation Feedback

Found an issue with documentation? Have suggestions?
- Create an issue: https://github.com/rolandb5/Jellyfin.Xtream/issues
- Tag with `documentation` label
- Reference the specific doc file and section

---

## 🏆 Documentation Goals

This comprehensive documentation aims to:
1. **Reduce onboarding time** for new contributors
2. **Preserve institutional knowledge** across development sessions
3. **Facilitate upstream contributions** with well-documented features
4. **Support AI assistants** with detailed context for each feature
5. **Maintain consistency** across all features and PRs
6. **Track progress** toward upstream contribution goals

---

## 📅 Last Updated

- **Index Created:** 2026-01-27
- **Last Modified:** 2026-01-27
- **Documentation Version:** 1.3
- **Plugin Version:** 0.9.5.3

---

**Quick Links:** [CLAUDE.md](../CLAUDE.md) | [README.md](../README.md) | [build.yaml](../build.yaml)
