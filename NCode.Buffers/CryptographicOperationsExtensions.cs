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
/// by converting them to byte spans internally using <see cref="MemoryMarshal.AsBytes{T}(Span{T})"/>.
/// </para>
/// <para>
/// These methods are essential for secure handling of sensitive data such as cryptographic keys, passwords,
/// and other secrets that may be stored in typed arrays or spans.
/// </para>
/// </remarks>
[PublicAPI]
public static class CryptographicOperationsExtensions
{
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
            where T : struct
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer));
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
            where T : struct
        {
            return CryptographicOperations.FixedTimeEquals(
                MemoryMarshal.AsBytes(left),
                MemoryMarshal.AsBytes(right)
            );
        }
    }
}
