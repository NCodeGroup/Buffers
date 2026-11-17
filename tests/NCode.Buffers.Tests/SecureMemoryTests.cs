// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace NCode.Buffers.Tests;

public class SecureMemoryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_AllocatesBufferOfRequestedLength()
    {
        using var memory = new SecureMemory<int>(null, 128);

        Assert.Equal(128, memory.Memory.Length);
    }

    [Fact]
    public void Constructor_AllocatesOnPinnedObjectHeap()
    {
        using var memory = new SecureMemory<byte>(null, 16);

        Assert.True(MemoryMarshal.TryGetArray(memory.Memory, out ArraySegment<byte> segment));
        Assert.NotNull(segment.Array);
        // The GC reports pinned-object-heap objects as its oldest generation; a new 16-byte array on the normal heap starts in generation 0.
        Assert.Equal(GC.MaxGeneration, GC.GetGeneration(segment.Array));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ZeroesMemory()
    {
        var memory = new SecureMemory<byte>(null, 16);
        memory.Memory.Span.Fill(0xAB);
        var contents = memory.Memory;

        memory.Dispose();

        Assert.All(contents.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void Dispose_WithPool_ReturnsMemoryToPool()
    {
        using var pool = new SecureMemoryPool<byte>();
        var memory = new SecureMemory<byte>(pool, 16);

        memory.Dispose();

        Assert.Same(SecureMemoryPoolTests.GetArray(memory), Assert.Single(pool.MemoryQueue));
    }

    [Fact]
    public void Dispose_CalledTwice_ReturnsMemoryToPoolOnce()
    {
        using var pool = new SecureMemoryPool<byte>();
        var memory = new SecureMemory<byte>(pool, 16);

        memory.Dispose();
        memory.Dispose();

        Assert.Single(pool.MemoryQueue);
    }

    #endregion
}
