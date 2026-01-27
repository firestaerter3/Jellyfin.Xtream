#!/bin/bash
# Generate feature documentation scaffold from templates
# Usage: ./generate-feature.sh <feature-number> <feature-name>
# Example: ./generate-feature.sh 05 malformed-json-handling

set -e

FEATURE_NUM=$1
FEATURE_NAME=$2
FEATURE_DIR="docs/features/${FEATURE_NUM}-${FEATURE_NAME}"

if [ -z "$FEATURE_NUM" ] || [ -z "$FEATURE_NAME" ]; then
    echo "Usage: $0 <feature-number> <feature-name>"
    echo "Example: $0 05 malformed-json-handling"
    exit 1
fi

# Create feature directory
mkdir -p "$FEATURE_DIR"

echo "Creating documentation scaffold for feature ${FEATURE_NUM}-${FEATURE_NAME}..."

# Create REQUIREMENTS.md template
cat > "${FEATURE_DIR}/REQUIREMENTS.md" << 'EOF'
# [Feature Name] - Requirements

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Implemented in:** v0.X.Y.Z
- **Related:** [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## 1. Executive Summary

### 1.1 Problem Statement

**User Pain Points:**
- [Describe the problem users face]

### 1.2 Solution

[Describe how this feature solves the problem]

### 1.3 Key Benefits

1. **Benefit 1**: [Description]
2. **Benefit 2**: [Description]

---

## 2. Functional Requirements

### FR-1: [Requirement Name]

**ID:** FR-1
**Priority:** High/Medium/Low
**Description:** [What the system shall do]

**Acceptance Criteria:**
- [ ] Criterion 1
- [ ] Criterion 2

---

## 3. Non-Functional Requirements

### NFR-1: Performance

**ID:** NFR-1
**Description:** [Performance requirements]

**Acceptance Criteria:**
- [ ] Metric 1
- [ ] Metric 2

---

## 4. Success Criteria

[How do we know this feature is successful?]

---

## 5. References

- [ARCHITECTURE.md](./ARCHITECTURE.md)
- [IMPLEMENTATION.md](./IMPLEMENTATION.md)
- [TEST_PLAN.md](./TEST_PLAN.md)
EOF

# Create ARCHITECTURE.md template
cat > "${FEATURE_DIR}/ARCHITECTURE.md" << 'EOF'
# [Feature Name] - Architecture

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27
- **Related:** [REQUIREMENTS.md](./REQUIREMENTS.md)

---

## 1. Overview

[High-level description of how this works]

---

## 2. Components

### Component 1: [Name]
- **File:** path/to/file.cs
- **Responsibility:** What it does
- **Key Methods:** List important methods

---

## 3. Data Flow

```
[User Action] → [Component 1] → [Component 2] → [Result]
```

---

## 4. Design Decisions

### Decision 1: [Title]
- **Context:** Why did we need to decide?
- **Options Considered:** What alternatives?
- **Decision:** What we chose
- **Rationale:** Why we chose it
- **Trade-offs:** What we gave up

---

## 5. References

- [REQUIREMENTS.md](./REQUIREMENTS.md)
- [IMPLEMENTATION.md](./IMPLEMENTATION.md)
EOF

# Create IMPLEMENTATION.md template
cat > "${FEATURE_DIR}/IMPLEMENTATION.md" << 'EOF'
# [Feature Name] - Implementation Details

## Document Info
- **Status:** Implemented/In Development
- **Version:** 0.X.Y.Z
- **Last Updated:** 2026-01-27
- **Related:** [REQUIREMENTS.md](./REQUIREMENTS.md), [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## Implementation Approach

[Step-by-step how we built this]

---

## Code Changes

### Files Modified

#### 1. **Jellyfin.Xtream/Path/File.cs**
   - **Line X-Y:** Added [description]
   - **Method `FunctionName()`:** [what it does]

### Files Added

#### 1. **Jellyfin.Xtream/NewFile.cs**
   - **Purpose:** [description]
   - **Key methods:** [list]

---

## Configuration

[New settings added]

---

## Edge Cases Handled

- **Case 1:** [Description and how handled]
- **Case 2:** [Description and how handled]

---

## Known Limitations

- Limitation 1
- Limitation 2

---

## Related Commits

- `abc1234` - [Commit message]
EOF

# Create CONTEXT.md template
cat > "${FEATURE_DIR}/CONTEXT.md" << 'EOF'
# [Feature Name] - Session Context

## Quick Reference
**Status:** [In Development / Implemented / PR Ready / PR Submitted / Merged]
**Version:** 0.X.Y.Z
**Last Updated:** 2026-01-27
**PR Link:** [URL if submitted]

---

## Key Files

1. `Jellyfin.Xtream/File1.cs` - [Purpose]
2. `Jellyfin.Xtream/File2.cs` - [Purpose]

---

## Critical Code Sections

### File: FileName.cs

**Lines X-Y:** MethodName()
```csharp
// Key code excerpt showing the critical logic
```

**Decision Rationale:**
- Why we chose this approach
- Trade-offs considered
- Performance implications

---

## Recent Changes Timeline

- **2026-01-27** (vX.Y.Z) - [Change description] [commit: abc1234]

---

## Open Questions & Blockers

- [ ] Question 1
- [ ] Question 2

---

## AI Assistant Gotchas

⚠️ **CRITICAL:** [Important warning]
💡 **TIP:** [Helpful tip]

---

## Session Handoff Notes

**Current State:** [Description]

**Next Steps:**
1. [Step 1]
2. [Step 2]

**Dependencies:** [What this depends on]

**Watch Out For:** [Gotchas]
EOF

# Create TEST_PLAN.md template
cat > "${FEATURE_DIR}/TEST_PLAN.md" << 'EOF'
# [Feature Name] - Test Plan

## Document Info
- **Version:** 1.0
- **Date:** 2026-01-27

---

## Manual Testing

### Test Case 1: [Description]

**Preconditions:**
- Setting X is configured
- Data Y exists

**Steps:**
1. Navigate to...
2. Click...
3. Verify...

**Expected Result:**
- Should see...
- Data should be...

**Actual Result:**
- ✅ PASS / ❌ FAIL
- Notes: [any observations]

---

## Integration Testing

- Test with Jellyfin version: X.Y.Z
- Test with Xtream provider: [name]

---

## Performance Testing

- Benchmark: [what to measure]
- Acceptable threshold: [metric]

---

## Regression Testing

- Verify existing features still work
- Check backward compatibility
EOF

# Create TODO.md template
cat > "${FEATURE_DIR}/TODO.md" << 'EOF'
# [Feature Name] - TODO

## Before PR Submission

- [ ] Update build.yaml version
- [ ] Update .csproj AssemblyVersion
- [ ] Add changelog entry
- [ ] Test on clean Jellyfin install
- [ ] Verify backward compatibility
- [ ] Run pre-commit checks

---

## Code Tasks

### High Priority
- [ ] Task 1

### Medium Priority
- [ ] Task 2

### Low Priority
- [ ] Task 3

---

## Documentation Tasks

- [ ] Update README.md
- [ ] Add code comments
- [ ] Document configuration options

---

## Future Enhancements

- [ ] Idea 1
- [ ] Idea 2

---

## Blocked Items

- [ ] Item blocked by [reason]
EOF

# Create CHANGELOG.md template
cat > "${FEATURE_DIR}/CHANGELOG.md" << 'EOF'
# [Feature Name] - Changelog

## vX.Y.Z (2026-01-27)

### Added
- **Feature description**: [Details]
  - **Impact:** [User impact]
  - **Implementation:** [How it works]
  - **Files:** [Modified files]
  - **Commit:** [hash]

### Fixed
- **Bug description**: [Details]
  - **Impact:** [User impact]
  - **Files:** [Modified files]
  - **Commit:** [hash]

### Changed
- **Change description**: [Details]
  - **Impact:** [User impact]
  - **Files:** [Modified files]
  - **Commit:** [hash]

---

## Technical Details

```csharp
// Before:
// [old code]

// After:
// [new code]
```

---

## Performance Impact

[Performance metrics]

---

## Breaking Changes

[Any breaking changes or "None"]

---

## Related Commits

- `abc1234` - [Commit message]
EOF

echo "✅ Created documentation scaffold in ${FEATURE_DIR}"
echo ""
echo "Created files:"
ls -1 "${FEATURE_DIR}/"
echo ""
echo "Next steps:"
echo "1. Fill in the templates with actual feature details"
echo "2. Review and update placeholders"
echo "3. Run validation: scripts/docs/validate-structure.sh"
