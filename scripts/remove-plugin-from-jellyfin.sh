#!/bin/bash
# Script to completely remove Jellyfin.Xtream plugin from Jellyfin Docker container
# Usage: ./scripts/remove-plugin-from-jellyfin.sh [container_name] [jellyfin_host] [-y|--yes]
#
# Environment variables:
#   JELLYFIN_HOST: Jellyfin server host/IP (default: localhost)
#   JELLYFIN_CONTAINER: Docker container name (default: jellyfin)
#   SSH_PASSWORD: SSH password for root@$JELLYFIN_HOST (required)
#
# Example:
#   JELLYFIN_HOST=your-host ./scripts/remove-plugin-from-jellyfin.sh -y
#   ./scripts/remove-plugin-from-jellyfin.sh jellyfin your-host -y

set -e

# Parse arguments
SKIP_CONFIRM=false
CONTAINER_NAME="${JELLYFIN_CONTAINER:-jellyfin}"
JELLYFIN_HOST="${JELLYFIN_HOST:-localhost}"

for arg in "$@"; do
    case $arg in
        -y|--yes)
            SKIP_CONFIRM=true
            ;;
        *)
            # If it's not a flag and starts with a letter/number, it's a container name or host
            if [[ "$arg" =~ ^[a-zA-Z0-9] ]] && [ "$CONTAINER_NAME" = "jellyfin" ] && [ "$JELLYFIN_HOST" = "localhost" ]; then
                CONTAINER_NAME="$arg"
            elif [[ "$arg" =~ ^[0-9] ]] || [[ "$arg" =~ ^[a-zA-Z0-9]+\.[a-zA-Z0-9] ]] || [[ "$arg" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
                JELLYFIN_HOST="$arg"
            fi
            ;;
    esac
done
PLUGIN_GUID="5d774c35-8567-46d3-a950-9bb8227a0c5d"
PLUGIN_NAME="Jellyfin.Xtream"

echo "🧹 Removing $PLUGIN_NAME plugin from Jellyfin..."
echo "Container: $CONTAINER_NAME"
echo "Host: $JELLYFIN_HOST"
echo ""

# Function to execute commands in Docker container with timeout
docker_exec() {
    if [ -z "$SSH_PASSWORD" ]; then
        echo "❌ ERROR: SSH_PASSWORD environment variable is required"
        echo "   Set it with: export SSH_PASSWORD=your_password"
        exit 1
    fi
    sshpass -p "$SSH_PASSWORD" ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 root@$JELLYFIN_HOST "docker exec $CONTAINER_NAME $@"
}

# Find Jellyfin data directory
echo "📂 Finding Jellyfin data directory..."
DATA_DIR=$(docker_exec sh -c 'echo ${JF_CONFIG_DIR:-/config}' 2>/dev/null || echo "/config")
if [ -z "$DATA_DIR" ]; then
    DATA_DIR="/config"
fi
echo "Data directory: $DATA_DIR"
echo ""

# Plugin paths (Jellyfin stores plugins in /config/data/plugins/)
PLUGINS_DIR="$DATA_DIR/data/plugins"
CONFIG_DIR="$DATA_DIR/data/plugins/configurations"
CACHE_DIR="$DATA_DIR/cache"

echo "🔍 Searching for plugin files..."
echo ""

# Find and list plugin directories (Jellyfin stores plugins in versioned directories)
echo "Looking for plugin directories..."
if [ -z "$SSH_PASSWORD" ]; then
    echo "❌ ERROR: SSH_PASSWORD environment variable is required"
    exit 1
fi
PLUGIN_DIRS=$(sshpass -p "$SSH_PASSWORD" ssh -o StrictHostKeyChecking=no root@$JELLYFIN_HOST "docker exec $CONTAINER_NAME ls -1 '$PLUGINS_DIR' 2>/dev/null | grep -i xtream" 2>/dev/null || echo "")
if [ -n "$PLUGIN_DIRS" ]; then
    echo "  Found plugin directories:"
    echo "$PLUGIN_DIRS" | while read dirname; do
        if [ -n "$dirname" ]; then
            fullpath="$PLUGINS_DIR/$dirname"
            echo "    - $fullpath"
            DLL_FILE=$(docker_exec sh -c "find '$fullpath' -maxdepth 1 -name '*.dll' -type f 2>/dev/null | head -1" 2>/dev/null || echo "")
            if [ -n "$DLL_FILE" ] && [ "$DLL_FILE" != "." ]; then
                echo "      DLL: $DLL_FILE"
            fi
        fi
    done
else
    echo "  No plugin directories found"
fi

# Find plugin configuration
echo "Looking for plugin configuration..."
PLUGIN_CONFIG=$(docker_exec sh -c "find '$CONFIG_DIR' -maxdepth 2 -type f \\( -name '*$PLUGIN_GUID*' -o -iname '*xtream*' \\) 2>/dev/null | head -1" 2>/dev/null || echo "")
if [ -n "$PLUGIN_CONFIG" ] && [ "$PLUGIN_CONFIG" != "." ]; then
    echo "  Found: $PLUGIN_CONFIG"
else
    echo "  No plugin config found"
    PLUGIN_CONFIG=""
fi

# Find plugin cache
echo "Looking for plugin cache..."
PLUGIN_CACHE=$(docker_exec sh -c "find '$CACHE_DIR' -maxdepth 2 -type d -name '*$PLUGIN_GUID*' -o -type d -iname '*xtream*' 2>/dev/null | head -1" 2>/dev/null || echo "")
if [ -n "$PLUGIN_CACHE" ] && [ "$PLUGIN_CACHE" != "." ]; then
    echo "  Found: $PLUGIN_CACHE"
else
    echo "  No plugin cache found"
    PLUGIN_CACHE=""
fi

echo ""
if [ "$SKIP_CONFIRM" = false ]; then
    read -p "⚠️  This will remove the plugin, its configuration, and cache. Continue? (y/N) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "❌ Cancelled"
        exit 1
    fi
else
    echo "⚠️  Removing plugin, configuration, and cache (--yes flag used)..."
fi

echo ""
echo "🗑️  Removing plugin files..."

# Remove all Xtream-related plugin directories (Jellyfin uses versioned folders)
echo "  Removing all Xtream plugin directories..."
if [ -n "$PLUGIN_DIRS" ]; then
    echo "$PLUGIN_DIRS" | while read dirname; do
        if [ -n "$dirname" ]; then
            fullpath="$PLUGINS_DIR/$dirname"
            echo "    Removing: $fullpath"
            docker_exec rm -rf "$fullpath" 2>/dev/null || echo "      ⚠️  Could not remove directory"
        fi
    done
fi
# Also try find as fallback
docker_exec sh -c "find '$PLUGINS_DIR' -maxdepth 1 -type d -iname '*xtream*' -exec rm -rf {} + 2>/dev/null || true" || true
docker_exec sh -c "find '$PLUGINS_DIR' -maxdepth 2 -name '*Xtream*.dll' -delete 2>/dev/null || true" || true

# Remove plugin configuration
if [ -n "$PLUGIN_CONFIG" ]; then
    echo "  Removing: $PLUGIN_CONFIG"
    docker_exec rm -f "$PLUGIN_CONFIG" || echo "    ⚠️  Could not remove config"
fi

# Remove all Xtream config files
echo "  Removing all Xtream config files..."
docker_exec find "$CONFIG_DIR" -name "*$PLUGIN_GUID*" -delete 2>/dev/null || true
docker_exec find "$CONFIG_DIR" -iname "*xtream*" -delete 2>/dev/null || true
docker_exec find "$CONFIG_DIR" -name "Jellyfin.Xtream.xml" -delete 2>/dev/null || true

# Remove plugin cache
if [ -n "$PLUGIN_CACHE" ]; then
    echo "  Removing: $PLUGIN_CACHE"
    docker_exec rm -rf "$PLUGIN_CACHE" || echo "    ⚠️  Could not remove cache"
fi

# Remove all Xtream cache files
echo "  Removing all Xtream cache files..."
docker_exec find "$CACHE_DIR" -name "*$PLUGIN_GUID*" -delete 2>/dev/null || true
docker_exec find "$CACHE_DIR" -iname "*xtream*" -delete 2>/dev/null || true

# Also check plugins/plugins directory (some Jellyfin versions use this)
echo "  Checking plugins/plugins directory..."
docker_exec find "$PLUGINS_DIR/plugins" -name "*Xtream*" -delete 2>/dev/null || true

# Remove from plugin manifest if it exists
echo "  Removing from plugin manifest..."
docker_exec sh -c "sed -i '/$PLUGIN_GUID/d' $CONFIG_DIR/plugins/plugins.json 2>/dev/null || true" || true

echo ""
echo "🔄 Restarting Jellyfin container..."
if [ -z "$SSH_PASSWORD" ]; then
    echo "    ⚠️  SSH_PASSWORD not set, skipping restart (restart manually if needed)"
else
    sshpass -p "$SSH_PASSWORD" ssh -o StrictHostKeyChecking=no root@$JELLYFIN_HOST "docker restart $CONTAINER_NAME" || echo "    ⚠️  Could not restart container (you may need to restart manually)"
fi

echo ""
echo "✅ Plugin removal complete!"
echo ""
echo "📝 Next steps:"
echo "  1. Wait for Jellyfin to restart (check: docker logs $CONTAINER_NAME)"
echo "  2. Go to Jellyfin web UI → Plugins"
echo "  3. Verify the plugin is no longer listed"
echo "  4. If you see it, uninstall it from the UI first, then run this script again"
echo ""
