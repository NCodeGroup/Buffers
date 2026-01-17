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

using System.Buffers;
using System.Diagnostics;
using JetBrains.Annotations;
using Nerdbank.Streams;

namespace NCode.Buffers;

/// <summary>
/// Provides extension methods for <see cref="Sequence{T}"/> to enable secure memory operations
/// and efficient conversion to contiguous spans.
/// </summary>
/// <remarks>
/// <para>
/// These extensions are particularly useful when working with <see cref="Sequence{T}"/> buffers
/// that may span multiple segments, but you need a contiguous <see cref="ReadOnlySpan{T}"/> for
/// operations that don't support segmented data (e.g., cryptographic APIs, P/Invoke calls).
/// </para>
/// <para>
/// The methods support both sensitive and non-sensitive data modes. When <c>isSensitive</c> is <c>true</c>,
/// buffers are rented from <see cref="SecureMemoryPool{T}"/> and securely zeroed upon disposal.
/// </para>
/// </remarks>
[PublicAPI]
public static class SequenceExtensions
{
    /// <typeparam name="T">The type of elements in the sequence. Must be an unmanaged value type.</typeparam>
    extension<T>(Sequence<T> buffer) where T : struct
    {
        /// <summary>
        /// Gets a <see cref="RefSpanLease{T}"/> that provides access to the underlying data as a contiguous <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="isSensitive">
        /// When <see langword="true"/>, if a buffer must be rented, it will be allocated from <see cref="SecureMemoryPool{T}"/>
        /// and securely zeroed when disposed. When <see langword="false"/>, the buffer is rented from <see cref="MemoryPool{T}.Shared"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RefSpanLease{T}"/> that provides access to the sequence data as a contiguous <see cref="ReadOnlySpan{T}"/>.
        /// The caller must dispose the lease to release the underlying resources.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <strong>Single-segment optimization:</strong> When the sequence consists of a single segment,
        /// the span is returned directly with the sequence as the owner, meaning no additional memory allocation occurs.
        /// The caller retains ownership of the original sequence, which must remain undisposed while using the span.
        /// </para>
        /// <para>
        /// <strong>Multi-segment handling:</strong> When the sequence spans multiple segments, a buffer is rented
        /// and the data is copied into it. The original sequence remains valid and unchanged.
        /// </para>
        /// <para>
        /// This method does <strong>not</strong> dispose or consume the original sequence. Use
        /// <see cref="ConsumeAsContiguousSpan"/> if you want to transfer ownership.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using var writer = BufferFactory.CreatePooledBufferWriter&lt;byte&gt;(isSensitive: true);
        /// // ... write data to writer ...
        ///
        /// using var lease = writer.GetSpanLease(isSensitive: true);
        /// ProcessData(lease.Span);
        /// // Lease is disposed, rented buffer (if any) is securely zeroed
        /// // Original writer is still valid
        /// </code>
        /// </example>
        [PublicAPI]
        public RefSpanLease<T> GetSpanLease(bool isSensitive)
        {
            var sequence = buffer.AsReadOnlySequence;
            if (sequence.IsSingleSegment)
            {
                return new RefSpanLease<T>(buffer, sequence.First.Span);
            }

            Debug.Assert(buffer.Length <= int.MaxValue, "Sequence length exceeds int.MaxValue.");
            var owner = BufferFactory.Rent((int)buffer.Length, isSensitive, out Span<T> destination);
            try
            {
                sequence.CopyTo(destination);
                return new RefSpanLease<T>(owner, destination);
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Consumes the sequence and returns an <see cref="IDisposable"/> owner along with a contiguous <see cref="ReadOnlySpan{T}"/> of the data.
        /// This method transfers ownership of the underlying buffer to the caller and disposes the original sequence.
        /// </summary>
        /// <param name="isSensitive">
        /// When <see langword="true"/>, if a buffer must be rented, it will be allocated from <see cref="SecureMemoryPool{T}"/>
        /// and securely zeroed when disposed. When <see langword="false"/>, the buffer is rented from <see cref="MemoryPool{T}.Shared"/>.
        /// </param>
        /// <param name="span">
        /// When this method returns, contains a <see cref="ReadOnlySpan{T}"/> representing the contiguous data from the sequence.
        /// This span is valid only while the returned owner has not been disposed.
        /// </param>
        /// <returns>
        /// An <see cref="IDisposable"/> that owns the underlying buffer. The caller must dispose this owner to release resources.
        /// <list type="bullet">
        /// <item><description>For single-segment sequences: returns the original <see cref="Sequence{T}"/> as the owner.</description></item>
        /// <item><description>For multi-segment sequences: returns an <see cref="IMemoryOwner{T}"/> from the memory pool.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// <strong>Ownership transfer:</strong> This method assumes lifecycle ownership of the sequence data.
        /// After calling this method, the original <see cref="Sequence{T}"/> is disposed (for multi-segment cases)
        /// and should no longer be used. The caller becomes responsible for disposing the returned owner.
        /// </para>
        /// <para>
        /// <strong>Single-segment optimization:</strong> When the sequence consists of a single segment,
        /// the span is returned directly and the sequence itself is returned as the owner.
        /// No data copying or additional allocation occurs.
        /// </para>
        /// <para>
        /// <strong>Multi-segment handling:</strong> When the sequence spans multiple segments, a buffer is rented,
        /// the data is copied into it, and the original sequence is disposed immediately.
        /// </para>
        /// <para>
        /// <strong>Exception safety:</strong> If an exception occurs during the copy operation,
        /// the rented buffer is disposed before re-throwing the exception.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using var writer = BufferFactory.CreatePooledBufferWriter&lt;byte&gt;(isSensitive: true);
        /// // ... write data to writer ...
        ///
        /// // Transfer ownership - writer is consumed and should not be used after this
        /// using var owner = writer.ConsumeAsContiguousSpan(isSensitive: true, out var span);
        /// ProcessData(span);
        /// // Owner is disposed, all memory is securely zeroed
        /// </code>
        /// </example>
        [PublicAPI]
        public IDisposable ConsumeAsContiguousSpan(bool isSensitive, out ReadOnlySpan<T> span)
        {
            var sequence = buffer.AsReadOnlySequence;
            if (sequence.IsSingleSegment)
            {
                span = sequence.First.Span;
                return buffer;
            }

            Debug.Assert(buffer.Length <= int.MaxValue, "Sequence length exceeds int.MaxValue.");
            var owner = BufferFactory.Rent((int)buffer.Length, isSensitive, out Span<T> destination);
            try
            {
                sequence.CopyTo(destination);
                span = destination;
                return owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }
}
