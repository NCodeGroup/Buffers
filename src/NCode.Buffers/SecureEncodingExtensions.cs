// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides extension methods for <see cref="Encoding"/> that encode and decode into rented buffers.
/// </summary>
/// <remarks>
/// <para>
/// Each method sizes the result with <see cref="Encoding.GetByteCount(ReadOnlySpan{char})"/> or
/// <see cref="Encoding.GetCharCount(ReadOnlySpan{byte})"/> before it rents anything, so an invalid input
/// throws the encoding's fallback exception without renting a buffer. Use <see cref="SecureEncoding.UTF8"/>
/// or another <see cref="SecureEncoding"/> instance so that invalid input throws instead of being replaced.
/// </para>
/// <para>
/// When <c>isSensitive</c> is <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
/// and securely zeroed when the owner is disposed.
/// </para>
/// </remarks>
[PublicAPI]
public static class SecureEncodingExtensions
{
    extension(Encoding encoding)
    {
        /// <summary>
        /// Encodes the characters into a rented buffer that holds exactly the encoded bytes.
        /// </summary>
        /// <param name="chars">The characters to encode.</param>
        /// <param name="isSensitive">
        /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
        /// and securely zeroed when disposed.
        /// </param>
        /// <param name="bytes">When this method returns, contains a <see cref="Span{T}"/> of exactly the encoded bytes. It is valid until the returned owner is disposed.</param>
        /// <returns>
        /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
        /// Dispose this owner to return the buffer to the pool.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the encoding is <see langword="null"/>.</exception>
        /// <exception cref="EncoderFallbackException">
        /// Thrown when <paramref name="chars"/> cannot be encoded and the encoding uses an exception fallback.
        /// </exception>
        /// <remarks>
        /// Use <see cref="SecureEncoding.UTF8"/> as the encoding so that invalid text throws instead of being replaced.
        /// </remarks>
        /// <example>
        /// <code>
        /// using var owner = SecureEncoding.UTF8.RentBytes("café", isSensitive: true, out Span&lt;byte&gt; bytes);
        /// var hash = SHA256.HashData(bytes);
        /// </code>
        /// </example>
        public IMemoryOwner<byte> RentBytes(
            ReadOnlySpan<char> chars,
            bool isSensitive,
            out Span<byte> bytes
        )
        {
            var owner = encoding.RentBytes(chars, isSensitive, out Memory<byte> memory);
            bytes = memory.Span;
            return owner;
        }

        /// <summary>
        /// Encodes the characters into a rented buffer that holds exactly the encoded bytes, returning it as <see cref="Memory{T}"/>.
        /// </summary>
        /// <param name="chars">The characters to encode.</param>
        /// <param name="isSensitive">
        /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
        /// and securely zeroed when disposed.
        /// </param>
        /// <param name="bytes">When this method returns, contains a <see cref="Memory{T}"/> of exactly the encoded bytes. It is valid until the returned owner is disposed.</param>
        /// <returns>
        /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
        /// Dispose this owner to return the buffer to the pool.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the encoding is <see langword="null"/>.</exception>
        /// <exception cref="EncoderFallbackException">
        /// Thrown when <paramref name="chars"/> cannot be encoded and the encoding uses an exception fallback.
        /// </exception>
        /// <remarks>
        /// Use <see cref="SecureEncoding.UTF8"/> as the encoding so that invalid text throws instead of being replaced.
        /// </remarks>
        /// <example>
        /// <code>
        /// using var owner = SecureEncoding.UTF8.RentBytes("café", isSensitive: true, out Memory&lt;byte&gt; bytes);
        /// var hash = SHA256.HashData(bytes.Span);
        /// </code>
        /// </example>
        public IMemoryOwner<byte> RentBytes(
            ReadOnlySpan<char> chars,
            bool isSensitive,
            out Memory<byte> bytes
        )
        {
            ArgumentNullException.ThrowIfNull(encoding);

            var byteCount = encoding.GetByteCount(chars);
            var owner = BufferFactory.Rent(byteCount, isSensitive, out bytes);
            try
            {
                var written = encoding.GetBytes(chars, bytes.Span);

                // The rental has the length the encoding counted; the slice keeps an encoding
                // that writes fewer than it counted from exposing the rest of the rental.
                bytes = bytes[..written];
                return owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Decodes the bytes into a rented buffer that holds exactly the decoded characters.
        /// </summary>
        /// <param name="bytes">The bytes to decode.</param>
        /// <param name="isSensitive">
        /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
        /// and securely zeroed when disposed.
        /// </param>
        /// <param name="chars">When this method returns, contains a <see cref="Span{T}"/> of exactly the decoded characters. It is valid until the returned owner is disposed.</param>
        /// <returns>
        /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
        /// Dispose this owner to return the buffer to the pool.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the encoding is <see langword="null"/>.</exception>
        /// <exception cref="DecoderFallbackException">
        /// Thrown when <paramref name="bytes"/> cannot be decoded and the encoding uses an exception fallback.
        /// </exception>
        /// <remarks>
        /// Use <see cref="SecureEncoding.UTF8"/> as the encoding so that invalid bytes throw instead of being replaced.
        /// </remarks>
        /// <example>
        /// <code>
        /// ReadOnlySpan&lt;byte&gt; utf8 = "café"u8;
        /// using var owner = SecureEncoding.UTF8.RentChars(utf8, isSensitive: true, out Span&lt;char&gt; chars);
        /// var length = chars.Length;
        /// </code>
        /// </example>
        public IMemoryOwner<char> RentChars(
            ReadOnlySpan<byte> bytes,
            bool isSensitive,
            out Span<char> chars
        )
        {
            var owner = encoding.RentChars(bytes, isSensitive, out Memory<char> memory);
            chars = memory.Span;
            return owner;
        }

        /// <summary>
        /// Decodes the bytes into a rented buffer that holds exactly the decoded characters, returning it as <see cref="Memory{T}"/>.
        /// </summary>
        /// <param name="bytes">The bytes to decode.</param>
        /// <param name="isSensitive">
        /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
        /// and securely zeroed when disposed.
        /// </param>
        /// <param name="chars">When this method returns, contains a <see cref="Memory{T}"/> of exactly the decoded characters. It is valid until the returned owner is disposed.</param>
        /// <returns>
        /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
        /// Dispose this owner to return the buffer to the pool.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the encoding is <see langword="null"/>.</exception>
        /// <exception cref="DecoderFallbackException">
        /// Thrown when <paramref name="bytes"/> cannot be decoded and the encoding uses an exception fallback.
        /// </exception>
        /// <remarks>
        /// Use <see cref="SecureEncoding.UTF8"/> as the encoding so that invalid bytes throw instead of being replaced.
        /// </remarks>
        /// <example>
        /// <code>
        /// ReadOnlySpan&lt;byte&gt; utf8 = "café"u8;
        /// using var owner = SecureEncoding.UTF8.RentChars(utf8, isSensitive: true, out Memory&lt;char&gt; chars);
        /// var length = chars.Length;
        /// </code>
        /// </example>
        public IMemoryOwner<char> RentChars(
            ReadOnlySpan<byte> bytes,
            bool isSensitive,
            out Memory<char> chars
        )
        {
            ArgumentNullException.ThrowIfNull(encoding);

            var charCount = encoding.GetCharCount(bytes);
            var owner = BufferFactory.Rent(charCount, isSensitive, out chars);
            try
            {
                var written = encoding.GetChars(bytes, chars.Span);

                // The rental has the length the encoding counted; the slice keeps an encoding
                // that writes fewer than it counted from exposing the rest of the rental.
                chars = chars[..written];
                return owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }
    }
}
