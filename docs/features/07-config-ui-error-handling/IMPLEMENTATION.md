# Feature 07: Config UI Error Handling - Implementation

## Document Info
- **Feature:** Config UI Error Handling
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document details the code changes implementing the configuration UI error handling improvements.

---

## Files Modified

| File | Changes | Purpose |
|------|---------|---------|
| `Configuration/Web/XtreamSeries.js` | ~100 lines | Cache buttons, polling, error handling |
| `Configuration/Web/XtreamSeries.html` | ~35 lines | Cache UI elements (progress bar, buttons) |
| `Configuration/Web/XtreamVod.js` | ~20 lines | Error handling pattern |
| `Configuration/Web/XtreamLive.js` | ~20 lines | Error handling pattern |
| `Api/XtreamController.cs` | ~30 lines | Status endpoint, removed Thread.Sleep |

---

## Detailed Code Changes

### 1. XtreamSeries.js - Clear Cache Handler (Lines 79-139)

**The `.finally()` Pattern:**

```javascript
clearCacheBtn.addEventListener('click', () => {
  // Pre-check status to show appropriate confirmation
  Xtream.fetchJson('Xtream/SeriesCacheStatus')
    .then((status) => {
      let confirmMessage = 'Are you sure you want to clear the cache?';
      if (status.IsRefreshing) {
        confirmMessage = 'A cache refresh is currently in progress. ' +
          'Clearing the cache will stop the refresh. Continue?';
      }

      if (!confirm(confirmMessage)) return;
      clearCache();
    })
    .catch((err) => {
      console.error('Failed to check cache status:', err);
      // Fallback: still allow clearing with default message
      if (confirm('Are you sure you want to clear the cache?')) {
        clearCache();
      }
    });
});

function clearCache() {
  console.log('clearCache() called');
  clearCacheBtn.disabled = true;
  clearCacheBtn.querySelector('span').textContent = 'Clearing...';

  fetch(ApiClient.getUrl('Xtream/SeriesCacheClear'), {
    method: 'POST',
    headers: ApiClient.defaultRequestHeaders()
  })
    .then(response => {
      console.log('Clear cache response status:', response.status);
      if (!response.ok) {
        throw new Error('Server returned ' + response.status);
      }
      return response.json();
    })
    .then(result => {
      console.log('Clear cache result:', result);
      if (result.Success) {
        Dashboard.alert(result.Message || 'Cache cleared successfully');
        cacheStatusText.textContent = 'Cache cleared';
        cacheStatusText.style.color = '#a0a0a0';
        cacheProgressFill.style.width = '0%';
      } else {
        Dashboard.alert(result.Message || 'Failed to clear cache');
      }
    })
    .catch(err => {
      console.error('Failed to clear cache:', err);
      Dashboard.alert('Failed to clear cache: ' + err.message);
    })
    .finally(() => {
      // CRITICAL: This always runs, preventing stuck button
      console.log('clearCache() finally block - re-enabling button');
      clearCacheBtn.disabled = false;
      clearCacheBtn.querySelector('span').textContent = 'Clear Cache';
    });
}
```

**Key Points:**
- Status check before confirmation allows context-aware message
- If status check fails, falls back to default behavior
- `.finally()` guarantees button recovery regardless of outcome
- Console logs aid debugging in production

### 2. XtreamSeries.js - Status Polling (Lines 142-183)

```javascript
let statusPollInterval;

function updateCacheStatus() {
  Xtream.fetchJson('Xtream/SeriesCacheStatus')
    .then((status) => {
      if (status.IsRefreshing || status.Progress > 0 || status.IsCachePopulated) {
        cacheStatusContainer.style.display = 'block';
        const progressPercent = Math.round(status.Progress * 100);
        cacheProgressFill.style.width = progressPercent + '%';
        cacheStatusText.textContent = status.Status || 'Idle';

        if (status.IsRefreshing) {
          cacheStatusText.style.color = '#00a4dc';  // Blue
          refreshCacheBtn.disabled = true;
        } else {
          refreshCacheBtn.disabled = false;
          if (status.Progress >= 1.0) {
            cacheStatusText.style.color = '#4caf50';  // Green
          } else {
            cacheStatusText.style.color = '#a0a0a0';  // Gray
          }
        }
      } else {
        cacheStatusContainer.style.display = 'none';
        refreshCacheBtn.disabled = false;
      }
    })
    .catch(() => {
      // Silently fail - API may not be available
    });
}

// Start polling when view is shown
updateCacheStatus();
statusPollInterval = setInterval(updateCacheStatus, 2000);

// Clean up interval when view is hidden
view.addEventListener("viewhide", () => {
  if (statusPollInterval) {
    clearInterval(statusPollInterval);
  }
});
```

**Key Points:**
- Polls every 2 seconds for responsive updates
- Cleans up interval on view hide to prevent memory leaks
- Silent failure on poll errors prevents UI disruption
- Progress bar has CSS transition for smooth animation

### 3. XtreamSeries.js - Error Display (Lines 240-256)

```javascript
.catch((error) => {
  console.error('Failed to load series categories:', error);
  Dashboard.hideLoadingMsg();

  // CRITICAL: Clear previous content to prevent stacking
  table.innerHTML = '';

  const errorRow = document.createElement('tr');
  const errorCell = document.createElement('td');
  errorCell.colSpan = 3;
  errorCell.style.color = '#ff6b6b';
  errorCell.style.padding = '16px';
  errorCell.innerHTML = 'Failed to load categories. Please check:<br>' +
    '1. Xtream credentials are configured (Credentials tab)<br>' +
    '2. Xtream server is accessible<br>' +
    '3. Browser console for detailed errors';
  errorRow.appendChild(errorCell);
  table.appendChild(errorRow);
});
```

**Key Points:**
- `table.innerHTML = ''` clears any previous content/errors
- Error message includes actionable troubleshooting steps
- Console error logged for detailed debugging
- Loading indicator hidden to restore UI state

### 4. XtreamVod.js and XtreamLive.js - Error Handling

Both files follow the same pattern as XtreamSeries.js (lines 43-59 and 35-51 respectively):

```javascript
.catch((error) => {
  console.error('Failed to load [VOD/Live TV] categories:', error);
  Dashboard.hideLoadingMsg();
  table.innerHTML = '';  // Clear previous content
  // ... create error row with troubleshooting steps
});
```

### 5. XtreamController.cs - SeriesCacheClear (Lines 251-281)

```csharp
[HttpPost("SeriesCacheClear")]
public ActionResult<object> ClearSeriesCache()
{
    var (isRefreshing, _, _, _, _) = Plugin.Instance.SeriesCacheService.GetStatus();

    string message = "Cache cleared successfully.";
    if (isRefreshing)
    {
        // Cancel the running refresh before clearing
        Plugin.Instance.SeriesCacheService.CancelRefresh();
        message = "Cache cleared. Refresh was cancelled.";
    }

    Plugin.Instance.SeriesCacheService.InvalidateCache();

    // Trigger Jellyfin cleanup (non-blocking)
    try
    {
        Plugin.Instance.TaskService.CancelIfRunningAndQueue(
            "Jellyfin.LiveTv",
            "Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask");
        message += " Jellyfin channel refresh triggered to clean up jellyfin.db.";
    }
    catch
    {
        message += " Warning: Could not trigger Jellyfin cleanup.";
    }

    return Ok(new { Success = true, Message = message });
}
```

**Key Points:**
- No `Thread.Sleep()` - returns immediately
- Cancels any running refresh before clearing
- Jellyfin cleanup triggered but not awaited
- Informative message returned to UI

### 6. XtreamSeries.html - UI Elements (Lines 41-59)

```html
<div class="inputContainer" style="display: flex; gap: 10px; margin-bottom: 1em;">
  <button is="emby-button" type="button" id="RefreshCacheBtn" class="raised emby-button">
    <span>Refresh Now</span>
  </button>
  <button is="emby-button" type="button" id="ClearCacheBtn" class="raised emby-button"
    style="background-color: #c25;"
    title="Clears all cached data. Next refresh will fetch everything from scratch.">
    <span>Clear Cache</span>
  </button>
</div>

<div class="inputContainer" id="CacheStatusContainer" style="display: none;">
  <label class="inputLabel">Cache Refresh Status</label>
  <div id="CacheProgressBar" style="width: 100%; height: 20px;
    background-color: #2a2a2a; border-radius: 4px; overflow: hidden; margin: 8px 0;">
    <div id="CacheProgressFill" style="height: 100%; background-color: #00a4dc;
      width: 0%; transition: width 0.3s ease;"></div>
  </div>
  <div id="CacheStatusText" style="color: #a0a0a0; font-size: 0.9em; margin-top: 4px;">
    Idle
  </div>
</div>
```

**Key Points:**
- Buttons wrapped in `<span>` for text updates via `.textContent`
- Clear Cache button has distinctive red styling (#c25)
- Progress bar uses CSS transition for smooth animation
- Status container hidden by default, shown when relevant

---

## Bug Fixes Summary

### 1. Stuck Clear Cache Button

**Problem:** Button showed "Clearing..." indefinitely if request failed.

**Root Causes:**
1. Backend used `Thread.Sleep()` which blocked response
2. Frontend only re-enabled button in success path

**Solution:**
1. Removed `Thread.Sleep()` from backend
2. Added `.finally()` block that always runs

### 2. Stacking Error Messages

**Problem:** Switching tabs and returning showed multiple error messages.

**Root Cause:** Error handler appended to table without clearing first.

**Solution:** Clear `table.innerHTML` before displaying new error.

### 3. Multiple Confirmation Dialogs

**Problem:** Clicking Clear Cache multiple times spawned multiple dialogs.

**Root Cause:** No check if operation was already in progress.

**Solution:** Check button disabled state and cache status before proceeding.

---

## Edge Cases Handled

| Scenario | Handling |
|----------|----------|
| Status API unavailable | Fall back to default confirmation message |
| Network error during clear | Alert user, re-enable button via .finally() |
| Clear during active refresh | Cancel refresh, then clear, inform user |
| Tab switch during polling | Stop polling to prevent memory leaks |
| Server returns non-200 | Throw error, caught by .catch() |

---

## Console Logging

Debug logs added to aid production troubleshooting:

```javascript
console.log('clearCache() called');
console.log('Clear cache response status:', response.status);
console.log('Clear cache result:', result);
console.log('clearCache() finally block - re-enabling button');
console.error('Failed to clear cache:', err);
```

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Functional requirements
- [Architecture](ARCHITECTURE.md) - UI patterns and data flow
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history
