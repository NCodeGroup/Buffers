// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;

namespace NCode.Buffers.Tests;

// The test that reads the shared secure pool's queue must not run beside other tests that rent from the pool.
[CollectionDefinition(nameof(SecretTests), DisableParallelization = true)]
[Collection(nameof(SecretTests))]
public class SecretTests
{
    private const int LargeLength = 5000;

    private static void CallMember(Secret<byte> secret, Secret<byte> other, string member)
    {
        switch (member)
        {
            case "Reveal":
                _ = secret.Reveal().Length;
                break;
            case "RevealMemory":
                _ = secret.RevealMemory();
                break;
            case "CopyTo":
            {
                var destination = new byte[6];
                secret.CopyTo(destination);
                break;
            }
            case "FixedTimeEquals":
                _ = secret.FixedTimeEquals("s3cr3t"u8);
                break;
            case "EqualsOther":
                _ = secret.Equals(other);
                break;
            case "OtherEquals":
                _ = other.Equals(secret);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(member),
                    member,
                    "No call is written for the member."
                );
        }
    }

    #region Create Tests

    [Fact]
    public void Create_CopiesTheValue()
    {
        byte[] value = [1, 2, 3, 4];

        using var secret = Secret.Create<byte>(value);
        value[0] = 99;
        var revealed = secret.Reveal().ToArray();

        Assert.Equal(4, secret.Length);
        Assert.Equal<byte>([1, 2, 3, 4], revealed);
    }

    [Fact]
    public void Create_Empty_HasLengthZero()
    {
        using var secret = Secret.Create<byte>([]);
        var isEmpty = secret.Reveal().IsEmpty;

        Assert.Equal(0, secret.Length);
        Assert.True(isEmpty);
    }

    [Fact]
    public void Create_WithAction_WritesInPlace()
    {
        using var secret = Secret.Create<byte, byte>(4, 7, static (span, seed) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (byte)(seed + i);
            }
        });
        var value = secret.Reveal().ToArray();

        Assert.Equal(4, secret.Length);
        Assert.Equal<byte>([7, 8, 9, 10], value);
    }

    [Fact]
    public void Create_WithAction_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Secret.Create<byte, int>(-1, 0, static (_, _) => { })
        );

        Assert.Equal("length", exception.ParamName);
    }

    [Fact]
    public void Create_WithAction_NullAction_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            Secret.Create<byte, int>(4, 0, null!)
        );

        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void Create_WithAction_ActionThrows_RethrowsAndZeroes()
    {
        const int length = 16;
        const byte fill = 0x5A;
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        Assert.Throws<InvalidOperationException>(() =>
            Secret.Create<byte, int>(length, 0, static (span, _) =>
            {
                span.Fill(fill);
                throw new InvalidOperationException();
            })
        );

        var isReturned = SecureMemoryPool<byte>.Shared.MemoryQueue.TryPeek(out var page);

        Assert.True(isReturned);
        Assert.NotNull(page);
        Assert.All(page, b => Assert.Equal(0, b));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsRedacted()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var text = secret.ToString();

        Assert.Equal("[REDACTED]", text);
    }

    [Fact]
    public void ToString_WhenInterpolated_ReturnsRedacted()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var text = $"{secret}";

        Assert.Equal("[REDACTED]", text);
    }

    [Fact]
    public void ToString_OfAnAnonymousTypeHoldingIt_HidesTheValue()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        var wrapped = new { Name = secret };

        var text = wrapped.ToString();

        Assert.Contains("[REDACTED]", text);
        Assert.DoesNotContain("s3cr3t", text);
    }

    #endregion

    #region DebuggerDisplay Tests

    [Fact]
    public void DebuggerDisplay_IsRedacted()
    {
        var attribute = typeof(Secret<byte>).GetCustomAttribute<DebuggerDisplayAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("[REDACTED]", attribute.Value);
    }

    #endregion

    #region Equals Tests

    [Fact]
    public void Equals_ComparesContents()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        using var equal = Secret.Create("s3cr3t"u8);
        using var unequal = Secret.Create("s3cr3x"u8);
        using var longer = Secret.Create("s3cr3t!"u8);

        var isEqual = secret.Equals(equal);
        var isUnequalEqual = secret.Equals(unequal);
        var isLongerEqual = secret.Equals(longer);

        Assert.True(isEqual);
        Assert.False(isUnequalEqual);
        Assert.False(isLongerEqual);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var isEqual = secret.Equals((Secret<byte>?)null);

        Assert.False(isEqual);
    }

    [Fact]
    public void Equals_SameInstance_ReturnsTrue()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var isEqual = secret.Equals(secret);

        Assert.True(isEqual);
    }

    [Fact]
    public void EqualsObject_EqualSecret_ReturnsTrue()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        using var equal = Secret.Create("s3cr3t"u8);

        var isEqual = secret.Equals((object)equal);

        Assert.True(isEqual);
    }

    [Fact]
    public void EqualsObject_NotASecret_ReturnsFalse()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        const string text = "s3cr3t";

        var isEqual = secret.Equals((object)text);

        Assert.False(isEqual);
    }

    [Fact]
    public void EqualsObject_Null_ReturnsFalse()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var isEqual = secret.Equals((object?)null);

        Assert.False(isEqual);
    }

    [Fact]
    public void Equals_SameInstanceAfterDispose_ThrowsObjectDisposedException()
    {
        var secret = Secret.Create("s3cr3t"u8);
        secret.Dispose();

        Assert.Throws<ObjectDisposedException>(() => secret.Equals(secret));
    }

    [Fact]
    public void Equals_NullAfterDispose_ReturnsFalse()
    {
        var secret = Secret.Create("s3cr3t"u8);
        secret.Dispose();

        var isEqual = secret.Equals((Secret<byte>?)null);

        Assert.False(isEqual);
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_ReturnsLength()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var hashCode = secret.GetHashCode();

        Assert.Equal(6, hashCode);
    }

    #endregion

    #region FixedTimeEquals Tests

    [Fact]
    public void FixedTimeEquals_ComparesContents()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var matchesEqual = secret.FixedTimeEquals("s3cr3t"u8);
        var matchesDifferent = secret.FixedTimeEquals("s3cr3x"u8);
        var matchesLonger = secret.FixedTimeEquals("s3cr3t!"u8);

        Assert.True(matchesEqual);
        Assert.False(matchesDifferent);
        Assert.False(matchesLonger);
    }

    #endregion

    #region CopyTo Tests

    [Fact]
    public void CopyTo_ShortDestination_ThrowsArgumentException()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        var destination = new byte[3];

        var exception = Assert.Throws<ArgumentException>(() => secret.CopyTo(destination));

        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public void CopyTo_CopiesTheValue()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        var destination = new byte[6];

        secret.CopyTo(destination);
        var expected = "s3cr3t"u8.ToArray();

        Assert.Equal(expected, destination);
    }

    #endregion

    #region RevealMemory Tests

    [Fact]
    public void RevealMemory_ReturnsTheValue()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var expected = "s3cr3t"u8.ToArray();
        var value = secret.RevealMemory().ToArray();

        Assert.Equal(expected, value);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ZeroesTheSecret()
    {
        var value = new byte[LargeLength];
        Array.Fill(value, (byte)0xA5);
        var secret = Secret.Create<byte>(value);
        var memory = secret.RevealMemory();
        var before = memory.ToArray();
        Assert.All(before, b => Assert.Equal(0xA5, b));

        secret.Dispose();
        var after = memory.ToArray();

        Assert.All(after, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Dispose_Twice_DoesNothing()
    {
        var secret = Secret.Create("s3cr3t"u8);

        secret.Dispose();
        var exception = Record.Exception(secret.Dispose);

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Reveal")]
    [InlineData("RevealMemory")]
    [InlineData("CopyTo")]
    [InlineData("FixedTimeEquals")]
    [InlineData("EqualsOther")]
    [InlineData("OtherEquals")]
    public void Dispose_ThenMember_ThrowsObjectDisposedException(string member)
    {
        var secret = Secret.Create("s3cr3t"u8);
        using var other = Secret.Create("s3cr3t"u8);
        secret.Dispose();

        Assert.Throws<ObjectDisposedException>(() => CallMember(secret, other, member));
    }

    #endregion
}
