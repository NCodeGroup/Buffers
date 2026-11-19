// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Buffers.Tests;

public class BufferGrowthTests
{
    #region GetNewSize Tests

    [Theory]
    [InlineData(10, 100, 16, 200)]
    [InlineData(10, 4, 256, 256)]
    [InlineData(1001, 16, 16, 1001)]
    public void GetNewSize_ReturnsTheLargestOfRequiredMinimumAndDoubleTheCapacity(
        int required,
        int capacity,
        int minimum,
        int expected
    )
    {
        var newSize = BufferGrowth.GetNewSize(required, capacity, minimum);

        Assert.Equal(expected, newSize);
    }

    [Fact]
    public void GetNewSize_WhenDoubleTheCapacityPassesArrayMaxLength_ReturnsArrayMaxLength()
    {
        var capacity = Array.MaxLength / 2 + 1;
        Assert.True(2L * capacity > Array.MaxLength);

        var newSize = BufferGrowth.GetNewSize(capacity + 1, capacity, BufferGrowth.DefaultCapacity);

        Assert.Equal(Array.MaxLength, newSize);
    }

    #endregion
}
