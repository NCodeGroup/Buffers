// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Buffers.Tests;

public class CryptographicOperationsExtensionsTests
{
    #region ZeroMemory Tests

    [Fact]
    public void ZeroMemory_Int32Span_SetsEveryElementToZero()
    {
        int[] values = [1, -1, int.MaxValue, int.MinValue];

        CryptographicOperations.ZeroMemory<int>(values);

        Assert.All(values, value => Assert.Equal(0, value));
    }

    [Fact]
    public void ZeroMemory_Slice_LeavesElementsOutsideTheSliceUnchanged()
    {
        long[] values = [1, 2, 3, 4];

        CryptographicOperations.ZeroMemory<long>(values.AsSpan(1, 2));

        long[] expected = [1, 0, 0, 4];
        Assert.Equal(expected, values);
    }

    [Fact]
    public void ZeroMemory_StructSpan_SetsEveryElementToDefault()
    {
        Guid[] values = [Guid.NewGuid(), Guid.NewGuid()];

        CryptographicOperations.ZeroMemory<Guid>(values);

        Assert.All(values, value => Assert.Equal(Guid.Empty, value));
    }

    [Fact]
    public void ZeroMemory_EmptySpan_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            CryptographicOperations.ZeroMemory<int>(Span<int>.Empty)
        );

        Assert.Null(exception);
    }

    #endregion

    #region ZeroMemoryOrClear Tests

    [Fact]
    public void ZeroMemoryOrClear_WithSeveralChunks_ZeroesEveryElement()
    {
        long[] buffer = [-1, -1, -1, -1, -1, -1];
        var firstFive = buffer.AsSpan(0, 5);

        CryptographicOperationsExtensions.ZeroMemoryOrClear(firstFive, maxElementsPerChunk: 2);

        Assert.Equal<long>([0, 0, 0, 0, 0, -1], buffer);
    }

    [Fact]
    public void ZeroMemoryOrClear_WithEmptySpan_DoesNothing()
    {
        long[] buffer = [-1];
        var empty = buffer.AsSpan(0, 0);

        CryptographicOperationsExtensions.ZeroMemoryOrClear(empty, maxElementsPerChunk: 2);

        Assert.Equal<long>([-1], buffer);
    }

    #endregion

    #region FixedTimeEquals Tests

    [Fact]
    public void FixedTimeEquals_SameContents_ReturnsTrue()
    {
        int[] left = [1, 2, 3];
        int[] right = [1, 2, 3];

        var result = CryptographicOperations.FixedTimeEquals<int>(left, right);

        Assert.True(result);
    }

    [Fact]
    public void FixedTimeEquals_DifferentElement_ReturnsFalse()
    {
        int[] left = [1, 2, 3];
        int[] right = [1, 2, 4];

        var result = CryptographicOperations.FixedTimeEquals<int>(left, right);

        Assert.False(result);
    }

    // Elements that differ only in their most significant byte prove every byte of each element is compared.
    [Fact]
    public void FixedTimeEquals_DifferenceOnlyInHighByte_ReturnsFalse()
    {
        int[] left = [0x01000000];
        int[] right = [0];

        var result = CryptographicOperations.FixedTimeEquals<int>(left, right);

        Assert.False(result);
    }

    [Fact]
    public void FixedTimeEquals_DifferentLengths_ReturnsFalse()
    {
        int[] left = [1, 2];
        int[] right = [1, 2, 3];

        var result = CryptographicOperations.FixedTimeEquals<int>(left, right);

        Assert.False(result);
    }

    [Fact]
    public void FixedTimeEquals_BothEmpty_ReturnsTrue()
    {
        var result = CryptographicOperations.FixedTimeEquals<int>(
            ReadOnlySpan<int>.Empty,
            ReadOnlySpan<int>.Empty
        );

        Assert.True(result);
    }

    [Fact]
    public void FixedTimeEquals_EqualStructs_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        Guid[] left = [id];
        Guid[] right = [new Guid(id.ToByteArray())];

        var result = CryptographicOperations.FixedTimeEquals<Guid>(left, right);

        Assert.True(result);
    }

    #endregion
}
