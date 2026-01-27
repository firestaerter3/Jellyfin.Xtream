# PR Status Dashboard

Central tracking document for all features' readiness for upstream contribution to Kevinjil/Jellyfin.Xtream.

**Last Updated:** 2026-01-27

---

## Overall Status

| Feature | Version | Status | Docs Complete | Tests Pass | Ready for PR |
|---------|---------|--------|---------------|------------|--------------|
| [01 - Flat View](#01-flat-view) | 0.9.1.0 | ✅ Implemented | 🟡 Partial | ✅ Yes | 🟡 Needs Testing |
| [02 - Unicode Pipe Support](#02-unicode-pipe-support) | 0.9.4.x | ✅ Implemented | ❌ Template | ✅ Yes | 🟢 Ready |
| [03 - Missing Episodes Fix](#03-missing-episodes-fix) | 0.9.4.x | ✅ Implemented | ❌ Template | ✅ Yes | 🟢 Ready |
| [04 - Eager Caching](#04-eager-caching) | 0.9.5.0+ | ✅ Implemented | ✅ Complete | ✅ Yes | 🟡 Needs Split |
| [05 - Malformed JSON](#05-malformed-json-handling) | 0.9.5.3 | ✅ Implemented | ❌ Template | ✅ Yes | 🟢 Ready |
| [06 - Clear Cache Cleanup](#06-clear-cache-cleanup) | 0.9.5.2 | ✅ Implemented | ❌ Template | ✅ Yes | 🟢 Ready |
| [07 - Config UI Errors](#07-config-ui-error-handling) | 0.9.4.x | ✅ Implemented | ❌ Template | 🟡 Partial | 🟡 Needs Review |

**Legend:**
- ✅ Complete / Pass
- 🟡 Partial / Needs Work
- ❌ Not Started
- 🟢 Ready for PR
- 🔴 Blocked

---

## Feature Details

### 01 - Flat View

**Status:** 🟡 Needs Upstream Testing

**Description:** Display all series/movies in flat list without categories

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [x] Documentation started (REQUIREMENTS.md complete)
- [ ] Documentation complete (need ARCH, IMPL, TEST, TODO, CHANGELOG)
- [x] Backward compatible (flat view can be disabled)
- [ ] Tested with upstream master branch
- [ ] Clean commit history
- [ ] PR description drafted

**Blockers:**
- None

**Next Steps:**
1. Complete remaining documentation (ARCHITECTURE, IMPLEMENTATION, etc.)
2. Create PR branch from upstream master
3. Cherry-pick flat view commits
4. Test independently
5. Submit PR

**Estimated Effort:** 2-3 hours

---

### 02 - Unicode Pipe Support

**Status:** 🟢 Ready for PR Submission

**Description:** Enhanced ParseName() to handle Unicode pipe characters (┃, │, ｜)

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [ ] Documentation complete (templates only)
- [x] Backward compatible (enhancement, no breaking changes)
- [ ] Tested with upstream master branch
- [ ] Clean commit history
- [ ] PR description drafted

**Blockers:**
- Documentation templates need to be filled

**Next Steps:**
1. Fill in documentation templates (quick - small feature)
2. Create PR branch from upstream master
3. Cherry-pick Unicode pipe commits
4. Test independently
5. Submit PR

**Estimated Effort:** 1-2 hours

**Related Commits:**
- Search for commits mentioning "unicode", "pipe", "┃", "│", "parsing"

---

### 03 - Missing Episodes Fix

**Status:** 🟢 Ready for PR Submission

**Description:** Fixed cache invalidation bug causing episodes not to display

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [ ] Documentation complete (templates only)
- [x] Backward compatible (bug fix)
- [ ] Tested with upstream master branch
- [ ] Clean commit history
- [ ] PR description drafted

**Blockers:**
- Documentation templates need to be filled

**Next Steps:**
1. Fill in documentation templates
2. Create PR branch from upstream master
3. Cherry-pick bug fix commits
4. Test independently
5. Submit PR

**Estimated Effort:** 1-2 hours

**Related Commits:**
- `ee5697b` - Fix cache invalidation causing episodes not to show and memory leak

---

### 04 - Eager Caching ⭐

**Status:** 🟡 Needs PR Splitting Strategy

**Description:** Pre-fetch and cache all series data, auto-populate Jellyfin DB for instant browsing

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [x] Documentation complete ✅
- [x] Backward compatible (can be disabled)
- [ ] Tested with upstream master branch
- [ ] Clean commit history (needs splitting)
- [ ] PR description drafted (will be multiple PRs)

**Blockers:**
- Feature is too large for single PR
- Needs to be split into 3-4 smaller PRs (see [TODO.md](../features/04-eager-caching/TODO.md#pr-splitting-strategy))

**Proposed PR Split:**
1. **PR 1:** Basic caching infrastructure (v0.9.4.6)
2. **PR 2:** UI controls and manual triggers (v0.9.4.10)
3. **PR 3:** True eager loading with DB population (v0.9.5.0)
4. **PR 4:** Bug fixes (malformed JSON, DB cleanup) (v0.9.5.2, v0.9.5.3)

**Next Steps:**
1. Decide on PR splitting strategy with upstream (if possible)
2. Create 4 separate PR branches
3. Cherry-pick commits to appropriate branches
4. Test each PR independently
5. Submit PRs in order (1 → 2 → 3 → 4)

**Estimated Effort:** 8-12 hours (for all 4 PRs)

**Related Commits:**
- `7046bc1` - Add cache maintenance buttons and configurable refresh frequency
- `ee5697b` - Fix cache invalidation causing episodes not to show and memory leak
- `3115144` - Fix Clear Cache not stopping cache refresh operation
- `de9fd6b` - Implement true eager loading by auto-populating Jellyfin database
- `111c298` - Clear Cache now triggers Jellyfin refresh to clean up jellyfin.db
- `6f5509e` - Fix malformed JSON responses from Xtream API (v0.9.5.3)

---

### 05 - Malformed JSON Handling

**Status:** 🟢 Ready for PR Submission

**Description:** Handle Xtream API returning `[]` instead of object for series data

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [ ] Documentation complete (templates only)
- [x] Backward compatible (error handling improvement)
- [ ] Tested with upstream master branch
- [ ] Clean commit history
- [ ] PR description drafted

**Blockers:**
- Documentation templates need to be filled
- May be included in Eager Caching PR #4 instead of standalone

**Next Steps:**
1. Decide if standalone PR or part of caching PR
2. Fill in documentation templates
3. Create PR branch (or include in caching PR #4)
4. Test independently
5. Submit PR

**Estimated Effort:** 1-2 hours (if standalone)

**Related Commits:**
- `6f5509e` - Fix malformed JSON responses from Xtream API (v0.9.5.3)

---

### 06 - Clear Cache DB Cleanup

**Status:** 🟢 Ready for PR Submission

**Description:** Clear Cache button now triggers Jellyfin refresh to remove orphaned items

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [ ] Documentation complete (templates only)
- [x] Backward compatible (enhancement)
- [ ] Tested with upstream master branch
- [ ] Clean commit history
- [ ] PR description drafted

**Blockers:**
- Documentation templates need to be filled
- Should be included in Eager Caching PR #4 (not standalone)

**Next Steps:**
1. Include in Eager Caching PR #4
2. Fill in documentation templates for reference
3. Test as part of caching PR

**Estimated Effort:** Included in caching PR

**Related Commits:**
- `111c298` - Clear Cache now triggers Jellyfin refresh to clean up jellyfin.db

---

### 07 - Config UI Error Handling

**Status:** 🟡 Needs Code Review and Testing

**Description:** Improved error handling in configuration UI (Clear Cache stuck, button feedback)

**Checklist:**
- [x] Code implemented
- [x] Manual testing complete
- [ ] Documentation complete (templates only)
- [x] Backward compatible (UI improvement)
- [ ] Code review for UI best practices
- [ ] Tested with upstream master branch
- [ ] Clean commit history
- [ ] PR description drafted

**Blockers:**
- Documentation templates need to be filled
- UI code should be reviewed for best practices
- May be included in Eager Caching PR #2 (UI controls)

**Next Steps:**
1. Review UI code for best practices
2. Decide if standalone or part of caching PR #2
3. Fill in documentation templates
4. Test independently
5. Submit PR

**Estimated Effort:** 2-3 hours

**Related Commits:**
- `02cfd2d` - Fix Clear Cache button getting stuck on 'Clearing...'
- `43f293c` - Fix Clear Cache button showing 'Clearing' forever

---

## Upstream Contribution Strategy

### Recommended PR Order

1. **Unicode Pipe Support** (PR #1)
   - **Why First:** Small, simple, no dependencies
   - **Impact:** Low risk, immediate value
   - **Estimated Review Time:** 1-2 days

2. **Missing Episodes Fix** (PR #2)
   - **Why Second:** Bug fix, no dependencies
   - **Impact:** Medium risk, fixes reported issue
   - **Estimated Review Time:** 2-3 days

3. **Flat View Feature** (PR #3)
   - **Why Third:** Core feature, well-tested, popular request
   - **Impact:** Medium risk, major UX improvement
   - **Estimated Review Time:** 1-2 weeks
   - **Note:** Wait for feedback before continuing

4. **Eager Caching - Part 1** (PR #4a)
   - **Why Fourth:** Foundation for performance improvements
   - **Impact:** Medium risk, opt-in feature
   - **Estimated Review Time:** 1-2 weeks

5. **Eager Caching - Part 2** (PR #4b)
   - **Dependencies:** Part 1 merged
   - **Impact:** Medium risk
   - **Estimated Review Time:** 1 week

6. **Eager Caching - Part 3** (PR #4c)
   - **Dependencies:** Part 2 merged
   - **Impact:** Medium risk, major feature completion
   - **Estimated Review Time:** 1-2 weeks

7. **Eager Caching - Part 4** (PR #4d)
   - **Dependencies:** Part 3 merged
   - **Impact:** Low risk, bug fixes
   - **Estimated Review Time:** 3-5 days

8. **Config UI Error Handling** (PR #5 or included in #4b)
   - **Why Last:** Low priority, nice-to-have
   - **Impact:** Low risk, UX polish
   - **Estimated Review Time:** 3-5 days

---

## Timeline Estimate

### Optimistic (Upstream Responsive)
- **Weeks 1-2:** PRs #1-3 (small features, bug fixes, flat view)
- **Weeks 3-6:** PR #4a-b (caching foundation)
- **Weeks 7-10:** PR #4c-d (caching completion)
- **Week 11:** PR #5 (UI polish)

**Total:** ~3 months

### Realistic (Upstream Busy)
- **Month 1:** PRs #1-3
- **Month 2-4:** PR #4a-b (caching foundation)
- **Month 5-6:** PR #4c-d (caching completion)
- **Month 7:** PR #5 (UI polish)

**Total:** ~6-7 months

### Pessimistic (Upstream Inactive)
- Maintain fork indefinitely
- Contribute documentation and guides
- Help other users adopt fork

---

## Pre-Submission Checklist

Before submitting ANY PR:

### Code Quality
- [ ] Code follows C# coding conventions
- [ ] StyleCop warnings resolved
- [ ] No hardcoded values or magic numbers
- [ ] Error handling comprehensive
- [ ] Logging covers all paths (info, warning, error)

### Testing
- [ ] Manual testing complete
- [ ] Tested with clean Jellyfin install
- [ ] Tested with upstream master branch
- [ ] Backward compatibility verified
- [ ] No regressions in existing features

### Documentation
- [ ] Feature documentation complete
- [ ] Code comments on complex logic
- [ ] Configuration options documented
- [ ] CHANGELOG entry written
- [ ] PR description clear and detailed

### Git
- [ ] Clean commit history (squashed if needed)
- [ ] Meaningful commit messages
- [ ] No merge commits (rebased on upstream master)
- [ ] No unrelated changes
- [ ] build.yaml version NOT bumped (upstream decides)

### PR Description
- [ ] Problem statement clear
- [ ] Solution explained
- [ ] Breaking changes noted (if any)
- [ ] Testing instructions provided
- [ ] Screenshots/GIFs (for UI changes)
- [ ] References to issues (if applicable)

---

## Upstream Contact

**Upstream Repository:** https://github.com/Kevinjil/Jellyfin.Xtream
**Maintainer:** Kevin Jilissen (Kevinjil)
**Last Upstream Activity:** [Check GitHub]

**Before Submitting First PR:**
- [ ] Check if Kevin is still active on GitHub
- [ ] Review recent PRs/issues for response time
- [ ] Consider opening discussion issue first
- [ ] Gauge interest in features before large PRs

---

## Risk Assessment

### Low Risk Features (Submit First)
- Unicode Pipe Support (small, isolated change)
- Missing Episodes Fix (bug fix)
- Malformed JSON Handling (error handling)

### Medium Risk Features (Submit After Low)
- Flat View (new feature, but well-tested)
- Clear Cache DB Cleanup (enhancement to existing)
- Config UI Error Handling (UI improvement)

### High Risk Features (Submit Last, Split Up)
- Eager Caching (large feature, architectural change)
  - Risk: Complexity, performance implications, testing burden
  - Mitigation: Split into 4 PRs, extensive documentation

---

## Success Metrics

### PR Acceptance Rate
- **Target:** 80%+ of PRs merged
- **Metric:** (Merged PRs) / (Submitted PRs)

### Review Turnaround
- **Target:** < 2 weeks average review time
- **Metric:** Days from PR submission to merge/close

### User Adoption
- **Target:** 50+ users of fork features
- **Metric:** GitHub stars, issues, discussions

### Code Quality
- **Target:** 0 regressions reported
- **Metric:** Issues opened after feature merge

---

## Alternative Strategies

### If Upstream Inactive
1. **Maintain Fork:** Continue development on fork
2. **Community Takeover:** Offer to become maintainer
3. **Hard Fork:** Rename project, publish independently
4. **Document Only:** Provide guides for users to adopt features

### If PRs Rejected
1. **Request Feedback:** Ask what changes needed
2. **Revise and Resubmit:** Address concerns
3. **Explain Value:** Provide user testimonials, metrics
4. **Fork Maintenance:** Keep fork updated with upstream changes

---

## Notes

- This dashboard should be updated weekly during active PR submission phase
- Each feature's status should be updated as work progresses
- Blockers should be escalated if blocking multiple features
- Timeline estimates should be revised based on actual upstream response times

---

**Last Review:** 2026-01-27
**Next Review:** [Set date when PR submissions begin]
