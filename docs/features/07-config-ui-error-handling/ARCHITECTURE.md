# Feature 07: Config UI Error Handling - Architecture

## Document Info
- **Feature:** Config UI Error Handling
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document describes the UI patterns, data flow, and architectural decisions for the configuration UI error handling improvements.

---

## Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   Configuration UI                          │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐ │
│  │   XtreamSeries  │  │   XtreamVod     │  │ XtreamLive  │ │
│  │   .js/.html     │  │   .js/.html     │  │ .js/.html   │ │
│  └────────┬────────┘  └────────┬────────┘  └──────┬──────┘ │
│           │                    │                   │        │
│           └────────────────────┼───────────────────┘        │
│                                │                            │
│                    ┌───────────▼───────────┐                │
│                    │     Xtream.js         │                │
│                    │  (shared utilities)   │                │
│                    └───────────┬───────────┘                │
└────────────────────────────────┼────────────────────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │   XtreamController.cs   │
                    │                         │
                    │  GET  /SeriesCacheStatus│
                    │  POST /SeriesCacheRefresh│
                    │  POST /SeriesCacheClear │
                    └─────────────────────────┘
```

---

## UI Patterns

### 1. Promise Finally Pattern

All async operations use `.finally()` to guarantee cleanup:

```javascript
button.disabled = true;
button.textContent = 'Processing...';

fetch(url)
  .then(response => { /* handle success */ })
  .catch(err => { /* handle error */ })
  .finally(() => {
    button.disabled = false;        // Always runs
    button.textContent = 'Original'; // Always runs
  });
```

**Why this matters:**
- Without `.finally()`, network errors or exceptions could leave button stuck
- Previous implementation relied on success-path cleanup only
- `.finally()` runs regardless of promise resolution or rejection

### 2. Status Polling Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Status Polling Flow                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  viewshow ──► Start Interval ──► updateCacheStatus()        │
│                    │                    │                   │
│                    │                    ▼                   │
│                    │           GET /SeriesCacheStatus       │
│                    │                    │                   │
│                    │                    ▼                   │
│                    │         ┌─────────────────────┐        │
│                    │         │  Update UI Elements │        │
│                    │         │  - Progress bar     │        │
│                    │         │  - Status text      │        │
│                    │         │  - Button states    │        │
│                    │         │  - Color coding     │        │
│                    │         └─────────────────────┘        │
│                    │                    │                   │
│                    └──── 2 seconds ─────┘                   │
│                                                             │
│  viewhide ──► clearInterval()                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Lifecycle Management:**
- Polling starts when Series tab is shown (`viewshow` event)
- Polling stops when user navigates away (`viewhide` event)
- Prevents resource leaks and unnecessary network requests

### 3. Error Display Pattern

```javascript
// Anti-pattern (old): Append to table, causing stacking
table.appendChild(errorRow);

// Correct pattern: Clear first, then add
table.innerHTML = '';           // Clear previous content/errors
table.appendChild(errorRow);    // Add new error
```

**Error Message Structure:**
```
┌────────────────────────────────────────────────────────────┐
│  Failed to load categories. Please check:                  │
│  1. Xtream credentials are configured (Credentials tab)    │
│  2. Xtream server is accessible                            │
│  3. Browser console for detailed errors                    │
└────────────────────────────────────────────────────────────┘
```

---

## Data Flow

### Clear Cache Operation

```
User clicks "Clear Cache"
         │
         ▼
┌─────────────────────────┐
│ 1. Check current status │ GET /SeriesCacheStatus
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ 2. Show confirmation    │ Different message if refresh running
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ 3. Disable button       │ Show "Clearing..."
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ 4. POST to clear cache  │ POST /SeriesCacheClear
└───────────┬─────────────┘
            │
     ┌──────┴──────┐
     │             │
  Success       Error
     │             │
     ▼             ▼
┌─────────┐   ┌───────────┐
│ Alert   │   │ Alert     │
│ success │   │ error msg │
└────┬────┘   └─────┬─────┘
     │              │
     └──────┬───────┘
            │
            ▼
┌─────────────────────────┐
│ 5. Re-enable button     │ .finally() - ALWAYS runs
│    Reset text           │
└─────────────────────────┘
```

### Status Response Structure

```json
{
  "IsRefreshing": true,
  "Progress": 0.45,
  "Status": "Processing series 234 of 520",
  "StartTime": "2026-01-27T10:00:00Z",
  "CompleteTime": null,
  "IsCachePopulated": true
}
```

---

## Color Coding System

| State | Color | Hex Code | Usage |
|-------|-------|----------|-------|
| Refreshing | Blue | #00a4dc | Active operation in progress |
| Complete | Green | #4caf50 | 100% progress reached |
| Idle/Cleared | Gray | #a0a0a0 | No activity, cache cleared |
| Error | Red | #ff6b6b | Error message text |

---

## Button State Logic

```javascript
// Refresh button: Disabled during active refresh
if (status.IsRefreshing) {
  refreshCacheBtn.disabled = true;
} else {
  refreshCacheBtn.disabled = false;
}

// Clear Cache button: Always enabled (can cancel refresh)
// No state changes needed - stays enabled
```

**Design Decision:** Clear Cache remains enabled during refresh to allow users to cancel a long-running operation.

---

## Backend Considerations

### No Blocking Operations

The backend was modified to avoid blocking:

```csharp
// Before (problematic)
Thread.Sleep(2000);  // Blocked HTTP response

// After (non-blocking)
// Response returns immediately
// Cache clear happens synchronously but quickly
// Jellyfin channel refresh is queued, not awaited
```

---

## Error Recovery Matrix

| Scenario | UI Behavior | User Action |
|----------|-------------|-------------|
| Network error on clear | Alert shown, button re-enabled | Retry or check network |
| Network error on status poll | Fails silently, retries in 2s | None needed |
| Server returns error | Alert with message | Check server logs |
| Status check before clear fails | Falls back to default confirm | Proceed or retry |

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Functional requirements
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [Feature 04 Architecture](../04-eager-caching/ARCHITECTURE.md) - Cache backend
