// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NCode.Buffers;

internal class SecureMemory<T> : IMemoryOwner<T> where T : struct
{
    private int _disposed;
    private SecureMemoryPool<T>? Pool { get; }

    /// <inheritdoc />
    public Memory<T> Memory { get; }

    internal SecureMemory(SecureMemoryPool<T>? pool, int bufferSize)
    {
        Pool = pool;

        // https://devblogs.microsoft.com/dotnet/internals-of-the-poh/
        var pinnedArray = GC.AllocateUninitializedArray<T>(bufferSize, pinned: true);
        Memory = MemoryMarshal.CreateFromPinnedArray(pinnedArray, 0, bufferSize);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        CryptographicOperations.ZeroMemory(Memory.Span);

        Return();
    }

    internal virtual void Return()
    {
        Pool?.Return(this);
    }
}
