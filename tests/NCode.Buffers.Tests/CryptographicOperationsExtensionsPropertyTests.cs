// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using FsCheck;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class CryptographicOperationsExtensionsPropertyTests
{
    private const string Seed = "20261115,12345";

    #region FixedTimeEquals Tests

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_MatchesSequenceEqual(byte[] left, byte[] right)
    {
        return CryptographicOperations.FixedTimeEquals(left.AsSpan(), right.AsSpan())
            == left.AsSpan().SequenceEqual(right);
    }

    [Property(Replay = Seed)]
    public bool FixedTimeEquals_GivenACopy_ReturnsTrue(byte[] value)
    {
        return CryptographicOperations.FixedTimeEquals(value.AsSpan(), value.ToArray().AsSpan());
    }

    #endregion

    #region ZeroMemory Tests

    [Property(Replay = Seed)]
    public bool ZeroMemory_ZeroesEveryElementAndKeepsTheLength(byte[] value)
    {
        var length = value.Length;

        CryptographicOperations.ZeroMemory(value.AsSpan());

        return value.Length == length && value.All(b => b == 0);
    }

    #endregion
}
