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

    #region Create Tests

    [Fact]
    public void Create_CopiesTheValue()
    {
        byte[] value = [1, 2, 3, 4];

        using var secret = Secret.Create<byte>(value);
        value[0] = 99;

        Assert.Equal(4, secret.Length);
        Assert.Equal<byte>([1, 2, 3, 4], secret.Reveal().ToArray());
    }

    [Fact]
    public void Create_Empty_HasLengthZero()
    {
        using var secret = Secret.Create<byte>([]);

        Assert.Equal(0, secret.Length);
        Assert.True(secret.Reveal().IsEmpty);
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

        Assert.Equal(4, secret.Length);
        Assert.Equal<byte>([7, 8, 9, 10], secret.Reveal().ToArray());
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

        Assert.True(SecureMemoryPool<byte>.Shared.MemoryQueue.TryPeek(out var page));
        Assert.All(page, b => Assert.Equal(0, b));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsRedacted()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        var wrapped = new { Name = secret };

        Assert.Equal("[REDACTED]", secret.ToString());
        Assert.Equal("[REDACTED]", $"{secret}");
        Assert.Contains("[REDACTED]", wrapped.ToString());
        Assert.DoesNotContain("s3cr3t", wrapped.ToString());
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

        Assert.True(secret.Equals(equal));
        Assert.False(secret.Equals(unequal));
        Assert.False(secret.Equals(longer));
        Assert.False(secret.Equals((Secret<byte>?)null));
        Assert.True(secret.Equals(secret));
        Assert.True(secret.Equals((object)equal));
        Assert.False(secret.Equals((object)"s3cr3t"));
        Assert.False(secret.Equals((object?)null));
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

        Assert.Equal(6, secret.GetHashCode());
    }

    #endregion

    #region FixedTimeEquals Tests

    [Fact]
    public void FixedTimeEquals_ComparesContents()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        Assert.True(secret.FixedTimeEquals("s3cr3t"u8));
        Assert.False(secret.FixedTimeEquals("s3cr3x"u8));
        Assert.False(secret.FixedTimeEquals("s3cr3t!"u8));
    }

    #endregion

    #region CopyTo Tests

    [Fact]
    public void CopyTo_ShortDestination_ThrowsArgumentException()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        var destination = new byte[3];

        Assert.Throws<ArgumentException>(() => secret.CopyTo(destination));
    }

    [Fact]
    public void CopyTo_CopiesTheValue()
    {
        using var secret = Secret.Create("s3cr3t"u8);
        var destination = new byte[6];

        secret.CopyTo(destination);

        Assert.Equal("s3cr3t"u8.ToArray(), destination);
    }

    #endregion

    #region RevealMemory Tests

    [Fact]
    public void RevealMemory_ReturnsTheValue()
    {
        using var secret = Secret.Create("s3cr3t"u8);

        Assert.Equal("s3cr3t"u8.ToArray(), secret.RevealMemory().ToArray());
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
        Assert.All(memory.ToArray(), b => Assert.Equal(0xA5, b));

        secret.Dispose();

        Assert.All(memory.ToArray(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Dispose_Twice_DoesNothing()
    {
        var secret = Secret.Create("s3cr3t"u8);

        secret.Dispose();
        var exception = Record.Exception(secret.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ThenMembers_ThrowObjectDisposedException()
    {
        var secret = Secret.Create("s3cr3t"u8);
        using var other = Secret.Create("s3cr3t"u8);
        secret.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = secret.Reveal().Length;
        });
        Assert.Throws<ObjectDisposedException>(() => secret.RevealMemory());
        Assert.Throws<ObjectDisposedException>(() => secret.CopyTo(new byte[6]));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = secret.FixedTimeEquals("s3cr3t"u8);
        });
        Assert.Throws<ObjectDisposedException>(() => secret.Equals(other));
        Assert.Throws<ObjectDisposedException>(() => other.Equals(secret));
    }

    #endregion
}
