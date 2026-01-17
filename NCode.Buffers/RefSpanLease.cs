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

using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A ref struct that holds a leased <see cref="ReadOnlySpan{T}"/> and manages the lifetime of its underlying owner.
/// When disposed, the owner is also disposed, releasing the leased memory back to its source (e.g., a memory pool).
/// </summary>
/// <typeparam name="T">The type of elements in the span.</typeparam>
/// <param name="owner">The <see cref="IDisposable"/> owner that manages the underlying memory,
/// or <see langword="null"/> if no owner is associated (e.g., when wrapping stack-allocated or static memory).</param>
/// <param name="span">The <see cref="ReadOnlySpan{T}"/> representing the leased memory.</param>
/// <remarks>
/// <para>
/// This type provides a convenient way to pair a <see cref="ReadOnlySpan{T}"/> with its lifetime management.
/// It is particularly useful when working with pooled memory where the span must be returned to the pool after use.
/// </para>
/// <para>
/// Since this is a <see langword="ref struct"/>, it can only be used on the stack and cannot be stored
/// in fields of reference types, boxed, or used in async methods.
/// </para>
/// <para>
/// This struct is safe to dispose multiple times; subsequent disposals after the first are no-ops.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using with pooled memory
/// using var lease = new RefSpanLease&lt;byte&gt;(memoryOwner, memoryOwner.Memory.Span);
/// ProcessData(lease.Span);
/// // Memory is returned to pool when lease is disposed
///
/// // Using with non-owned memory (no disposal needed)
/// var lease = new RefSpanLease&lt;byte&gt;(null, stackalloc byte[256]);
/// ProcessData(lease.Span);
/// </code>
/// </example>
[PublicAPI]
public readonly ref struct RefSpanLease<T>(IDisposable? owner, ReadOnlySpan<T> span) : IDisposable
{
    private IDisposable? Owner { get; } = owner;

    /// <summary>
    /// Gets the leased <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <value>
    /// A <see cref="ReadOnlySpan{T}"/> representing the leased memory region.
    /// </value>
    /// <remarks>
    /// The span remains valid only while the <see cref="RefSpanLease{T}"/> has not been disposed.
    /// Accessing this property after disposal may result in undefined behavior if the underlying
    /// memory has been returned to a pool or otherwise invalidated.
    /// </remarks>
    public ReadOnlySpan<T> Span { get; } = span;

    /// <summary>
    /// Disposes the underlying owner, releasing the leased memory back to its source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the owner is <see langword="null"/>, this method does nothing.
    /// </para>
    /// <para>
    /// After calling this method, the <see cref="Span"/> property should no longer be accessed,
    /// as the underlying memory may have been returned to a pool or otherwise invalidated.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        Owner?.Dispose();
    }
}
