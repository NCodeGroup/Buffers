// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides extension methods for <see cref="CryptographicOperations"/> that enable secure memory operations
/// on generic value type spans, not just byte spans.
/// </summary>
/// <remarks>
/// <para>
/// The standard <see cref="CryptographicOperations"/> class only provides methods that operate on byte spans.
/// This extension class enables the same security-critical operations on spans of any unmanaged value type
/// by viewing them as byte spans with <see cref="MemoryMarshal.AsBytes{T}(Span{T})"/>, in chunks of at most
/// <see cref="int.MaxValue"/> bytes, so a span of any length is handled.
/// </para>
/// <para>
/// These methods are essential for secure handling of sensitive data such as cryptographic keys, passwords,
/// and other secrets that may be stored in typed arrays or spans.
/// </para>
/// </remarks>
[PublicAPI]
public static class CryptographicOperationsExtensions
{
    /// <summary>
    /// Zeroes the span with <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>, or, for a
    /// <typeparamref name="T"/> that is or contains a reference, sets it to default with <see cref="Span{T}.Clear"/>.
    /// </summary>
    /// <typeparam name="T">The type of the span elements.</typeparam>
    /// <param name="buffer">The span to zero.</param>
    internal static void ZeroMemoryOrClear<T>(Span<T> buffer)
    {
        var maxElementsPerChunk = int.MaxValue / Unsafe.SizeOf<T>();
        ZeroMemoryOrClear(buffer, maxElementsPerChunk);
    }

    /// <summary>
    /// Zeroes the span as <see cref="ZeroMemoryOrClear{T}(Span{T})"/> does, at most
    /// <paramref name="maxElementsPerChunk"/> elements per call to
    /// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>, so no call covers more than
    /// <see cref="int.MaxValue"/> bytes.
    /// </summary>
    /// <typeparam name="T">The type of the span elements.</typeparam>
    /// <param name="buffer">The span to zero.</param>
    /// <param name="maxElementsPerChunk">The most elements one call zeroes; at least 1.</param>
    internal static void ZeroMemoryOrClear<T>(Span<T> buffer, int maxElementsPerChunk)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            buffer.Clear();
            return;
        }

        var elementSize = Unsafe.SizeOf<T>();
        var remaining = buffer;
        while (!remaining.IsEmpty)
        {
            var count = Math.Min(remaining.Length, maxElementsPerChunk);
            ref var first = ref MemoryMarshal.GetReference(remaining);
            ref var firstByte = ref Unsafe.As<T, byte>(ref first);
            var bytes = MemoryMarshal.CreateSpan(ref firstByte, count * elementSize);
            CryptographicOperations.ZeroMemory(bytes);
            remaining = remaining[count..];
        }
    }

    /// <summary>
    /// Compares two spans as <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
    /// does, at most <paramref name="maxElementsPerChunk"/> elements per call, so no call covers more than
    /// <see cref="int.MaxValue"/> bytes.
    /// </summary>
    /// <typeparam name="T">The unmanaged value type of the span elements.</typeparam>
    /// <param name="left">The first span to compare.</param>
    /// <param name="right">The second span to compare.</param>
    /// <param name="maxElementsPerChunk">The most elements one call compares; at least 1.</param>
    /// <returns><see langword="true"/> when the spans have the same length and bytes.</returns>
    internal static bool FixedTimeEqualsInChunks<T>(
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right,
        int maxElementsPerChunk
    )
        where T : unmanaged
    {
        // The length is not secret: CryptographicOperations.FixedTimeEquals also returns false at once when it differs.
        if (left.Length != right.Length)
            return false;

        // Every chunk is compared, whatever an earlier one returned, so the time depends only on the length.
        var equal = true;
        while (!left.IsEmpty)
        {
            var count = Math.Min(left.Length, maxElementsPerChunk);
            equal &= CryptographicOperations.FixedTimeEquals(
                MemoryMarshal.AsBytes(left[..count]),
                MemoryMarshal.AsBytes(right[..count])
            );
            left = left[count..];
            right = right[count..];
        }

        return equal;
    }

    extension(CryptographicOperations)
    {
        /// <summary>
        /// Fills a span with zeros in a way that is not optimized away by the compiler or runtime.
        /// </summary>
        /// <typeparam name="T">The unmanaged value type of the span elements.</typeparam>
        /// <param name="buffer">The span to fill with zeros.</param>
        /// <remarks>
        /// <para>
        /// This method ensures that the memory is actually zeroed and not optimized away, which is critical
        /// for clearing sensitive data such as cryptographic keys or passwords from memory.
        /// </para>
        /// <para>
        /// Unlike a simple <c>buffer.Clear()</c> or loop assignment, this method uses
        /// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> internally, which guarantees
        /// the zeroing operation will not be removed by compiler optimizations.
        /// </para>
        /// <para>
        /// A span of any length is zeroed: the bytes are zeroed in chunks of at most <see cref="int.MaxValue"/> bytes,
        /// the most one <see cref="Span{T}"/> of bytes can hold.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// Span&lt;int&gt; sensitiveData = stackalloc int[4];
        /// // ... use the sensitive data ...
        ///
        /// // Securely clear the data when done
        /// CryptographicOperations.ZeroMemory(sensitiveData);
        /// </code>
        /// </example>
        [PublicAPI]
        public static void ZeroMemory<T>(Span<T> buffer)
            where T : unmanaged
        {
            ZeroMemoryOrClear(buffer);
        }

        /// <summary>
        /// Compares two spans in a way that does not leak timing information about the contents.
        /// </summary>
        /// <typeparam name="T">The unmanaged value type of the span elements.</typeparam>
        /// <param name="left">The first span to compare.</param>
        /// <param name="right">The second span to compare.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> have the same
        /// length and contents; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method performs a constant-time comparison, meaning the time taken to compare two spans
        /// does not depend on where the first difference occurs. This is critical for comparing secrets
        /// such as cryptographic hashes, MACs, or authentication tokens to prevent timing attacks.
        /// </para>
        /// <para>
        /// Spans of any length are compared: spans of equal length are compared in chunks of at most
        /// <see cref="int.MaxValue"/> bytes, and every chunk is compared, whatever an earlier chunk held.
        /// </para>
        /// <para>
        /// A timing attack could allow an attacker to determine the contents of a secret value by
        /// measuring how long comparisons take. This method prevents such attacks by always comparing
        /// all bytes regardless of where differences occur.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// ReadOnlySpan&lt;byte&gt; computedHash = ComputeHash(data);
        /// ReadOnlySpan&lt;byte&gt; expectedHash = GetExpectedHash();
        ///
        /// // Use constant-time comparison to prevent timing attacks
        /// bool isValid = CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        /// </code>
        /// </example>
        [PublicAPI]
        public static bool FixedTimeEquals<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
            where T : unmanaged
        {
            var maxElementsPerChunk = int.MaxValue / Unsafe.SizeOf<T>();
            return FixedTimeEqualsInChunks(left, right, maxElementsPerChunk);
        }
    }
}
