#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Buffers;

namespace NCode.Buffers.Tests;

public class SecureMemoryPoolTests
{
    #region Constants Tests

    [Fact]
    public void DefaultHighPressureThreshold_Valid()
    {
        const double result = SecureMemoryPool<byte>.DefaultHighPressureThreshold;

        Assert.Equal(0.90, result);
    }

    [Fact]
    public void PageSize_Valid()
    {
        const int result = SecureMemoryPool<byte>.PageSize;

        Assert.Equal(4096, result);
    }

    #endregion

    #region Shared Instance Tests

    [Fact]
    public void Shared_ReturnsSameInstance()
    {
        var shared1 = SecureMemoryPool<byte>.Shared;
        var shared2 = SecureMemoryPool<byte>.Shared;

        Assert.Same(shared1, shared2);
    }

    [Fact]
    public void Shared_IsNotNull()
    {
        var shared = SecureMemoryPool<byte>.Shared;

        Assert.NotNull(shared);
    }

    [Fact]
    public void Shared_DifferentTypesReturnDifferentInstances()
    {
        var bytePool = SecureMemoryPool<byte>.Shared;
        var intPool = SecureMemoryPool<int>.Shared;

        Assert.NotNull(bytePool);
        Assert.NotNull(intPool);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void HighPressureThreshold_DefaultValue()
    {
        using var pool = new SecureMemoryPool<byte>();

        Assert.Equal(0.90, pool.HighPressureThreshold);
    }

    [Fact]
    public void HighPressureThreshold_CanBeSet()
    {
        using var pool = new SecureMemoryPool<byte>();

        pool.HighPressureThreshold = 0.80;

        Assert.Equal(0.80, pool.HighPressureThreshold);
    }

    [Fact]
    public void MaxBufferSize_ReturnsArrayMaxLength()
    {
        using var pool = new SecureMemoryPool<byte>();

        Assert.Equal(Array.MaxLength, pool.MaxBufferSize);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesEmptyPool()
    {
        using var pool = new SecureMemoryPool<byte>();

        Assert.Empty(pool.MemoryQueue);
        Assert.False(pool.IsDisposed);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ClearsMemoryQueue()
    {
        var pool = new SecureMemoryPool<byte>();

        pool.MemoryQueue.Enqueue(new SecureMemory<byte>(pool, 1024));
        Assert.NotEmpty(pool.MemoryQueue);

        pool.Dispose();

        Assert.True(pool.IsDisposed);
        Assert.Empty(pool.MemoryQueue);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var pool = new SecureMemoryPool<byte>();

        pool.Dispose();
        pool.Dispose();
        pool.Dispose();

        Assert.True(pool.IsDisposed);
    }

    [Fact]
    public void Dispose_SetsIsDisposedToTrue()
    {
        var pool = new SecureMemoryPool<byte>();
        Assert.False(pool.IsDisposed);

        pool.Dispose();

        Assert.True(pool.IsDisposed);
    }

    #endregion

    #region Rent Tests

    [Fact]
    public void Rent_WhenDisposed_ThrowsObjectDisposedException()
    {
        var pool = new SecureMemoryPool<byte>();
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Rent(0));
    }

    [Fact]
    public void Rent_SizeLessThanNegativeOne_ThrowsArgumentOutOfRangeException()
    {
        using var pool = new SecureMemoryPool<byte>();

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(-2));
    }

    [Fact]
    public void Rent_SizeZero_ReturnsEmptyMemorySingleton()
    {
        using var pool = new SecureMemoryPool<byte>();

        using var lease = pool.Rent(0);

        Assert.Same(EmptyMemory<byte>.Singleton, lease);
    }

    [Fact]
    public void Rent_SizeNegativeOne_ReturnsPageSizeBuffer()
    {
        using var pool = new SecureMemoryPool<byte>();

        using var lease = pool.Rent(-1);

        Assert.Equal(SecureMemoryPool<byte>.PageSize, lease.Memory.Length);
    }

    [Fact]
    public void Rent_SizeLessThanPage_ReturnsPageSizeBuffer()
    {
        using var pool = new SecureMemoryPool<byte>();

        const int requestedSize = SecureMemoryPool<byte>.PageSize - 1;
        using var lease = pool.Rent(requestedSize);

        Assert.Equal(SecureMemoryPool<byte>.PageSize, lease.Memory.Length);
    }

    [Fact]
    public void Rent_SizeEqualToPage_ReturnsPageSizeBuffer()
    {
        using var pool = new SecureMemoryPool<byte>();

        const int requestedSize = SecureMemoryPool<byte>.PageSize;
        using var lease = pool.Rent(requestedSize);

        Assert.Equal(SecureMemoryPool<byte>.PageSize, lease.Memory.Length);
    }

    [Fact]
    public void Rent_SizeMoreThanPage_ReturnsExactSizeBuffer()
    {
        using var pool = new SecureMemoryPool<byte>();

        const int requestedSize = SecureMemoryPool<byte>.PageSize + 1;
        using var lease = pool.Rent(requestedSize);

        Assert.Equal(requestedSize, lease.Memory.Length);
    }

    [Fact]
    public void Rent_LeaseIsReusedFromPool()
    {
        using var pool = new SecureMemoryPool<byte>();

        const int requestedSize = SecureMemoryPool<byte>.PageSize - 1;
        var lease1 = pool.Rent(requestedSize);
        lease1.Dispose();

        using var lease2 = pool.Rent(requestedSize);

        Assert.Same(lease1, lease2);
    }

    [Fact]
    public void Rent_LargeBuffer_IsNotPooled()
    {
        using var pool = new SecureMemoryPool<byte>();

        const int requestedSize = SecureMemoryPool<byte>.PageSize + 1;
        var lease1 = pool.Rent(requestedSize);
        lease1.Dispose();

        using var lease2 = pool.Rent(requestedSize);

        Assert.NotSame(lease1, lease2);
    }

    [Fact]
    public void Rent_MultipleLeases_AllValid()
    {
        using var pool = new SecureMemoryPool<byte>();

        using var lease1 = pool.Rent(100);
        using var lease2 = pool.Rent(200);
        using var lease3 = pool.Rent(300);

        Assert.Equal(SecureMemoryPool<byte>.PageSize, lease1.Memory.Length);
        Assert.Equal(SecureMemoryPool<byte>.PageSize, lease2.Memory.Length);
        Assert.Equal(SecureMemoryPool<byte>.PageSize, lease3.Memory.Length);
    }

    [Fact]
    public void Rent_ReturnsWritableMemory()
    {
        using var pool = new SecureMemoryPool<byte>();

        using var lease = pool.Rent(100);
        lease.Memory.Span[0] = 0xAA;
        lease.Memory.Span[99] = 0xBB;

        Assert.Equal(0xAA, lease.Memory.Span[0]);
        Assert.Equal(0xBB, lease.Memory.Span[99]);
    }

    #endregion

    #region Return Tests

    [Fact]
    public void Return_AddsToMemoryQueue()
    {
        using var pool = new SecureMemoryPool<byte>();
        Assert.Empty(pool.MemoryQueue);

        using var lease = new SecureMemory<byte>(null, 1024);

        pool.Return(lease);

        Assert.Single(pool.MemoryQueue, lease);
    }

    [Fact]
    public void Return_WhenDisposed_DoesNotAddToQueue()
    {
        var pool = new SecureMemoryPool<byte>();
        Assert.Empty(pool.MemoryQueue);

        pool.Dispose();

        using var lease = new SecureMemory<byte>(null, 1024);

        pool.Return(lease);

        Assert.Empty(pool.MemoryQueue);
    }

    [Fact]
    public void Return_MultipleLeases_AllAddedToQueue()
    {
        using var pool = new SecureMemoryPool<byte>();

        using var lease1 = new SecureMemory<byte>(null, 1024);
        using var lease2 = new SecureMemory<byte>(null, 1024);
        using var lease3 = new SecureMemory<byte>(null, 1024);

        pool.Return(lease1);
        pool.Return(lease2);
        pool.Return(lease3);

        Assert.Equal(3, pool.MemoryQueue.Count);
    }

    #endregion

    #region TrimMemory Tests

    [Fact]
    public void TrimMemory_WhenHighPressure_ClearsQueue()
    {
        using var pool = new SecureMemoryPool<byte>();

        pool.MemoryQueue.Enqueue(new SecureMemory<byte>(pool, 1024));
        Assert.NotEmpty(pool.MemoryQueue);

        pool.HighPressureThreshold = 0.0;
        pool.TrimMemory();

        Assert.Empty(pool.MemoryQueue);
    }

    [Fact]
    public void TrimMemory_ReturnsTrue()
    {
        using var pool = new SecureMemoryPool<byte>();

        var result = pool.TrimMemory();

        Assert.True(result);
    }

    [Fact]
    public void TrimMemory_WhenLowPressure_KeepsQueue()
    {
        using var pool = new SecureMemoryPool<byte>();

        pool.MemoryQueue.Enqueue(new SecureMemory<byte>(pool, 1024));
        Assert.NotEmpty(pool.MemoryQueue);

        pool.HighPressureThreshold = 1.1;
        pool.TrimMemory();

        Assert.NotEmpty(pool.MemoryQueue);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void GenericType_Int32_Works()
    {
        using var pool = new SecureMemoryPool<int>();

        using var lease = pool.Rent(100);

        Assert.True(lease.Memory.Length >= 100);
    }

    [Fact]
    public void GenericType_Long_Works()
    {
        using var pool = new SecureMemoryPool<long>();

        using var lease = pool.Rent(100);

        Assert.True(lease.Memory.Length >= 100);
    }

    [Fact]
    public void GenericType_Char_Works()
    {
        using var pool = new SecureMemoryPool<char>();

        using var lease = pool.Rent(100);

        Assert.True(lease.Memory.Length >= 100);
    }

    #endregion

    #region IMemoryPool Interface Tests

    [Fact]
    public void ImplementsMemoryPool()
    {
        using var pool = new SecureMemoryPool<byte>();

        Assert.IsAssignableFrom<MemoryPool<byte>>(pool);
    }

    [Fact]
    public void CanBeUsedAsMemoryPool()
    {
        MemoryPool<byte> pool = new SecureMemoryPool<byte>();

        using var lease = pool.Rent(100);

        Assert.NotNull(lease);
        Assert.True(lease.Memory.Length >= 100);

        pool.Dispose();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void RentDisposeRent_ReusesBuffer()
    {
        using var pool = new SecureMemoryPool<byte>();

        var lease1 = pool.Rent(100);
        var memory1 = lease1.Memory;
        lease1.Dispose();

        var lease2 = pool.Rent(100);
        var memory2 = lease2.Memory;

        Assert.Same(lease1, lease2);
        Assert.Equal(memory1.Length, memory2.Length);

        lease2.Dispose();
    }

    [Fact]
    public void RentedMemory_IsZeroedOnReturn()
    {
        using var pool = new SecureMemoryPool<byte>();

        var lease = pool.Rent(100);
        lease.Memory.Span.Fill(0xFF);
        Assert.True(lease.Memory.Span.ToArray().All(b => b == 0xFF));

        lease.Dispose();

        var lease2 = pool.Rent(100);
        Assert.True(lease2.Memory.Span.ToArray().All(b => b == 0));

        lease2.Dispose();
    }

    #endregion
}
