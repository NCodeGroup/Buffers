#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
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
/// The pool automatically trims cached memory when system memory pressure exceeds <see cref="HighPressureThreshold"/>
/// during Gen2 garbage collections.
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
public class SecureMemoryPool<T> : MemoryPool<T> where T : struct
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
    /// </remarks>
    public new static SecureMemoryPool<T> Shared { get; } = new();

    private int _disposed;

    /// <summary>
    /// Gets a value indicating whether this pool has been disposed.
    /// </summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Gets the queue of available memory buffers for reuse.
    /// </summary>
    internal ConcurrentQueue<SecureMemory<T>> MemoryQueue { get; } = new();

    /// <summary>
    /// Gets or sets the high pressure threshold when the memory pool should trim cached memory.
    /// </summary>
    /// <value>
    /// A value between 0.0 and 1.0 representing the percentage of the high memory load threshold.
    /// The default value is <see cref="DefaultHighPressureThreshold"/>.
    /// </value>
    /// <remarks>
    /// When the system's memory load exceeds this threshold multiplied by the high memory load threshold,
    /// the pool will clear all cached buffers during the next Gen2 garbage collection.
    /// </remarks>
    public double HighPressureThreshold { get; set; } = DefaultHighPressureThreshold;

    /// <inheritdoc />
    /// <value>Returns <see cref="Array.MaxLength"/>, the maximum length of an array.</value>
    public override int MaxBufferSize => Array.MaxLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureMemoryPool{T}"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor registers a callback for Gen2 garbage collections to automatically
    /// trim cached memory when system memory pressure is high.
    /// </remarks>
    public SecureMemoryPool()
    {
        Gen2GcCallback.Register(state => ((SecureMemoryPool<T>)state).TrimMemory(), this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Disposing the pool clears all cached memory buffers. Any buffers currently rented
    /// will still be valid but will not be returned to the pool when disposed.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
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
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minBufferSize"/> is less than -1.</exception>
    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(minBufferSize, -1);

        if (minBufferSize == 0)
        {
            return EmptyMemory<T>.Singleton;
        }

        var byteCount = minBufferSize == -1 ? PageSize : minBufferSize * Marshal.SizeOf<T>();
        if (byteCount <= PageSize)
        {
            return MemoryQueue.TryDequeue(out var memory) ? memory : new SecureMemory<T>(this, PageSize);
        }

        // non-pooled
        return new SecureMemory<T>(null, minBufferSize);
    }

    /// <summary>
    /// Returns a memory buffer to the pool for reuse.
    /// </summary>
    /// <param name="memory">The memory buffer to return to the pool.</param>
    /// <remarks>
    /// If the pool has been disposed, the memory is not returned to the pool.
    /// The memory should already be zeroed before calling this method.
    /// </remarks>
    internal virtual void Return(SecureMemory<T> memory)
    {
        if (IsDisposed)
            return;

        MemoryQueue.Enqueue(memory);
    }

    /// <summary>
    /// Trims cached memory when system memory pressure is high.
    /// </summary>
    /// <returns>Always returns <see langword="true"/> to keep the callback registered.</returns>
    /// <remarks>
    /// This method is called automatically during Gen2 garbage collections.
    /// If the system's memory load exceeds <see cref="HighPressureThreshold"/> multiplied by
    /// the high memory load threshold, all cached buffers are cleared.
    /// </remarks>
    internal bool TrimMemory()
    {
        var memoryInfo = GC.GetGCMemoryInfo();

        var isPressureHigh =
            memoryInfo.MemoryLoadBytes >=
            memoryInfo.HighMemoryLoadThresholdBytes * HighPressureThreshold;

        if (isPressureHigh)
        {
            MemoryQueue.Clear();
        }

        return true;
    }
}
