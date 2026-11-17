// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NCode.Buffers;

internal class SecureMemory<T> : IMemoryOwner<T>
    where T : unmanaged
{
    private int _disposed;
    private SecureMemoryPool<T>? Pool { get; }
    private T[] PinnedArray { get; }

    /// <inheritdoc />
    public Memory<T> Memory { get; }

    internal SecureMemory(SecureMemoryPool<T>? pool, int bufferSize)
        // https://devblogs.microsoft.com/dotnet/internals-of-the-poh/
        : this(pool, GC.AllocateUninitializedArray<T>(bufferSize, pinned: true)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureMemory{T}"/> class that wraps an existing array.
    /// </summary>
    /// <param name="pool">The pool that receives the array when this owner is disposed, or <see langword="null"/> for none.</param>
    /// <param name="pinnedArray">The array to wrap. It must be pinned, for example allocated on the pinned object heap.</param>
    internal SecureMemory(SecureMemoryPool<T>? pool, T[] pinnedArray)
    {
        Pool = pool;
        PinnedArray = pinnedArray;
        Memory = MemoryMarshal.CreateFromPinnedArray(pinnedArray, 0, pinnedArray.Length);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        CryptographicOperations.ZeroMemory(Memory.Span);

        Pool?.Return(PinnedArray);
    }
}
