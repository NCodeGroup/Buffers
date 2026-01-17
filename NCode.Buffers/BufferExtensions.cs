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

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> and <see cref="Memory{T}"/> to enable buffer writing operations
/// and secure memory management.
/// </summary>
/// <remarks>
/// <para>
/// This class contains extension methods that provide convenient ways to create buffer writers and secure lifetimes
/// from existing span and memory instances.
/// </para>
/// <para>
/// The extension methods use C# 14 extension member syntax and provide fluent APIs for common buffer operations.
/// </para>
/// </remarks>
[PublicAPI]
public static class BufferExtensions
{
    /// <summary>
    /// Extension methods for <see cref="Span{T}"/> where T is a value type.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    extension<T>(Span<T> span) where T : struct
    {
        /// <summary>
        /// Wraps this span in a <see cref="SecureSpanLifetime{T}"/> that securely zeroes the memory upon disposal.
        /// </summary>
        /// <returns>
        /// A <see cref="SecureSpanLifetime{T}"/> that manages the lifetime of this span and ensures
        /// the memory is securely zeroed when disposed.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is useful when you have a span containing sensitive data (such as cryptographic keys,
        /// passwords, or other secrets) and want to ensure the memory is securely cleared when you're done with it.
        /// </para>
        /// <para>
        /// The returned lifetime uses <see cref="CryptographicOperations.ZeroMemory"/>
        /// to ensure the memory is cleared in a way that cannot be optimized away by the compiler or JIT.
        /// </para>
        /// <para>
        /// Since <see cref="SecureSpanLifetime{T}"/> is a ref struct, it can only be used on the stack
        /// and cannot be stored in fields of reference types.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// Span&lt;byte&gt; buffer = stackalloc byte[256];
        /// using var lifetime = buffer.GetSecureLifetime();
        /// // Use buffer for cryptographic operations
        /// // Memory is automatically zeroed when lifetime is disposed
        /// </code>
        /// </example>
        public SecureSpanLifetime<T> GetSecureLifetime()
        {
            return new SecureSpanLifetime<T>(span);
        }

        /// <summary>
        /// Creates a <see cref="FixedSpanBufferWriter{T}"/> that writes to this span.
        /// </summary>
        /// <returns>
        /// A <see cref="FixedSpanBufferWriter{T}"/> that can be used to write data to the span.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The returned buffer writer has a fixed capacity equal to the length of this span and cannot grow.
        /// </para>
        /// <para>
        /// Since <see cref="FixedSpanBufferWriter{T}"/> is a ref struct, it can only be used on the stack
        /// and cannot be stored in fields of reference types.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// Span&lt;byte&gt; buffer = stackalloc byte[100];
        /// var writer = buffer.GetFixedBufferWriter();
        /// var span = writer.GetSpan(10);
        /// // Write data to span...
        /// writer.Advance(10);
        /// </code>
        /// </example>
        public FixedSpanBufferWriter<T> GetFixedBufferWriter()
        {
            return new FixedSpanBufferWriter<T>(span);
        }
    }

    /// <summary>
    /// Extension methods for <see cref="Memory{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    extension<T>(Memory<T> memory)
    {
        /// <summary>
        /// Creates a <see cref="FixedMemoryBufferWriter{T}"/> that writes to this memory.
        /// </summary>
        /// <returns>
        /// A <see cref="FixedMemoryBufferWriter{T}"/> that can be used to write data to the memory.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The returned buffer writer has a fixed capacity equal to the length of this memory and cannot grow.
        /// </para>
        /// <para>
        /// Unlike <see cref="FixedSpanBufferWriter{T}"/>, <see cref="FixedMemoryBufferWriter{T}"/> is a class
        /// and can be stored in fields and passed to async methods.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// Memory&lt;byte&gt; buffer = new byte[100];
        /// var writer = buffer.GetFixedBufferWriter();
        /// var span = writer.GetSpan(10);
        /// // Write data to span...
        /// writer.Advance(10);
        /// </code>
        /// </example>
        public FixedMemoryBufferWriter<T> GetFixedBufferWriter()
        {
            return new FixedMemoryBufferWriter<T>(memory);
        }
    }
}
