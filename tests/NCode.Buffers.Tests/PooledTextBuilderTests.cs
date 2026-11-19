// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace NCode.Buffers.Tests;

public class PooledTextBuilderTests
{
    private readonly struct WritesThenFails : ISpanFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => "yyyyyyyyyy";

        public bool TryFormat(
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider
        )
        {
            if (destination.Length < 10)
            {
                destination.Fill('x');
                charsWritten = 0;
                return false;
            }

            destination[..10].Fill('y');
            charsWritten = 10;
            return true;
        }
    }

    private readonly struct WritesThenThrows : ISpanFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => "x";

        public bool TryFormat(
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider
        )
        {
            destination.Fill('x');
            throw new InvalidOperationException();
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NegativeInitialCapacity_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new PooledTextBuilder(isSensitive: false, initialCapacity: -1);
        });

        Assert.Equal("initialCapacity", exception.ParamName);
    }

    [Fact]
    public void Constructor_InitialCapacity_RentsAtLeastThatCapacity()
    {
        const int initialCapacity = 100;

        using var builder = new PooledTextBuilder(isSensitive: false, initialCapacity);

        Assert.True(builder.Capacity >= initialCapacity);
        Assert.True(builder.RentedMemory.Length >= initialCapacity);
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void Constructor_WithoutInitialCapacity_HasNoBuffer()
    {
        using var builder = new PooledTextBuilder(isSensitive: true);

        Assert.Equal(0, builder.Capacity);
        Assert.Equal(0, builder.Length);
        Assert.True(builder.IsSensitive);
    }

    [Fact]
    public void Constructor_InitialBuffer_UsesTheBuffer()
    {
        Span<char> initial = stackalloc char[8];

        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        Assert.Equal(8, builder.Capacity);
        Assert.Equal(0, builder.Length);
        Assert.False(builder.IsSensitive);
    }

    #endregion

    #region RentedMemory Tests

    [Fact]
    public void RentedMemory_WithoutARental_IsEmpty()
    {
        Span<char> initial = stackalloc char[8];

        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        Assert.True(builder.RentedMemory.IsEmpty);
    }

    #endregion

    #region Append Tests

    [Fact]
    public void Default_Append_RentsAndAppends()
    {
        using var builder = new PooledTextBuilder(isSensitive: false);

        builder.Append('a');

        Assert.True(builder.Capacity >= 256);
        Assert.Equal("a", builder.ToString());
    }

    [Fact]
    public void Append_Char_Appends()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append('a');
        builder.Append('b');

        Assert.Equal("ab", builder.ToString());
        Assert.Equal(2, builder.Length);
    }

    [Fact]
    public void Append_Char_PastCapacity_Grows()
    {
        Span<char> initial = stackalloc char[2];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append('a');
        builder.Append('b');
        builder.Append('c');

        Assert.Equal("abc", builder.ToString());
        Assert.True(builder.Capacity >= 3);
    }

    [Fact]
    public void Append_Span_Appends()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append("abc".AsSpan());
        builder.Append("def".AsSpan());

        Assert.Equal("abcdef", builder.ToString());
    }

    [Fact]
    public void Append_String_Appends()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append("hello");
        builder.Append(" world");

        Assert.Equal("hello world", builder.ToString());
    }

    [Fact]
    public void Append_Formattable_UsesFormatAndProvider()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append(3.5, "F2", CultureInfo.InvariantCulture);

        Assert.Equal("3.50", builder.ToString());
    }

    [Fact]
    public void Append_FormattablePastCapacity_Grows()
    {
        Span<char> initial = stackalloc char[4];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append(1234567890, default, CultureInfo.InvariantCulture);

        Assert.Equal("1234567890", builder.ToString());
        Assert.True(builder.Capacity >= 10);
    }

    [Fact]
    public void Append_PastAnInitialSpan_ZeroesTheSpan()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: true);

        builder.Append("abcdefgh");
        builder.Append("ijklmnopqrst");

        Assert.False(initial.ContainsAnyExcept('\0'));
        Assert.Equal("abcdefghijklmnopqrst", builder.ToString());
    }

    [Fact]
    public void Append_PastAnInitialSpan_WhenNotSensitive_LeavesTheSpan()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);

        builder.Append("abcdefgh");
        builder.Append("ijklmnopqrst");

        Assert.Equal("abcdefgh", new string(initial));
        Assert.Equal("abcdefghijklmnopqrst", builder.ToString());
    }

    [Fact]
    public void Append_ItsOwnWrittenSpanWhileGrowing_CopiesTheText()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: true);

        builder.Append("abcdefgh");
        builder.Append(builder.WrittenSpan);

        Assert.Equal("abcdefghabcdefgh", builder.ToString());
    }

    [Fact]
    public void Append_FormattableThatFailsAfterWriting_ZeroesTheInitialSpan()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: true);

        builder.Append(new WritesThenFails());

        Assert.False(initial.ContainsAnyExcept('\0'));
        Assert.Equal("yyyyyyyyyy", builder.ToString());
    }

    [Fact]
    public void Append_PastRentedCapacity_KeepsText()
    {
        var text = new string('a', 1000);
        using var builder = new PooledTextBuilder(isSensitive: false, initialCapacity: 4);

        builder.Append("abc");
        builder.Append(text);

        Assert.Equal("abc" + text, builder.ToString());
        Assert.Equal(1003, builder.Length);
    }

    #endregion

    #region AppendSpan Tests

    [Fact]
    public void AppendSpan_ReturnsWritableSpan()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);
        builder.Append("ab");

        var span = builder.AppendSpan(3);
        "xyz".CopyTo(span);

        Assert.Equal(3, span.Length);
        Assert.Equal(5, builder.Length);
        Assert.Equal("abxyz", builder.ToString());
    }

    [Fact]
    public void AppendSpan_PastCapacity_Grows()
    {
        Span<char> initial = stackalloc char[2];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);
        builder.Append("ab");

        var span = builder.AppendSpan(5);
        "xyzuv".CopyTo(span);

        Assert.Equal("abxyzuv", builder.ToString());
    }

    [Fact]
    public void AppendSpan_Negative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new PooledTextBuilder(isSensitive: false);
            _ = builder.AppendSpan(-1);
        });

        Assert.Equal("length", exception.ParamName);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ZeroesTheWrittenPart()
    {
        var initial = new char[8];
        var builder = new PooledTextBuilder(initial, isSensitive: true);
        builder.Append("secret");

        builder.Reset();

        Assert.Equal(0, builder.Length);
        Assert.Equal(8, builder.Capacity);
        Assert.False(initial.AsSpan().ContainsAnyExcept('\0'));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WhenSensitive_ZeroesTheBuffer()
    {
        var builder = new PooledTextBuilder(isSensitive: true, initialCapacity: 3000);
        builder.Append("secret");
        var memory = builder.RentedMemory;
        Assert.Equal("secret", new string(memory.Span[..6]));

        builder.Dispose();

        Assert.False(memory.Span.ContainsAnyExcept('\0'));
        Assert.Equal(0, builder.Length);
        Assert.Equal(0, builder.Capacity);
    }

    [Fact]
    public void Dispose_WithInitialSpan_ZeroesTheSpan()
    {
        Span<char> initial = stackalloc char[8];
        var builder = new PooledTextBuilder(initial, isSensitive: true);
        builder.Append("secret");

        builder.Dispose();

        Assert.False(initial.ContainsAnyExcept('\0'));
        Assert.Equal(0, builder.Length);
        Assert.Equal(0, builder.Capacity);
    }

    [Fact]
    public void Dispose_WithInitialSpan_WhenNotSensitive_LeavesTheSpan()
    {
        Span<char> initial = stackalloc char[8];
        var builder = new PooledTextBuilder(initial, isSensitive: false);
        builder.Append("secret");

        builder.Dispose();

        Assert.Equal("secret", new string(initial[..6]));
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void Dispose_AfterAFormattableThrows_ZeroesTheInitialSpan()
    {
        var initial = new char[8];
        var builder = new PooledTextBuilder(initial, isSensitive: true);
        var threw = false;

        try
        {
            builder.Append(new WritesThenThrows());
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.True(initial.AsSpan().ContainsAnyExcept('\0'));

        builder.Dispose();

        Assert.False(initial.AsSpan().ContainsAnyExcept('\0'));
    }

    [Fact]
    public void Dispose_Twice_DoesNothing()
    {
        var builder = new PooledTextBuilder(isSensitive: true, initialCapacity: 16);
        builder.Append("abc");

        builder.Dispose();
        builder.Dispose();

        Assert.Equal(0, builder.Length);
        Assert.Equal(0, builder.Capacity);
    }

    [Fact]
    public void Dispose_ThenAppend_RentsANewBuffer()
    {
        var builder = new PooledTextBuilder(isSensitive: false, initialCapacity: 16);
        builder.Append("abc");
        builder.Dispose();

        builder.Append("def");

        Assert.Equal("def", builder.ToString());
        builder.Dispose();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsTheText()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);
        builder.Append("abc");

        Assert.Equal("abc", builder.ToString());
        Assert.Equal("abc", new string(builder.WrittenSpan));
    }

    #endregion

    #region Length and Capacity Tests

    [Fact]
    public void Length_And_Capacity_ReportTheState()
    {
        Span<char> initial = stackalloc char[8];
        using var builder = new PooledTextBuilder(initial, isSensitive: false);
        Assert.Equal(0, builder.Length);
        Assert.Equal(8, builder.Capacity);

        builder.Append("abc");

        Assert.Equal(3, builder.Length);
        Assert.Equal(8, builder.Capacity);

        builder.Append(new string('d', 20));

        Assert.Equal(23, builder.Length);
        Assert.True(builder.Capacity >= 23);
    }

    #endregion
}
