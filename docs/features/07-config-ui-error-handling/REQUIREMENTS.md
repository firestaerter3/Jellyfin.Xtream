# Feature 07: Config UI Error Handling - Requirements

## Document Info
- **Feature:** Config UI Error Handling
- **Version:** 0.9.4.x
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This feature addresses multiple UI/UX issues in the plugin configuration interface, focusing on error handling, button states, and real-time feedback. The improvements ensure the configuration UI remains responsive and provides clear feedback during all operations.

---

## User Stories

### US-1: Clear Cache Button Recovery
**As a** Jellyfin administrator
**I want** the Clear Cache button to always recover after use
**So that** I can clear the cache multiple times without refreshing the page

**Acceptance Criteria:**
- Button shows "Clearing..." during operation
- Button re-enables after operation completes (success or failure)
- Button never remains stuck in disabled state

### US-2: Error Message Management
**As a** Jellyfin administrator
**I want** error messages to not accumulate when switching tabs
**So that** I only see relevant, current errors

**Acceptance Criteria:**
- Previous errors are cleared before showing new ones
- Revisiting a tab doesn't show stale error messages
- Table content is cleared before displaying error

### US-3: Real-time Cache Status
**As a** Jellyfin administrator
**I want** to see real-time cache refresh progress
**So that** I know the operation is working and how much longer it will take

**Acceptance Criteria:**
- Progress bar shows current completion percentage
- Status text describes current operation
- Progress updates every 2 seconds
- Visual indicators distinguish different states

### US-4: Graceful Error Handling
**As a** Jellyfin administrator
**I want** helpful error messages when operations fail
**So that** I can troubleshoot issues myself

**Acceptance Criteria:**
- All API calls have error handling
- Error messages suggest troubleshooting steps
- Console logs detail for debugging
- Operations continue despite partial failures

### US-5: Smart Button States
**As a** Jellyfin administrator
**I want** buttons to reflect their current availability
**So that** I don't trigger conflicting operations

**Acceptance Criteria:**
- Refresh button disabled during active refresh
- Clear Cache stays enabled to allow cancellation
- Button text changes during operations

---

## Functional Requirements

### FR-1: Clear Cache Button Recovery
- **FR-1.1:** Button MUST use `.finally()` block to guarantee re-enable
- **FR-1.2:** Button MUST show "Clearing..." text during operation
- **FR-1.3:** Button MUST revert to "Clear Cache" after any outcome

### FR-2: Error Message Clearing
- **FR-2.1:** Table innerHTML MUST be cleared before displaying errors
- **FR-2.2:** Loading indicator MUST be hidden on error
- **FR-2.3:** Error styling MUST use red color (#ff6b6b)

### FR-3: Cache Status Display
- **FR-3.1:** Status MUST poll `/Xtream/SeriesCacheStatus` endpoint
- **FR-3.2:** Poll interval MUST be 2 seconds
- **FR-3.3:** Progress bar MUST show percentage (0-100%)
- **FR-3.4:** Status container MUST hide when no cache activity

### FR-4: Color-Coded Status Indicators
- **FR-4.1:** Blue (#00a4dc) for active refresh
- **FR-4.2:** Green (#4caf50) for complete (100%)
- **FR-4.3:** Gray (#a0a0a0) for idle/cleared states

### FR-5: Button State Management
- **FR-5.1:** Refresh button disabled during active refresh
- **FR-5.2:** Clear Cache button always enabled (can cancel refresh)
- **FR-5.3:** Check status before showing confirmation dialog

---

## Non-Functional Requirements

### NFR-1: UI Responsiveness
- **NFR-1.1:** Button state changes MUST be immediate
- **NFR-1.2:** No blocking operations in UI thread
- **NFR-1.3:** Backend MUST NOT use Thread.Sleep()

### NFR-2: Error Message Quality
- **NFR-2.1:** Messages MUST be user-friendly (no technical jargon)
- **NFR-2.2:** Messages MUST include troubleshooting steps
- **NFR-2.3:** Console logs MUST provide detailed error info

### NFR-3: Polling Efficiency
- **NFR-3.1:** Status polling MUST stop when view is hidden
- **NFR-3.2:** Failed status polls MUST fail silently
- **NFR-3.3:** Polling MUST not impact other UI operations

---

## Dependencies

- Requires Feature 04 (Eager Caching) for cache status endpoint
- Uses Jellyfin Dashboard API for alerts
- Uses browser Fetch API for HTTP requests

---

## Related Documentation

- [Architecture](ARCHITECTURE.md) - UI patterns and data flow
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [Feature 04 - Eager Caching](../04-eager-caching/REQUIREMENTS.md) - Cache backend

---

## References

- XtreamSeries.js:79-139 - Clear Cache handler
- XtreamSeries.js:142-183 - Status polling
- XtreamSeries.js:240-256 - Error display
- XtreamController.cs:209-223 - SeriesCacheStatus endpoint
- XtreamController.cs:251-281 - SeriesCacheClear endpoint
