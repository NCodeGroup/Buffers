// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class StringSegmentsPropertyTests
{
    private const string Seed = "20261115,12345";

    #region Split Tests

    [Property(Replay = Seed)]
    public bool Split_WhenJoinedWithSeparator_RoundTripsTheOriginal(
        NonNull<string> value,
        char separator
    )
    {
        var segments = StringSegments.Split(value.Get, separator);

        var joined = string.Join(separator, segments.Select(segment => segment.Memory.ToString()));

        return joined == value.Get;
    }

    [Property(Replay = Seed)]
    public bool Split_Count_MatchesStringSplit(NonNull<string> value, char separator)
    {
        var segments = StringSegments.Split(value.Get, separator);

        return segments.Count == value.Get.Split(separator).Length
            && segments.Count == segments.ToArray().Length;
    }

    [Property(Replay = Seed)]
    public bool Split_SegmentsAreSlicesOfTheOriginal(NonNull<string> value, char separator)
    {
        var segments = StringSegments.Split(value.Get, separator);

        var total = segments.Sum(segment => segment.Memory.Length);
        var separators = Math.Max(segments.Count - 1, 0);

        return total + separators == value.Get.Length
            && segments.All(segment => !segment.Memory.Span.Contains(separator));
    }

    [Property(Replay = Seed)]
    public bool Split_WithSpanSeparator_NeverThrowsAndRoundTrips(
        NonNull<string> value,
        NonNull<string> separator
    )
    {
        if (separator.Get.Length == 0)
        {
            return true;
        }

        var segments = StringSegments.Split(
            value.Get,
            separator.Get.AsSpan(),
            StringComparison.Ordinal
        );

        var joined = string.Join(
            separator.Get,
            segments.Select(segment => segment.Memory.ToString())
        );

        return joined == value.Get;
    }

    #endregion
}
