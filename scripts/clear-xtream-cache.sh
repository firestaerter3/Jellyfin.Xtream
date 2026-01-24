#!/bin/bash
# Clear all Xtream plugin cache and data
# Usage: ./scripts/clear-xtream-cache.sh [JELLYFIN_URL] [API_KEY]
#
# Environment variables:
#   JELLYFIN_URL: Jellyfin server URL (default: http://localhost:8096)
#   API_KEY: Jellyfin API key (required)

JELLYFIN_URL="${1:-${JELLYFIN_URL:-http://localhost:8096}}"
API_KEY="${2:-${API_KEY}}"

if [ -z "$API_KEY" ]; then
    echo "❌ ERROR: API_KEY is required"
    echo ""
    echo "Usage: $0 [JELLYFIN_URL] [API_KEY]"
    echo "   Or set environment variables:"
    echo "   export JELLYFIN_URL=http://localhost:8096"
    echo "   export API_KEY=your_api_key_here"
    echo ""
    echo "To get your API key:"
    echo "  1. Go to Jellyfin Dashboard → API Keys"
    echo "  2. Create a new API key"
    echo "  3. Use it in this script"
    exit 1
fi

echo "🗑️  Clearing Xtream plugin cache..."
echo "   Jellyfin URL: $JELLYFIN_URL"
echo ""

# Clear series cache via API
RESPONSE=$(curl -s -X POST \
    -H "X-Emby-Authorization: MediaBrowser Client=\"Script\", Device=\"Cache Clear\", DeviceId=\"cache-clear-script\", Version=\"1.0.0\"" \
    -H "X-Emby-Token: $API_KEY" \
    "$JELLYFIN_URL/Xtream/ClearSeriesCache" 2>&1)

if echo "$RESPONSE" | grep -q "successfully\|Message"; then
    echo "✅ Cache cleared successfully!"
    echo ""
    echo "$RESPONSE" | grep -o '"Message":"[^"]*"' | sed 's/"Message":"\(.*\)"/\1/'
else
    echo "❌ Error clearing cache:"
    echo "$RESPONSE"
    exit 1
fi

echo ""
echo "📋 Next steps:"
echo "  1. The cache has been cleared"
echo "  2. Series data will be refetched on next access"
echo "  3. Or trigger a manual refresh via: Dashboard → Scheduled Tasks → Refresh Xtream Series Cache"
