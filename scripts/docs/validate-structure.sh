#!/bin/bash
# Validate documentation structure
# Checks that all required documents exist for each feature

set -e

DOCS_DIR="docs/features"
REQUIRED_DOCS=("REQUIREMENTS.md" "ARCHITECTURE.md" "IMPLEMENTATION.md" "CONTEXT.md" "TEST_PLAN.md" "TODO.md" "CHANGELOG.md")
ERRORS=0

echo "Validating documentation structure..."
echo ""

# Check each feature directory
for feature_dir in "$DOCS_DIR"/*/ ; do
    if [ -d "$feature_dir" ]; then
        feature=$(basename "$feature_dir")
        echo "Checking feature: $feature"

        for doc in "${REQUIRED_DOCS[@]}"; do
            doc_path="${feature_dir}${doc}"
            if [ -f "$doc_path" ]; then
                # Check if file is not just a template (has actual content)
                if grep -q "\[Feature Name\]" "$doc_path" || grep -q "TODO:" "$doc_path" 2>/dev/null; then
                    echo "  ⚠️  $doc exists but appears to be a template"
                else
                    echo "  ✅ $doc"
                fi
            else
                echo "  ❌ $doc MISSING"
                ((ERRORS++))
            fi
        done
        echo ""
    fi
done

# Check reference docs
echo "Checking reference documentation..."
REFERENCE_DOCS=("PROJECT_CONTEXT.md" "BUILD_ERRORS_PREVENTION.md" "REPOSITORY_SETUP.md")
for doc in "${REFERENCE_DOCS[@]}"; do
    if [ -f "docs/reference/$doc" ]; then
        echo "  ✅ $doc"
    else
        echo "  ❌ $doc MISSING"
        ((ERRORS++))
    fi
done
echo ""

# Check upstream docs
echo "Checking upstream documentation..."
UPSTREAM_DOCS=("PR_PROPOSAL.md")
for doc in "${UPSTREAM_DOCS[@]}"; do
    if [ -f "docs/upstream/$doc" ]; then
        echo "  ✅ $doc"
    else
        echo "  ❌ $doc MISSING"
        ((ERRORS++))
    fi
done
echo ""

# Check for INDEX.md
echo "Checking master index..."
if [ -f "docs/INDEX.md" ]; then
    echo "  ✅ INDEX.md"
else
    echo "  ❌ INDEX.md MISSING"
    ((ERRORS++))
fi
echo ""

# Summary
if [ $ERRORS -eq 0 ]; then
    echo "✅ Documentation structure validation passed!"
    exit 0
else
    echo "❌ Documentation structure validation failed with $ERRORS errors"
    exit 1
fi
