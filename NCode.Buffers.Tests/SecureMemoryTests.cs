#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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

        Assert.Single(pool.MemoryQueue, memory);
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
