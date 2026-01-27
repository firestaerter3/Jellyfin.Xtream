# Feature 05: Malformed JSON Handling - Changelog

## Document Info
- **Feature:** Malformed JSON Handling
- **Version:** 0.9.5.3
- **Status:** Implemented
- **Last Updated:** 2026-01-27

---

## Version History

### v0.9.5.3 (2026-01-XX)

#### Bug Fix

**JSON Deserialization Crash on Malformed Response**
- **Problem:** Plugin crashed when Xtream API returned array instead of object
- **Error:** `JsonSerializationException: Cannot deserialize the current JSON array`
- **Affected API:** `get_series_info` endpoint (returns `SeriesStreamInfo`)
- **Root Cause:** Some providers return `[]` for series with no data instead of `{}`

**Solution:**
- Added pre-deserialization check for array responses
- Check if JSON starts with `[` when expecting `SeriesStreamInfo`
- Return empty `SeriesStreamInfo` object instead of crashing
- Log warning with URL for debugging

**Files Changed:**
- `Client/XtreamClient.cs` - QueryApi method (lines 136-154)
- `Client/XtreamClient.cs` - GetEmptyObject helper (lines 161-200)

**Technical Details:**
```csharp
// Check if we're expecting an object but got an array
string trimmedJson = jsonContent.TrimStart();
if (trimmedJson.StartsWith('[') && typeof(T) == typeof(SeriesStreamInfo))
{
    logger.LogWarning("Xtream API returned array instead of object...");
    return (T)(object)new SeriesStreamInfo();
}
```

---

## Impact

| Metric | Value |
|--------|-------|
| Users Affected | All users with affected providers |
| Severity | High (caused crashes) |
| Risk | Low (detection is specific, no false positives) |

---

## Discovery

- **Symptom:** Cache refresh would abort partway through
- **Error:** JsonSerializationException in logs
- **Investigation:** Found certain series IDs always returned `[]`
- **Root Cause:** Provider database inconsistency

---

## Complementary Features

This fix works together with:
- **Feature 04 (Eager Caching):** Cache refresh now completes fully
- **HTTP Retry Handler:** Both use `GetEmptyObject<T>` for graceful degradation

---

## Migration Notes

No migration required. Fix is automatic and backwards compatible.

---

## Future Considerations

If other types are affected:
1. Add type check in QueryApi detection logic
2. Add type case in GetEmptyObject factory
3. Document in this changelog

Currently only `SeriesStreamInfo` is known to be affected.

---

## Related Documentation

- [Requirements](REQUIREMENTS.md) - Problem statement
- [Architecture](ARCHITECTURE.md) - Detection design
- [Implementation](IMPLEMENTATION.md) - Code changes
- [Test Plan](TEST_PLAN.md) - Test cases
