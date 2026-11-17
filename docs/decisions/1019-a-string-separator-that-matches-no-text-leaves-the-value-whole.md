# 1019. A string separator that matches no text leaves the value whole

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

`StringSegments.Split` and the `SplitSegments` extensions take a `ReadOnlySpan<char>` separator and an optional
`StringComparison`. A split finds the separator, cuts out the text it matched and searches again after it. An empty
separator matches no text at every position. Under a culture-aware comparison a separator can match text of another
length, such as `"e\u0301"` matching the precomposed `"\u00E9"`, or no text at all when it is made only of ignorable
characters, such as the soft hyphen U+00AD. A loop that advances by the separator's own length never moves past an
empty separator, so the call allocates segments until the process runs out of memory
([GHSA-pmgm-gmr9-6v3g](https://github.com/NCodeGroup/Buffers/security/advisories/GHSA-pmgm-gmr9-6v3g)), and with a
culture-aware match of another length it cuts in the wrong place or slices past the end of the value. The separator
can come from configuration or input the caller doesn't control.

`string.Split(string)` returns the whole string as its one element for an empty separator, and
`string.Replace(string, string, StringComparison)` leaves a string unchanged when the text to replace matches no text.

## Decision

**A split cuts out the text the separator matched, at that text's length, and a separator that matches no text
leaves the value whole: one segment that holds the whole value.**

1. An empty separator returns one segment holding the whole value, under every `StringComparison`, also for an empty
   value.
2. Under `CurrentCulture`, `InvariantCulture` and their `IgnoreCase` forms, the search reports the length of the text
   it matched (`CompareInfo.IndexOf` with `matchLength`) and the split advances past that text. A separator that
   matches no text there, such as one made only of ignorable characters, returns one segment holding the whole value.
3. Under an ordinal comparison, the matched text is as long as the separator.
4. The XML documentation of every overload that takes a `ReadOnlySpan<char>` separator states this.

## Options considered

- One segment holding the whole value (chosen): `string.Split(string)` gives the same answer, and no separator from
  input can make the call fail or loop.
- Throw `ArgumentException` for an empty separator. Rejected: a caller with a configurable separator has to
  special-case it, which `string.Split` doesn't ask for.
- Split between every character. Rejected: no .NET API does it, and one empty input allocates a segment per
  character.

## Consequences

- Every match a split cuts out is at least one character long, so no separator makes a split loop.
- A culture-aware split returns the segments between the matches as that comparison sees them.
- A new splitting API follows the same rule.

## References

- [GHSA-pmgm-gmr9-6v3g](https://github.com/NCodeGroup/Buffers/security/advisories/GHSA-pmgm-gmr9-6v3g)
- [StringSegments.cs](../../src/NCode.Buffers/StringSegments.cs)
- [String.Split](https://learn.microsoft.com/dotnet/api/system.string.split)
- [CompareInfo.IndexOf](https://learn.microsoft.com/dotnet/api/system.globalization.compareinfo.indexof)
