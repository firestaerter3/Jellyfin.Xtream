# Pull Request Workflow

Comprehensive guide for submitting features to upstream Kevinjil/Jellyfin.Xtream repository.

---

## Prerequisites

### Before You Start

1. **Upstream Check**
   ```bash
   # Verify upstream is configured
   git remote -v
   # Should show:
   # upstream https://github.com/Kevinjil/Jellyfin.Xtream.git (fetch)
   # upstream https://github.com/Kevinjil/Jellyfin.Xtream.git (push)
   ```

2. **Maintainer Activity Check**
   - Visit https://github.com/Kevinjil/Jellyfin.Xtream
   - Check last commit date
   - Review open/closed PRs
   - Check issue response times

3. **Feature Selection**
   - Review [PR Status Dashboard](PR_STATUS.md)
   - Choose a feature marked 🟢 Ready or 🟡 Needs Work
   - Ensure all blockers are resolved

---

## Step-by-Step PR Workflow

### Phase 1: Preparation

#### 1.1 Sync with Upstream
```bash
# Fetch latest upstream changes
git fetch upstream

# Check for upstream changes
git log master..upstream/master --oneline

# If upstream has changes, merge them
git checkout master
git merge upstream/master
git push origin master
```

#### 1.2 Verify Feature is Ready
Use [PR Status Dashboard](PR_STATUS.md) checklist:
- [ ] Code implemented and tested
- [ ] Documentation complete
- [ ] Manual testing passed
- [ ] Backward compatible
- [ ] Clean commit history

#### 1.3 Create PR Branch
```bash
# Branch naming: pr/<feature-name>
git checkout -b pr/unicode-pipe-support upstream/master

# Verify you're on upstream master
git log --oneline -5
```

---

### Phase 2: Cherry-Picking Commits

#### 2.1 Identify Relevant Commits
```bash
# Find commits for this feature
git log master --oneline --grep="unicode\|pipe"

# Example output:
# abc1234 - Add Unicode pipe support to ParseName()
# def5678 - Fix Unicode pipe handling edge case
# ghi9012 - Add tests for Unicode pipes
```

#### 2.2 Cherry-Pick Commits
```bash
# Cherry-pick commits (oldest to newest)
git cherry-pick abc1234
git cherry-pick def5678
git cherry-pick ghi9012

# If conflicts occur:
git status  # See conflicting files
# Resolve conflicts manually
git add <resolved-files>
git cherry-pick --continue
```

#### 2.3 Squash if Needed
```bash
# If multiple commits should be one:
git rebase -i upstream/master

# In editor, change "pick" to "squash" for commits to combine
# Save and edit final commit message
```

---

### Phase 3: Testing

#### 3.1 Build and Test
```bash
# Build the plugin
dotnet build Jellyfin.Xtream/Jellyfin.Xtream.csproj

# Check for warnings/errors
# With TreatWarningsAsErrors, all must be clean
```

#### 3.2 Manual Testing
Follow test plan in feature's `TEST_PLAN.md`:
1. Deploy to test Jellyfin instance
2. Run all test cases
3. Verify backward compatibility
4. Test with clean configuration

#### 3.3 Verify No Regressions
```bash
# Test features that might be affected
# - Original category navigation
# - Series/VOD playback
# - Configuration UI
# - Plugin enable/disable
```

---

### Phase 4: Documentation

#### 4.1 Review Feature Documentation
Ensure feature's documentation is complete:
- [ ] `REQUIREMENTS.md` - Clear problem statement and requirements
- [ ] `ARCHITECTURE.md` - Design decisions explained
- [ ] `IMPLEMENTATION.md` - Code changes documented
- [ ] `TEST_PLAN.md` - Test cases defined
- [ ] `CHANGELOG.md` - Changes summarized

#### 4.2 Prepare PR Description
Use this template:

```markdown
## Description

[Brief overview of what this PR does]

## Problem Statement

[What problem does this solve? Link to issues if applicable]

## Solution

[How does this PR solve the problem? High-level approach]

## Changes

- Added: [List new features/files]
- Modified: [List changed files/behavior]
- Fixed: [List bugs fixed]

## Testing

### Manual Testing
- [ ] Tested with Jellyfin 10.X.Y
- [ ] Tested with clean configuration
- [ ] Tested backward compatibility (feature can be disabled)
- [ ] Verified no regressions in existing features

### Test Cases
1. [Test case 1 description]
   - Expected: [result]
   - Actual: [result]
2. [Test case 2 description]
   - Expected: [result]
   - Actual: [result]

## Screenshots/GIFs

[For UI changes, include before/after screenshots]

## Backward Compatibility

- [x] This PR is backward compatible
- [ ] This PR has breaking changes (describe below)

[If breaking changes, describe migration path]

## Configuration

[New configuration options, if any]

```toml
# Example configuration
FlattenSeriesView = false  # Default
```

## Performance Impact

[Describe any performance implications]
- Cache hit: ~Xms
- Cache miss: ~Yms
- Memory usage: ~ZMB for N items

## Documentation

- [x] Code comments added for complex logic
- [x] Configuration options documented
- [x] Feature documentation in fork: [link to docs]

## Checklist

- [ ] Code follows project coding conventions
- [ ] StyleCop warnings resolved
- [ ] No merge commits (rebased on master)
- [ ] Commit messages are clear and descriptive
- [ ] Manual testing completed
- [ ] Backward compatibility verified
- [ ] Documentation updated

## Related Issues

Fixes #123
Closes #456
```

---

### Phase 5: Submission

#### 5.1 Push PR Branch
```bash
# Push to your fork
git push origin pr/unicode-pipe-support

# If force-push needed (after rebase):
git push origin pr/unicode-pipe-support --force-with-lease
```

#### 5.2 Create Pull Request
1. Go to https://github.com/Kevinjil/Jellyfin.Xtream
2. Click "Pull Requests" tab
3. Click "New Pull Request"
4. Click "compare across forks"
5. Set base: `Kevinjil/Jellyfin.Xtream:master`
6. Set compare: `rolandb5/Jellyfin.Xtream:pr/unicode-pipe-support`
7. Click "Create Pull Request"
8. Fill in PR title and description (prepared in Phase 4)
9. Click "Create Pull Request"

#### 5.3 Request Review
If Kevin doesn't have auto-review:
- Add comment: "@Kevinjil Could you review this when you have time? Thanks!"
- Be patient - maintainers are volunteers

---

### Phase 6: Review Process

#### 6.1 Respond to Feedback
- Check GitHub notifications daily
- Respond to comments within 48 hours
- Be polite and receptive to feedback
- Ask questions if feedback is unclear

#### 6.2 Making Changes
```bash
# Make requested changes on PR branch
git checkout pr/unicode-pipe-support

# Edit files as requested
vim Jellyfin.Xtream/Service/StreamService.cs

# Commit changes
git add .
git commit -m "Address review feedback: improve error handling"

# Push changes
git push origin pr/unicode-pipe-support

# PR automatically updates
```

#### 6.3 Rebasing if Needed
```bash
# If upstream master changed during review
git fetch upstream
git rebase upstream/master

# Resolve conflicts if any
git add <resolved-files>
git rebase --continue

# Force-push (PR updated automatically)
git push origin pr/unicode-pipe-support --force-with-lease
```

---

### Phase 7: Post-Merge

#### 7.1 Update Fork
```bash
# After PR merged
git fetch upstream
git checkout master
git merge upstream/master
git push origin master
```

#### 7.2 Clean Up
```bash
# Delete local PR branch
git branch -d pr/unicode-pipe-support

# Delete remote PR branch
git push origin --delete pr/unicode-pipe-support
```

#### 7.3 Update Documentation
- Update [PR Status Dashboard](PR_STATUS.md)
- Mark feature as ✅ Merged
- Celebrate! 🎉

---

## Common Scenarios

### Scenario 1: PR Rejected

**If maintainer rejects PR:**

1. **Ask for Clarification**
   ```
   Hi @Kevinjil, thanks for reviewing. Could you clarify what changes you'd like to see?
   I'm happy to revise this PR to meet your requirements.
   ```

2. **Revise if Reasonable**
   - Address concerns
   - Make requested changes
   - Re-request review

3. **Close if Irreconcilable**
   - Thank maintainer for their time
   - Close PR politely
   - Maintain feature in fork

### Scenario 2: No Response

**If maintainer doesn't respond after 2 weeks:**

1. **Polite Ping**
   ```
   Hi @Kevinjil, just checking if you had a chance to review this PR.
   No rush - just want to make sure it's on your radar. Thanks!
   ```

2. **Second Ping (4 weeks)**
   ```
   Hi @Kevinjil, following up on this PR. Is there anything I can do to help move this forward?
   Happy to address any concerns. Thanks!
   ```

3. **After 8 weeks**
   - Maintain feature in fork
   - Document in fork README: "Feature awaiting upstream review"
   - Continue independent development

### Scenario 3: Conflicts During Review

**If upstream merges something that conflicts with your PR:**

1. **Rebase on Latest Master**
   ```bash
   git fetch upstream
   git rebase upstream/master
   # Resolve conflicts
   git push origin pr/feature --force-with-lease
   ```

2. **Comment on PR**
   ```
   Rebased on latest master. Conflicts resolved. Ready for re-review.
   ```

### Scenario 4: Breaking Changes Requested

**If maintainer wants breaking changes:**

1. **Discuss Alternatives**
   ```
   I understand the concern. Would it be acceptable to:
   - Add a configuration option for backward compatibility?
   - Phase in the change over two releases?
   - Provide migration guide for users?
   ```

2. **Implement Migration Path**
   - Add deprecation warnings
   - Provide migration script/guide
   - Update documentation

---

## Best Practices

### Communication

**Do:**
- ✅ Be polite and professional
- ✅ Respond to feedback within 48 hours
- ✅ Ask questions if unclear
- ✅ Thank reviewers for their time
- ✅ Provide context and reasoning

**Don't:**
- ❌ Argue or be defensive
- ❌ Ghost after submitting PR
- ❌ Make demands on maintainer's time
- ❌ Submit rushed PRs without testing
- ❌ Ignore coding conventions

### Code Quality

**Do:**
- ✅ Follow existing code style
- ✅ Add comments for complex logic
- ✅ Write descriptive commit messages
- ✅ Test thoroughly before submitting
- ✅ Keep PRs focused on one feature

**Don't:**
- ❌ Submit untested code
- ❌ Include unrelated changes
- ❌ Use magic numbers or hardcoded values
- ❌ Leave commented-out code
- ❌ Submit with warnings or errors

### PR Size

**Small PR (< 200 lines):**
- Fast review
- Easy to understand
- Low risk
- Example: Unicode pipe support, bug fixes

**Medium PR (200-500 lines):**
- Moderate review time
- Requires documentation
- Medium risk
- Example: Flat view feature

**Large PR (> 500 lines):**
- Long review time
- Requires extensive documentation
- High risk
- **Recommendation:** Split into smaller PRs
- Example: Eager caching (split into 4 PRs)

---

## Troubleshooting

### Problem: Cherry-Pick Conflicts

**Solution:**
```bash
# See conflicting files
git status

# Resolve conflicts manually in editor
vim <conflicting-file>

# Stage resolved files
git add <resolved-files>

# Continue cherry-pick
git cherry-pick --continue
```

### Problem: Failed Build After Cherry-Pick

**Solution:**
```bash
# Check build errors
dotnet build

# Fix errors (may need to adapt code to upstream)
vim <file-with-errors>

# Amend commit with fixes
git add .
git commit --amend
```

### Problem: PR Shows Extra Commits

**Cause:** Branch not based on upstream/master

**Solution:**
```bash
# Create new PR branch correctly
git checkout -b pr/feature-fixed upstream/master

# Cherry-pick only relevant commits
git cherry-pick <commit1> <commit2> ...
```

### Problem: StyleCop Warnings

**Solution:**
```bash
# Build shows warnings
dotnet build Jellyfin.Xtream/Jellyfin.Xtream.csproj

# Fix warnings (plugin uses TreatWarningsAsErrors)
# Common issues:
# - SA1028: Trailing whitespace
# - CA1860: Use Count > 0 instead of Any()

# Amend commit
git add .
git commit --amend
```

---

## Checklists

### Pre-Submission Checklist

Use this before submitting every PR:

```markdown
## Code
- [ ] Code builds without errors or warnings
- [ ] StyleCop rules followed
- [ ] No hardcoded values or magic numbers
- [ ] Error handling comprehensive
- [ ] Logging covers all paths

## Testing
- [ ] Manual testing complete (all test cases pass)
- [ ] Tested with clean Jellyfin install
- [ ] Tested backward compatibility
- [ ] No regressions in existing features
- [ ] Tested on upstream master branch

## Git
- [ ] Clean commit history (squashed if needed)
- [ ] Meaningful commit messages
- [ ] No merge commits (rebased on upstream/master)
- [ ] No unrelated changes
- [ ] Branch based on upstream/master

## Documentation
- [ ] Code comments on complex logic
- [ ] PR description clear and complete
- [ ] Configuration options documented
- [ ] Breaking changes noted (if any)
- [ ] Testing instructions in PR description

## Courtesy
- [ ] Checked upstream activity (maintainer active?)
- [ ] Reviewed similar PRs for context
- [ ] Prepared to respond to feedback quickly
- [ ] Willing to revise based on feedback
```

### Post-Merge Checklist

After PR is merged:

```markdown
- [ ] Updated fork master branch
- [ ] Deleted PR branch (local and remote)
- [ ] Updated PR Status Dashboard
- [ ] Updated feature documentation with PR link
- [ ] Celebrated with team/community 🎉
- [ ] Posted update in fork README (if applicable)
```

---

## Examples

### Example 1: Small PR (Unicode Pipe Support)

**Commits to Cherry-Pick:**
```bash
abc1234 - Add Unicode pipe support to ParseName()
```

**PR Title:**
```
Add Unicode pipe character support to title parsing
```

**PR Description (abbreviated):**
```markdown
## Description
Extends ParseName() to handle Unicode pipe characters (┃, │, ｜)
in addition to ASCII pipe (|).

## Problem
Some Xtream providers use Unicode pipes in titles (e.g., ┃NL┃ Breaking Bad)
which were not being stripped, causing inconsistent display.

## Solution
Added regex patterns for Unicode pipe variants in ParseName() method.

## Changes
- Modified: Jellyfin.Xtream/Service/StreamService.cs:ParseName()
  - Added patterns for U+2503, U+2502, U+FF5C

## Testing
- [x] Tested with titles containing ┃NL┃
- [x] Tested with titles containing │HD│
- [x] Verified ASCII | still works
- [x] Verified backward compatibility

## Backward Compatibility
- [x] This PR is backward compatible
```

---

## Resources

- **Upstream Repository:** https://github.com/Kevinjil/Jellyfin.Xtream
- **Fork Repository:** https://github.com/rolandb5/Jellyfin.Xtream
- **PR Status Dashboard:** [PR_STATUS.md](PR_STATUS.md)
- **GitHub PR Guide:** https://docs.github.com/en/pull-requests
- **Git Cherry-Pick Guide:** https://git-scm.com/docs/git-cherry-pick

---

**Last Updated:** 2026-01-27
