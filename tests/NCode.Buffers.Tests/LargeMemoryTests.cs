// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Buffers.Tests;

// Each test allocates 2 GiB, so the class runs alone and only on request. It covers four types that share that need, so it is named for it rather than for one type under test.
[CollectionDefinition(nameof(LargeMemoryTests), DisableParallelization = true)]
[Collection(nameof(LargeMemoryTests))]
public class LargeMemoryTests
{
    // 536,870,912 elements of 4 bytes are 2,147,483,648 bytes, one more than int.MaxValue.
    private const int IntsPastInt32MaxValueBytes = 536_870_912;

    #region ZeroMemory Tests

    [LargeMemoryFact]
    public void ZeroMemory_WhenTheSpanHoldsMoreThanInt32MaxValueBytes_SetsEveryElementToZero()
    {
        var values = GC.AllocateUninitializedArray<int>(IntsPastInt32MaxValueBytes);
        values.AsSpan().Fill(-1);

        CryptographicOperations.ZeroMemory<int>(values);

        Assert.False(values.AsSpan().ContainsAnyExcept(0));
    }

    #endregion

    #region FixedTimeEquals Tests

    [LargeMemoryFact]
    public void FixedTimeEquals_WhenTheSpansHoldMoreThanInt32MaxValueBytes_ComparesEveryElement()
    {
        // One element more than a span needs, so that two overlapping spans differ only in their last element.
        var values = new int[IntsPastInt32MaxValueBytes + 1];
        values[^1] = 1;
        var first = values.AsSpan(0, IntsPastInt32MaxValueBytes);
        var shifted = values.AsSpan(1, IntsPastInt32MaxValueBytes);

        Assert.True(CryptographicOperations.FixedTimeEquals<int>(first, first));
        Assert.False(CryptographicOperations.FixedTimeEquals<int>(first, shifted));
    }

    #endregion

    #region Rent Tests

    [LargeMemoryFact]
    public void Rent_WhenTheBufferHoldsMoreThanInt32MaxValueBytes_DisposeZeroesIt()
    {
        using var pool = new SecureMemoryPool<int>();
        var owner = pool.Rent(IntsPastInt32MaxValueBytes);
        var memory = owner.Memory;
        memory.Span.Fill(-1);

        owner.Dispose();

        Assert.False(memory.Span.ContainsAnyExcept(0));
    }

    #endregion

    #region CreatePinnedArray Tests

    [LargeMemoryFact]
    public void CreatePinnedArray_WhenTheArrayHoldsMoreThanInt32MaxValueBytes_DisposeZeroesIt()
    {
        var lifetime = BufferFactory.CreatePinnedArray<int>(IntsPastInt32MaxValueBytes);
        var values = lifetime.PinnedArray;
        values.AsSpan().Fill(-1);

        lifetime.Dispose();

        Assert.False(values.AsSpan().ContainsAnyExcept(0));
    }

    #endregion

    #region GetSecureLifetime Tests

    [LargeMemoryFact]
    public void GetSecureLifetime_WhenTheSpanHoldsMoreThanInt32MaxValueBytes_DisposeZeroesIt()
    {
        var values = GC.AllocateUninitializedArray<int>(IntsPastInt32MaxValueBytes);
        values.AsSpan().Fill(-1);

        values.AsSpan().GetSecureLifetime().Dispose();

        Assert.False(values.AsSpan().ContainsAnyExcept(0));
    }

    #endregion

    // Runs a test only when NCODE_BUFFERS_LARGE_MEMORY_TESTS is 1, on a machine with at least 6 GiB of free memory.
    private sealed class LargeMemoryFactAttribute : FactAttribute
    {
        private const string VariableName = "NCODE_BUFFERS_LARGE_MEMORY_TESTS";

        public LargeMemoryFactAttribute()
        {
            if (Environment.GetEnvironmentVariable(VariableName) != "1")
            {
                Skip =
                    $"Allocates 2 GiB. Set {VariableName}=1 to run it, on a machine with at least 6 GiB of free memory.";
            }
        }
    }
}
