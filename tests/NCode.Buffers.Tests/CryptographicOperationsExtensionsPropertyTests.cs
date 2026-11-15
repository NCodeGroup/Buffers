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
        return CryptographicOperationsExtensions.FixedTimeEquals<int>(left, right)
            == left.AsSpan().SequenceEqual(right);
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_WithBytes_MatchesSequenceEqual(byte[] left, byte[] right)
    {
        return CryptographicOperationsExtensions.FixedTimeEquals<byte>(left, right)
            == left.AsSpan().SequenceEqual(right);
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_GivenACopy_ReturnsTrue(long[] value)
    {
        return CryptographicOperationsExtensions.FixedTimeEquals<long>(value, value.ToArray());
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
