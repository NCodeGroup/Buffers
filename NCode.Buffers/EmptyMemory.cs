// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides an <see cref="IMemoryOwner{T}"/> implementation that represents an empty memory block.
/// </summary>
/// <typeparam name="T">The type of elements to store in memory.</typeparam>
/// <remarks>
/// <para>
/// This class is useful when an <see cref="IMemoryOwner{T}"/> is required but no actual memory allocation is needed.
/// It provides a lightweight, allocation-free alternative to creating an empty buffer from a memory pool.
/// </para>
/// <para>
/// The <see cref="Singleton"/> property provides a shared instance that can be reused throughout the application,
/// avoiding unnecessary allocations. The <see cref="Dispose"/> method is a no-op since there are no resources to release.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Use as a default/empty value
/// IMemoryOwner&lt;byte&gt; buffer = EmptyMemory&lt;byte&gt;.Singleton;
///
/// // Safe to call Dispose (no-op)
/// buffer.Dispose();
/// </code>
/// </example>
[PublicAPI]
public sealed class EmptyMemory<T> : IMemoryOwner<T>
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptyMemory{T}"/>.
    /// </summary>
    /// <value>
    /// A shared, reusable instance of <see cref="EmptyMemory{T}"/>.
    /// </value>
    /// <remarks>
    /// Use this property to avoid allocating new instances when an empty <see cref="IMemoryOwner{T}"/> is needed.
    /// The singleton is safe to use across multiple threads and can be disposed multiple times without side effects.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Shipped public API; the shared per-type singleton is intentional.")]
    public static EmptyMemory<T> Singleton { get; } = new();

    /// <inheritdoc />
    /// <remarks>
    /// Always returns <see cref="Memory{T}.Empty"/>, a zero-length memory block.
    /// </remarks>
    public Memory<T> Memory { get; } = Memory<T>.Empty;

    /// <inheritdoc />
    /// <remarks>
    /// This method performs no action since there are no resources to release.
    /// It is safe to call multiple times.
    /// </remarks>
    public void Dispose()
    {
        // nothing
    }
}
