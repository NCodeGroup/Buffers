// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides a resource pool that enables reusing instances of memory buffers
/// that are pinned during their lifetime and securely zeroed when returned.
/// </summary>
/// <typeparam name="T">The type of elements in the memory buffers. Must be an unmanaged value type.</typeparam>
/// <remarks>
/// <para>
/// This memory pool is designed for scenarios where sensitive data (such as cryptographic keys, passwords,
/// or other secrets) needs to be stored in memory and securely cleared when no longer needed.
/// </para>
/// <para>
/// Memory buffers rented from this pool are pinned to prevent the garbage collector from moving them,
/// and are securely zeroed using <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/>
/// when returned to the pool.
/// </para>
/// <para>
/// The pool maintains a cache of fixed-size buffers (default <see cref="PageSize"/> bytes) that can be reused.
/// Requests for larger buffers are fulfilled with non-pooled allocations that are still securely zeroed on return.
/// </para>
/// <para>
/// After a garbage collection, on the finalizer thread, the pool drops every cached buffer when
/// <see cref="GCMemoryInfo.MemoryLoadBytes"/> is at or above <see cref="HighPressureThreshold"/> times
/// <see cref="GCMemoryInfo.HighMemoryLoadThresholdBytes"/>. The check runs after every generation 2 collection; the first
/// one or two checks after the pool is created can also follow a generation 0 or generation 1 collection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var owner = SecureMemoryPool&lt;byte&gt;.Shared.Rent(256);
/// var memory = owner.Memory;
/// // Use memory for sensitive operations
/// // Memory is automatically zeroed when owner is disposed
/// </code>
/// </example>
[PublicAPI]
public class SecureMemoryPool<T> : MemoryPool<T>
    where T : unmanaged
{
    /// <summary>
    /// The default high pressure threshold when the memory pool should trim cached memory.
    /// </summary>
    /// <value>The default value is 0.90 (90% of the high memory load threshold).</value>
    public const double DefaultHighPressureThreshold = 0.90;

    /// <summary>
    /// The fixed size of pooled memory buffers in bytes.
    /// </summary>
    /// <value>4096 bytes, which matches the page size on most operating systems.</value>
    /// <remarks>
    /// Buffers requested at or below this size are served from the pool.
    /// Larger requests receive non-pooled allocations.
    /// </remarks>
    public const int PageSize = 4096;

    /// <summary>
    /// Gets a singleton instance of <see cref="SecureMemoryPool{T}"/>.
    /// </summary>
    /// <value>A shared, thread-safe instance of the secure memory pool.</value>
    /// <remarks>
    /// Use this property to access a global instance of the pool without creating your own.
    /// The shared instance is suitable for most applications.
    /// Its <c>Dispose</c> does nothing, as the <c>Dispose</c> of <c>MemoryPool&lt;T&gt;.Shared</c> does:
    /// the instance serves every caller in the process, so no caller can end it for the others.
    /// Dispose only a pool you create.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Shipped public API; renaming or moving it would be a breaking change."
    )]
    public static new SecureMemoryPool<T> Shared { get; } = new() { IsShared = true };

    // The number of elements in one pooled buffer of PageSize bytes, and at least one for an element larger than a page.
    private static readonly int ElementsPerPage = Math.Max(1, PageSize / Unsafe.SizeOf<T>());

    private int _disposed;

    // Shared serves every caller in the process, so its Dispose does nothing, as MemoryPool<T>.Shared's does.
    private bool IsShared { get; init; }

    /// <summary>
    /// Gets a value indicating whether this pool has been disposed.
    /// </summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Gets the queue of zeroed pinned arrays available for reuse.
    /// </summary>
    // The pool keeps arrays, never owners: every rental gets a new owner, so an earlier renter's second Dispose can't zero or return a buffer someone rents later.
    internal ConcurrentQueue<T[]> MemoryQueue { get; } = new();

    /// <summary>
    /// Gets or sets the high pressure threshold when the memory pool should trim cached memory.
    /// </summary>
    /// <value>
    /// The fraction of <see cref="GCMemoryInfo.HighMemoryLoadThresholdBytes"/> at which the pool drops its cache,
    /// usually between 0.0 and 1.0. The default value is <see cref="DefaultHighPressureThreshold"/>.
    /// </value>
    /// <remarks>
    /// After a garbage collection, on the finalizer thread, the pool drops all cached buffers when
    /// <see cref="GCMemoryInfo.MemoryLoadBytes"/> is at or above this value multiplied by
    /// <see cref="GCMemoryInfo.HighMemoryLoadThresholdBytes"/>. The check runs after the collection, not during it: after
    /// every generation 2 collection, and the first one or two checks after the pool is created can also follow a
    /// generation 0 or generation 1 collection.
    /// </remarks>
    public double HighPressureThreshold { get; set; } = DefaultHighPressureThreshold;

    /// <inheritdoc />
    /// <value>Returns <see cref="Array.MaxLength"/>, the maximum length of an array.</value>
    /// <remarks>
    /// A buffer of up to this many elements is zeroed when its owner is disposed, whatever the element type.
    /// Whether the runtime can allocate it depends on the memory available.
    /// </remarks>
    public override int MaxBufferSize => Array.MaxLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureMemoryPool{T}"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor registers a callback that runs on the finalizer thread after garbage collections, as
    /// <see cref="HighPressureThreshold"/> describes, and drops the cached buffers when the memory load is high.
    /// </remarks>
    public SecureMemoryPool()
    {
        Gen2GcCallback.Register(state => ((SecureMemoryPool<T>)state).TrimMemory(), this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Disposing a pool you create clears its cached buffers and makes <see cref="Rent"/> throw
    /// <see cref="ObjectDisposedException"/>. Buffers currently rented stay valid and are zeroed when their owners are
    /// disposed, but are not returned to the pool. Disposing <c>Shared</c> does nothing.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (IsShared)
            return;

        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0 || !disposing)
            return;

        MemoryQueue.Clear();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// If <paramref name="minBufferSize"/> is 0, returns <see cref="EmptyMemory{T}.Singleton"/>.
    /// </para>
    /// <para>
    /// If <paramref name="minBufferSize"/> is -1 or results in a byte count at or below <see cref="PageSize"/>,
    /// a pooled buffer is returned if available, otherwise a new pooled buffer is allocated.
    /// </para>
    /// <para>
    /// If the requested size exceeds <see cref="PageSize"/> bytes, a non-pooled buffer is allocated.
    /// Non-pooled buffers are still securely zeroed when disposed but are not returned to the pool.
    /// </para>
    /// <para>
    /// Each call returns an owner of its own. Its first <c>Dispose</c> zeroes the buffer and returns a pooled buffer to the pool;
    /// a later <c>Dispose</c> of the same owner does nothing, so it never affects a later renter of the buffer.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The pool has been disposed. <c>Shared</c> never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minBufferSize"/> is less than -1.</exception>
    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(minBufferSize, -1);

        if (minBufferSize == 0)
        {
            return EmptyMemory<T>.Singleton;
        }

        var byteCount = minBufferSize == -1 ? PageSize : (long)minBufferSize * Unsafe.SizeOf<T>();
        if (byteCount <= PageSize)
        {
            return MemoryQueue.TryDequeue(out var pinnedArray)
                ? new SecureMemory<T>(this, pinnedArray)
                : new SecureMemory<T>(this, ElementsPerPage);
        }

        // non-pooled
        return new SecureMemory<T>(null, minBufferSize);
    }

    /// <summary>
    /// Returns a memory buffer to the pool for reuse.
    /// </summary>
    /// <param name="pinnedArray">The zeroed pinned array of a disposed owner.</param>
    /// <remarks>
    /// If the pool has been disposed, the memory is not returned to the pool.
    /// The memory should already be zeroed before calling this method.
    /// </remarks>
    internal virtual void Return(T[] pinnedArray)
    {
        if (IsDisposed)
            return;

        MemoryQueue.Enqueue(pinnedArray);
    }

    /// <summary>
    /// Trims cached memory when system memory pressure is high.
    /// </summary>
    /// <returns>Always returns <see langword="true"/> to keep the callback registered.</returns>
    /// <remarks>
    /// The callback the constructor registers calls this method on the finalizer thread after a garbage collection:
    /// after every generation 2 collection, and the first one or two times after the pool is created also after a
    /// generation 0 or generation 1 collection. When <see cref="GCMemoryInfo.MemoryLoadBytes"/> is at or above
    /// <see cref="HighPressureThreshold"/> multiplied by <see cref="GCMemoryInfo.HighMemoryLoadThresholdBytes"/>, all
    /// cached buffers are dropped.
    /// </remarks>
    internal bool TrimMemory()
    {
        var memoryInfo = GC.GetGCMemoryInfo();

        var isPressureHigh =
            memoryInfo.MemoryLoadBytes
            >= memoryInfo.HighMemoryLoadThresholdBytes * HighPressureThreshold;

        if (isPressureHigh)
        {
            MemoryQueue.Clear();
        }

        return true;
    }
}
