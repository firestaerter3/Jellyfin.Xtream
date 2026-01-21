#!/bin/bash
#===============================================================================
# Jellyfin Xtream Plugin Fork Setup Script
# Purpose: Automate forking and setting up dev environment for flat series view
# Version: 2.0
#===============================================================================

set -e

# Configuration
UPSTREAM_URL="https://github.com/Kevinjil/Jellyfin.Xtream.git"
WORK_DIR="$HOME/jellyfin-xtream-fork"
FEATURE_BRANCH="feature/flat-series-view"
DOTNET_VERSION="8.0"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_header() {
    echo ""
    echo -e "${BLUE}============================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}============================================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

#===============================================================================
# Pre-flight Checks
#===============================================================================

print_header "Jellyfin Xtream Plugin - Flat View Fork Setup"

echo "This script will help you set up a development environment for"
echo "implementing the flat series view feature in the Xtream plugin."
echo ""

# Check git
if ! command -v git &> /dev/null; then
    print_error "git is not installed. Please install git first."
    exit 1
fi
print_success "git is installed"

# Check .NET SDK
if command -v dotnet &> /dev/null; then
    INSTALLED_DOTNET=$(dotnet --version 2>/dev/null | cut -d'.' -f1)
    if [ "$INSTALLED_DOTNET" -ge 8 ] 2>/dev/null; then
        print_success ".NET SDK $INSTALLED_DOTNET is installed"
    else
        print_warning ".NET SDK version $INSTALLED_DOTNET found (8.0+ recommended)"
    fi
else
    print_warning ".NET SDK not found. You'll need it to build the plugin."
    echo "        Install from: https://dotnet.microsoft.com/download"
    echo ""
    read -p "Continue without .NET SDK? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

#===============================================================================
# Step 1: GitHub Fork Instructions
#===============================================================================

print_header "Step 1: Fork the Repository"

echo "Before cloning, you need to fork the repository on GitHub:"
echo ""
echo "  1. Go to: https://github.com/Kevinjil/Jellyfin.Xtream"
echo "  2. Click the 'Fork' button (top right)"
echo "  3. Create the fork under your account"
echo ""
read -p "Have you forked the repository? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo ""
    echo "Please fork the repository first, then run this script again."
    exit 0
fi

#===============================================================================
# Step 2: Clone Repository
#===============================================================================

print_header "Step 2: Clone Repository"

read -p "Enter your GitHub username: " GITHUB_USER

if [ -z "$GITHUB_USER" ]; then
    print_error "GitHub username is required"
    exit 1
fi

FORK_URL="https://github.com/${GITHUB_USER}/Jellyfin.Xtream.git"

# Create work directory
mkdir -p "$WORK_DIR"
cd "$WORK_DIR"

if [ -d "Jellyfin.Xtream" ]; then
    print_warning "Repository already exists at $WORK_DIR/Jellyfin.Xtream"
    read -p "Delete and re-clone? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        rm -rf "Jellyfin.Xtream"
    else
        cd Jellyfin.Xtream
        print_info "Using existing repository"
    fi
fi

if [ ! -d "Jellyfin.Xtream" ]; then
    echo "Cloning from: $FORK_URL"
    if git clone "$FORK_URL" Jellyfin.Xtream; then
        print_success "Repository cloned successfully"
        cd Jellyfin.Xtream
    else
        print_error "Failed to clone. Checking if it's the username..."
        echo "Trying to clone upstream instead..."
        git clone "$UPSTREAM_URL" Jellyfin.Xtream
        cd Jellyfin.Xtream
        print_warning "Cloned upstream. Remember to push to your fork later."
    fi
fi

# Add upstream remote
if ! git remote | grep -q "upstream"; then
    git remote add upstream "$UPSTREAM_URL"
    print_success "Added upstream remote"
fi

# Fetch latest
git fetch origin
git fetch upstream

#===============================================================================
# Step 3: Create Feature Branch
#===============================================================================

print_header "Step 3: Create Feature Branch"

# Check if branch exists
if git show-ref --verify --quiet refs/heads/"$FEATURE_BRANCH"; then
    print_info "Branch '$FEATURE_BRANCH' already exists"
    git checkout "$FEATURE_BRANCH"
else
    # Create from latest upstream master/main
    DEFAULT_BRANCH=$(git remote show upstream | grep 'HEAD branch' | cut -d' ' -f5)
    git checkout -b "$FEATURE_BRANCH" "upstream/$DEFAULT_BRANCH"
    print_success "Created branch '$FEATURE_BRANCH' from upstream/$DEFAULT_BRANCH"
fi

#===============================================================================
# Step 4: Analyze Codebase
#===============================================================================

print_header "Step 4: Codebase Analysis"

echo "Scanning codebase for key files..."
echo ""

echo -e "${YELLOW}=== Configuration Files ===${NC}"
find . -name "*Configuration*.cs" -type f 2>/dev/null | head -5 || echo "  (none found)"
echo ""

echo -e "${YELLOW}=== Channel/Provider Files (where items are created) ===${NC}"
grep -rl "IChannel\|GetChildren" --include="*.cs" . 2>/dev/null | head -10 || echo "  (none found)"
echo ""

echo -e "${YELLOW}=== Series-related Files ===${NC}"
find . -name "*.cs" -type f | xargs grep -l -i "series" 2>/dev/null | head -10 || echo "  (none found)"
echo ""

echo -e "${YELLOW}=== Category-related Code ===${NC}"
grep -r "category" --include="*.cs" -l -i . 2>/dev/null | head -10 || echo "  (none found)"
echo ""

echo -e "${YELLOW}=== UI Configuration Files ===${NC}"
find . -name "*.html" -type f 2>/dev/null | head -5 || echo "  (none found)"
echo ""

echo -e "${YELLOW}=== Project Structure ===${NC}"
find . -name "*.csproj" -type f 2>/dev/null
echo ""

#===============================================================================
# Step 5: Build Test
#===============================================================================

print_header "Step 5: Build Verification"

if command -v dotnet &> /dev/null; then
    echo "Attempting to restore and build..."
    if dotnet restore 2>/dev/null; then
        print_success "Dependencies restored"
    else
        print_warning "Restore failed (may need manual intervention)"
    fi
    
    if dotnet build -c Debug 2>/dev/null; then
        print_success "Build successful"
    else
        print_warning "Build failed (check dependencies and .NET version)"
    fi
else
    print_warning "Skipping build (no .NET SDK)"
fi

#===============================================================================
# Step 6: Create Implementation Stubs
#===============================================================================

print_header "Step 6: Create Implementation Notes"

# Create a local notes file
cat > FLAT_VIEW_IMPLEMENTATION_NOTES.md << 'EOF'
# Flat Series View - Implementation Notes

## Files to Modify

Based on codebase analysis, modify these files:

### 1. Configuration
Look for `PluginConfiguration.cs` and add:
```csharp
public bool FlattenSeriesView { get; set; } = false;
```

### 2. Channel/Provider Logic
Find the file with `GetChildren` method and add conditional logic:
```csharp
if (Configuration.FlattenSeriesView)
{
    // Return all series directly
}
else
{
    // Original behavior: return category folders
}
```

### 3. UI Configuration
Find HTML configuration page and add checkbox:
```html
<input type="checkbox" id="flattenSeriesView" />
```

## Quick Search Commands

```bash
# Find where items are created
grep -r "GetChildren" --include="*.cs" .

# Find category handling
grep -rn "category" --include="*.cs" -i . | head -20

# Find folder creation
grep -r "Folder\|FolderType" --include="*.cs" .

# Find series item creation
grep -r "ChannelItemInfo\|ChannelItemType" --include="*.cs" .
```

## Testing

1. Build: `dotnet build -c Release`
2. Copy DLL to Jellyfin plugins folder
3. Restart Jellyfin
4. Enable flat view in settings
5. Verify series appear directly
EOF

print_success "Created FLAT_VIEW_IMPLEMENTATION_NOTES.md"

#===============================================================================
# Summary
#===============================================================================

print_header "Setup Complete!"

echo -e "${GREEN}Repository:${NC} $WORK_DIR/Jellyfin.Xtream"
echo -e "${GREEN}Branch:${NC} $FEATURE_BRANCH"
echo -e "${GREEN}Fork URL:${NC} $FORK_URL"
echo ""
echo "Next steps:"
echo ""
echo "  1. Review the implementation plan:"
echo "     docs/feature-requests/JELLYFIN_XTREAM_FLAT_VIEW_IMPLEMENTATION.md"
echo ""
echo "  2. Open the project in your IDE:"
echo "     cd $WORK_DIR/Jellyfin.Xtream"
echo "     code .  # or open in Visual Studio"
echo ""
echo "  3. Find and modify these key files:"
echo "     - PluginConfiguration.cs (add FlattenSeriesView property)"
echo "     - *Channel*.cs (modify GetChildren method)"
echo "     - *.html (add checkbox to UI)"
echo ""
echo "  4. Build and test:"
echo "     dotnet build -c Release"
echo "     # Copy DLL to Jellyfin plugins folder"
echo "     # Restart Jellyfin and test"
echo ""
echo "  5. When ready, push and create PR:"
echo "     git add ."
echo "     git commit -m 'Add flat series view feature'"
echo "     git push origin $FEATURE_BRANCH"
echo ""
echo "Good luck with the implementation! 🚀"
