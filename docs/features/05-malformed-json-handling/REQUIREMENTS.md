# Feature 05: Malformed JSON Handling - Requirements

## Document Info
- **Feature:** Malformed JSON Handling
- **Version:** 0.9.5.3
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This feature handles cases where Xtream providers return malformed JSON responses - specifically returning an array `[]` when an object `{}` is expected. Without this fix, the plugin would crash during JSON deserialization.

---

## Problem Statement

### Symptoms
- Plugin crashes when browsing certain series
- Error in logs: `JsonSerializationException`
- Specific series always fail, others work fine
- Error message mentions "cannot deserialize array into object"

### Root Cause
When requesting series info (`get_series_info` API call), the expected response is:
```json
{
  "info": { ... },
  "seasons": [ ... ],
  "episodes": { ... }
}
```

However, some Xtream providers return an empty array for series with no data:
```json
[]
```

The JSON deserializer expects an object but receives an array, causing a crash.

### When This Happens
- Series was recently removed from provider
- Series has no episodes yet (placeholder)
- Provider database inconsistency
- Series ID is invalid or orphaned

---

## User Stories

### US-1: Graceful Degradation
**As a** Jellyfin user
**I want** the plugin to handle broken series gracefully
**So that** one bad series doesn't break my entire library

**Acceptance Criteria:**
- Malformed series response doesn't crash the plugin
- Other series continue to work
- Error is logged for debugging
- User sees empty series instead of error

### US-2: Cache Refresh Resilience
**As a** Jellyfin administrator
**I want** cache refresh to complete even with malformed data
**So that** my library stays populated despite provider issues

**Acceptance Criteria:**
- Cache refresh continues past malformed series
- Progress isn't blocked by bad data
- Summary shows which series had issues

---

## Functional Requirements

### FR-1: Array Detection
- **FR-1.1:** Before deserializing, check if JSON starts with `[`
- **FR-1.2:** Only check for `SeriesStreamInfo` type (most common issue)
- **FR-1.3:** Handle both empty `[]` and non-empty arrays

### FR-2: Graceful Fallback
- **FR-2.1:** When array detected, return empty `SeriesStreamInfo` object
- **FR-2.2:** Do not throw exception
- **FR-2.3:** Log warning with URL for debugging

### FR-3: Error Logging
- **FR-3.1:** Log malformed response at Warning level
- **FR-3.2:** Include URL that returned malformed data
- **FR-3.3:** Include brief JSON sample for diagnosis

---

## Non-Functional Requirements

### NFR-1: Performance
- **NFR-1.1:** JSON inspection adds minimal overhead
- **NFR-1.2:** Only check first character of response
- **NFR-1.3:** No additional API calls

### NFR-2: Stability
- **NFR-2.1:** Plugin must not crash on malformed data
- **NFR-2.2:** Cache refresh must not abort
- **NFR-2.3:** Other operations must continue

---

## Test Cases

| ID | Scenario | Expected Result |
|----|----------|-----------------|
| TC-1 | Normal object response | Deserializes normally |
| TC-2 | Empty array `[]` | Returns empty SeriesStreamInfo |
| TC-3 | Non-empty array `[{...}]` | Returns empty SeriesStreamInfo |
| TC-4 | Whitespace before array ` []` | Detected correctly (trimmed) |
| TC-5 | Other types (List<Series>) | Normal deserialization |

---

## Scope

### In Scope
- `SeriesStreamInfo` responses (most commonly affected)
- Array-instead-of-object detection
- Graceful empty object return

### Out of Scope
- Other malformed JSON patterns (invalid syntax, etc.)
- Automatic retry of malformed responses
- Provider notification of issues

---

## Dependencies

- Newtonsoft.Json for deserialization
- Feature 04 (Eager Caching) - benefits from resilient API calls

---

## Related Documentation

- [Architecture](ARCHITECTURE.md) - Detection and fallback design
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history

---

## References

- `Client/XtreamClient.cs:136-154` - QueryApi method with array detection
- `Client/XtreamClient.cs:161-200` - GetEmptyObject helper
