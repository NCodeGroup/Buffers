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

        var texts = segments.Select(segment => segment.Memory.ToString());
        var joined = string.Join(separator, texts);

        return joined == value.Get;
    }

    [Property(Replay = Seed)]
    public bool Split_WhenCounting_CountMatchesStringSplit(NonNull<string> value, char separator)
    {
        var segments = StringSegments.Split(value.Get, separator);

        var expected = value.Get.Split(separator);

        return segments.Count == expected.Length && segments.Count == segments.ToArray().Length;
    }

    [Property(Replay = Seed)]
    public bool Split_WhenSlicing_SegmentsHoldNoSeparatorAndCoverTheOriginal(
        NonNull<string> value,
        char separator
    )
    {
        var segments = StringSegments.Split(value.Get, separator);

        var total = segments.Sum(segment => segment.Memory.Length);
        var separators = segments.Count - 1;

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

        var separatorSpan = separator.Get.AsSpan();
        var segments = StringSegments.Split(value.Get, separatorSpan, StringComparison.Ordinal);

        var texts = segments.Select(segment => segment.Memory.ToString());
        var joined = string.Join(separator.Get, texts);

        return joined == value.Get;
    }

    #endregion
}
