// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class CryptographicOperationsExtensionsPropertyTests
{
    private const string Seed = "20261115,12345";

    #region FixedTimeEquals Tests

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_WithInts_MatchesSequenceEqual(int[] left, int[] right)
    {
        var expected = left.AsSpan().SequenceEqual(right);

        var actual = CryptographicOperationsExtensions.FixedTimeEquals<int>(left, right);

        return actual == expected;
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_WithBytes_MatchesSequenceEqual(byte[] left, byte[] right)
    {
        var expected = left.AsSpan().SequenceEqual(right);

        var actual = CryptographicOperationsExtensions.FixedTimeEquals<byte>(left, right);

        return actual == expected;
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_GivenACopy_ReturnsTrue(long[] value)
    {
        var copy = value.ToArray();

        return CryptographicOperationsExtensions.FixedTimeEquals<long>(value, copy);
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEqualsInChunks_GivenAnyChunkSize_MatchesSequenceEqual(
        int[] left,
        int[] right,
        PositiveInt maxElementsPerChunk
    )
    {
        var expected = left.AsSpan().SequenceEqual(right);

        var actual = CryptographicOperationsExtensions.FixedTimeEqualsInChunks<int>(
            left,
            right,
            maxElementsPerChunk.Get
        );

        return actual == expected;
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEqualsInChunks_GivenACopyAndAnyChunkSize_ReturnsTrue(
        long[] value,
        PositiveInt maxElementsPerChunk
    )
    {
        var copy = value.ToArray();

        return CryptographicOperationsExtensions.FixedTimeEqualsInChunks<long>(
            value,
            copy,
            maxElementsPerChunk.Get
        );
    }

    #endregion

    #region ZeroMemory Tests

    [Property(Replay = Seed)]
    public bool ZeroMemory_WithInts_ZeroesEveryElementAndKeepsTheLength(int[] value)
    {
        var length = value.Length;

        CryptographicOperationsExtensions.ZeroMemory<int>(value);

        return value.Length == length && value.All(element => element == 0);
    }

    [Property(Replay = Seed)]
    public bool ZeroMemory_WithBytes_ZeroesEveryElementAndKeepsTheLength(byte[] value)
    {
        var length = value.Length;

        CryptographicOperationsExtensions.ZeroMemory<byte>(value);

        return value.Length == length && value.All(element => element == 0);
    }

    #endregion
}
