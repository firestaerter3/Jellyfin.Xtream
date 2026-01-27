# Feature 05: Malformed JSON Handling - Architecture

## Document Info
- **Feature:** Malformed JSON Handling
- **Version:** 0.9.5.3
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document describes the architectural approach to handling malformed JSON responses from Xtream providers.

---

## Problem Analysis

### Expected vs Actual Response

**Expected (SeriesStreamInfo):**
```json
{
  "info": {
    "name": "Series Name",
    "category_id": "5",
    "plot": "Description..."
  },
  "seasons": [
    { "season_id": 1, "name": "Season 1" }
  ],
  "episodes": {
    "1": [
      { "id": "101", "title": "Episode 1" }
    ]
  }
}
```

**Malformed (Empty Array):**
```json
[]
```

### Why Providers Return Arrays

1. **Database inconsistency** - Series record exists but data deleted
2. **Placeholder entries** - Series announced but not yet available
3. **API bugs** - Provider software defects
4. **Removed content** - Series taken down but ID still referenced

---

## Solution Architecture

### Detection Point

Detection happens in the `QueryApi<T>` method - the central point for all API calls:

```
┌─────────────────────────────────────────────────────────────┐
│                     QueryApi<T> Flow                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Build URL                                               │
│                                                             │
│  2. Execute HTTP request                                    │
│     └─► Optional retry handling                             │
│                                                             │
│  3. ┌─────────────────────────────────────────────────────┐ │
│     │  DETECTION POINT                                    │ │
│     │  Check: JSON starts with '[' AND T == SeriesStream? │ │
│     │                                                     │ │
│     │  If yes: Log warning, return empty object           │ │
│     │  If no:  Continue to deserialization                │ │
│     └─────────────────────────────────────────────────────┘ │
│                                                             │
│  4. Deserialize JSON to type T                              │
│                                                             │
│  5. Return result                                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Why Detect Before Deserialization?

1. **Avoid exception overhead** - Exceptions are expensive
2. **Clean error handling** - No need for catch block
3. **Informative logging** - Know exactly what went wrong
4. **Faster recovery** - No stack unwinding

---

## Type-Specific Handling

### Why Only SeriesStreamInfo?

Most API endpoints return lists naturally:
- `get_series` → `List<Series>` (array expected)
- `get_vod_streams` → `List<StreamInfo>` (array expected)
- `get_categories` → `List<Category>` (array expected)

Only `get_series_info` expects an object:
- `get_series_info` → `SeriesStreamInfo` (object expected)

### Detection Logic

```csharp
string trimmedJson = jsonContent.TrimStart();
if (trimmedJson.StartsWith('[') && typeof(T) == typeof(SeriesStreamInfo))
{
    // Handle malformed response
    return (T)(object)new SeriesStreamInfo();
}
```

Key points:
- `TrimStart()` handles whitespace before JSON
- `StartsWith('[')` is O(1) check
- Type check ensures we only intercept SeriesStreamInfo
- Other types pass through normally

---

## Empty Object Factory

For graceful degradation, the `GetEmptyObject<T>` method returns appropriate empty instances:

```
┌─────────────────────────────────────────────────────────────┐
│                   GetEmptyObject<T>                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  typeof(T) == SeriesStreamInfo?  → new SeriesStreamInfo()   │
│  typeof(T) == List<Series>?      → new List<Series>()       │
│  typeof(T) == List<Category>?    → new List<Category>()     │
│  typeof(T) == List<StreamInfo>?  → new List<StreamInfo>()   │
│  typeof(T) == VodStreamInfo?     → new VodStreamInfo()      │
│  typeof(T) == PlayerApi?         → new PlayerApi()          │
│  typeof(T) == EpgListings?       → new EpgListings()        │
│  else                            → default(T)               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

This factory is also used for:
- HTTP retry exhaustion (persistent failures)
- Network timeouts
- Server errors

---

## Data Flow

### Normal Flow
```
Request → HTTP → JSON → Deserialize → SeriesStreamInfo → Use
```

### Malformed Flow
```
Request → HTTP → JSON → Detect '[' → Log Warning → Empty Object → Use
```

### Impact on Callers

Callers receive an empty object instead of exception:

```csharp
// Caller code (unchanged)
SeriesStreamInfo info = await client.GetSeriesStreamsBySeriesAsync(creds, id, ct);

// info.Episodes will be empty dictionary
// info.Seasons will be empty list
// info.Info will be default (empty) SeriesInfo

// Downstream code handles empty gracefully
if (info.Episodes.Count == 0)
{
    return new List<Episode>();  // No episodes available
}
```

---

## Logging Strategy

### Warning Level
```
Xtream API returned array instead of object for SeriesStreamInfo (URL: {Url})
```

**Why Warning (not Error)?**
- Not a plugin bug
- Not actionable by user
- Provider-side issue
- Plugin continues functioning

### Debug Information
The URL is logged to help identify:
- Which series ID has issues
- Which provider endpoint
- Pattern of failures

---

## Design Decisions

### Decision 1: Pre-Deserialization Detection vs Try-Catch

**Options:**
1. Let deserializer throw, catch JsonSerializationException
2. Detect before deserializing (chosen)

**Rationale:** Pre-detection is cleaner:
- Avoids exception overhead
- More specific error message
- No need to parse exception message

### Decision 2: Return Empty vs Throw

**Options:**
1. Throw custom exception
2. Return null
3. Return empty object (chosen)

**Rationale:** Empty object allows graceful degradation:
- Callers don't need null checks
- Callers don't need try-catch
- Empty data is valid edge case

### Decision 3: Type-Specific vs Generic

**Options:**
1. Check all types for array-vs-object mismatch
2. Only check SeriesStreamInfo (chosen)

**Rationale:**
- Only SeriesStreamInfo is affected in practice
- Other types naturally expect arrays
- Simpler implementation

---

## Error Recovery Matrix

| Response | Expected Type | Detection | Result |
|----------|---------------|-----------|--------|
| `{...}` | SeriesStreamInfo | No | Normal deserialize |
| `[]` | SeriesStreamInfo | Yes | Empty object |
| `[{...}]` | SeriesStreamInfo | Yes | Empty object |
| `[]` | List<Series> | No | Empty list (normal) |
| `{...}` | List<Series> | No | Deserialize error (rare) |

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history
