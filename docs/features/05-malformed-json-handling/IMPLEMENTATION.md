# Feature 05: Malformed JSON Handling - Implementation

## Document Info
- **Feature:** Malformed JSON Handling
- **Version:** 0.9.5.3
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Overview

This document details the code changes implementing malformed JSON handling.

---

## Files Modified

| File | Changes | Purpose |
|------|---------|---------|
| `Client/XtreamClient.cs` | ~70 lines added | Array detection and empty object factory |

---

## Code Changes

### XtreamClient.cs - QueryApi Method (Lines 110-154)

**Array Detection Logic:**

```csharp
private async Task<T> QueryApi<T>(ConnectionInfo connectionInfo, string urlPath,
    CancellationToken cancellationToken)
{
    Uri uri = new Uri(connectionInfo.BaseUrl + urlPath);

    // ... HTTP request handling with retry ...

    string? jsonContent = /* response from HTTP request */;

    try
    {
        // Check if we're expecting an object but got an array
        string trimmedJson = jsonContent.TrimStart();
        if (trimmedJson.StartsWith('[') && typeof(T) == typeof(SeriesStreamInfo))
        {
            logger.LogWarning(
                "Xtream API returned array instead of object for SeriesStreamInfo " +
                "(URL: {Url}). Returning empty object.", uri);
            return (T)(object)new SeriesStreamInfo();
        }

        return JsonConvert.DeserializeObject<T>(jsonContent, _serializerSettings)!;
    }
    catch (JsonSerializationException ex)
    {
        string jsonSample = jsonContent.Length > 500
            ? string.Concat(jsonContent.AsSpan(0, 500), "...")
            : jsonContent;
        logger.LogError(ex,
            "Failed to deserialize response from Xtream API (URL: {Url}). " +
            "JSON content: {Json}", uri, jsonSample);
        throw;
    }
}
```

**Key Implementation Points:**

1. **Whitespace Handling:** `TrimStart()` handles cases where response has leading whitespace
2. **Type Check:** `typeof(T) == typeof(SeriesStreamInfo)` ensures we only intercept the affected type
3. **Logging:** Warning includes URL for debugging which series has issues
4. **Cast Pattern:** `(T)(object)new SeriesStreamInfo()` works around generic constraints

---

### XtreamClient.cs - GetEmptyObject Method (Lines 156-200)

```csharp
/// <summary>
/// Returns an empty object of the specified type for graceful degradation.
/// </summary>
private static T GetEmptyObject<T>()
{
    if (typeof(T) == typeof(SeriesStreamInfo))
    {
        return (T)(object)new SeriesStreamInfo();
    }

    if (typeof(T) == typeof(List<Series>))
    {
        return (T)(object)new List<Series>();
    }

    if (typeof(T) == typeof(List<Category>))
    {
        return (T)(object)new List<Category>();
    }

    if (typeof(T) == typeof(List<StreamInfo>))
    {
        return (T)(object)new List<StreamInfo>();
    }

    if (typeof(T) == typeof(VodStreamInfo))
    {
        return (T)(object)new VodStreamInfo();
    }

    if (typeof(T) == typeof(PlayerApi))
    {
        return (T)(object)new PlayerApi();
    }

    if (typeof(T) == typeof(EpgListings))
    {
        return (T)(object)new EpgListings();
    }

    // Default fallback
    return default(T)!;
}
```

**Purpose:** This factory method is used for:
1. Malformed JSON responses (array instead of object)
2. Persistent HTTP failures (after retries exhausted)
3. Any scenario requiring graceful degradation

---

## Detection Logic Explained

### Why Check for `[` Character?

JSON arrays always start with `[`, objects with `{`:
- `[]` - empty array
- `[1, 2, 3]` - array with values
- `[{"key": "value"}]` - array of objects
- `{}` - empty object
- `{"key": "value"}` - object with properties

### Why Only SeriesStreamInfo?

| API Endpoint | Expected Type | Returns Array? |
|--------------|---------------|----------------|
| get_series | List<Series> | Yes (correct) |
| get_series_info | SeriesStreamInfo | No (but some providers do) |
| get_vod_streams | List<StreamInfo> | Yes (correct) |
| get_categories | List<Category> | Yes (correct) |

Only `get_series_info` expects an object, making it the only candidate for this mismatch.

---

## Example Scenarios

### Scenario 1: Normal Response

**Request:** `GET /player_api.php?...&action=get_series_info&series_id=123`

**Response:**
```json
{
  "info": { "name": "Breaking Bad" },
  "seasons": [...],
  "episodes": {...}
}
```

**Flow:**
1. Check: `trimmedJson.StartsWith('[')` → false
2. Normal deserialization proceeds
3. Returns populated `SeriesStreamInfo`

### Scenario 2: Malformed Response (Empty Array)

**Request:** `GET /player_api.php?...&action=get_series_info&series_id=999`

**Response:**
```json
[]
```

**Flow:**
1. Check: `trimmedJson.StartsWith('[')` → true
2. Check: `typeof(T) == typeof(SeriesStreamInfo)` → true
3. Log warning with URL
4. Return `new SeriesStreamInfo()` (empty)

### Scenario 3: Malformed Response (Non-Empty Array)

**Response:**
```json
[{"some": "data"}]
```

**Flow:** Same as Scenario 2 - detected and handled gracefully.

---

## Integration with Retry Handler

The malformed JSON detection works alongside the retry handler:

```
HTTP Request
    │
    ▼
Retry Handler (if enabled)
    │
    ├── Success → JSON Response
    │                 │
    │                 ▼
    │         ┌─────────────────────┐
    │         │  Malformed Check    │
    │         │  Array for object?  │
    │         └─────────┬───────────┘
    │                   │
    │             Yes   │   No
    │              │    │    │
    │              ▼    │    ▼
    │         Empty     │  Deserialize
    │         Object    │    │
    │              │    │    │
    │              └────┼────┘
    │                   │
    │                   ▼
    │              Return Result
    │
    └── Persistent Failure → GetEmptyObject<T>()
```

---

## Error Handling

### Remaining Catch Block

Even with array detection, a catch block remains for other deserialization errors:

```csharp
catch (JsonSerializationException ex)
{
    string jsonSample = jsonContent.Length > 500
        ? string.Concat(jsonContent.AsSpan(0, 500), "...")
        : jsonContent;
    logger.LogError(ex,
        "Failed to deserialize response from Xtream API (URL: {Url}). " +
        "JSON content: {Json}", uri, jsonSample);
    throw;
}
```

This catches:
- Invalid JSON syntax
- Type mismatches not caught by array check
- Missing required properties
- Other deserialization failures

---

## Testing Verification

To verify the fix works:

1. **Check Logs:** Look for warning message with "returned array instead of object"
2. **Cache Refresh:** Verify refresh completes without crashing
3. **Browse Series:** Malformed series shows as empty (no episodes)
4. **Other Series:** Unaffected series work normally

---

## Backwards Compatibility

The fix is fully backwards compatible:
- Normal responses processed unchanged
- No configuration changes required
- No API changes to callers
- Empty objects are valid return values

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Architecture](ARCHITECTURE.md) - Detection design
- [Test Plan](TEST_PLAN.md) - Test cases
- [Changelog](CHANGELOG.md) - Version history
