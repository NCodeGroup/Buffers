// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides methods that convert between binary data and its text representation using rented buffers.
/// </summary>
/// <remarks>
/// <para>
/// Each method validates the <see cref="BinaryTextFormat"/> before it rents anything, so an undefined format
/// throws without renting a buffer. When decoding throws, the rented buffer is disposed before the exception leaves.
/// </para>
/// <para>
/// When <c>isSensitive</c> is <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
/// and securely zeroed when the owner is disposed.
/// </para>
/// </remarks>
[PublicAPI]
public static class BinaryTextEncoding
{
    /// <summary>
    /// Decodes the text into a rented buffer that holds exactly the decoded bytes.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="format">The text format of <paramref name="text"/>.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
    /// and securely zeroed when disposed.
    /// </param>
    /// <param name="bytes">When this method returns, contains a <see cref="Span{T}"/> of exactly the decoded bytes.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a defined <see cref="BinaryTextFormat"/> value.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="text"/> is not valid in the given format.</exception>
    /// <remarks>
    /// Hexadecimal decoding accepts both uppercase and lowercase digits for either hexadecimal format.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var owner = BinaryTextEncoding.RentDecoded("FBFF01", BinaryTextFormat.HexUpper, isSensitive: true, out Span&lt;byte&gt; bytes);
    /// var hash = SHA256.HashData(bytes);
    /// </code>
    /// </example>
    public static IMemoryOwner<byte> RentDecoded(
        ReadOnlySpan<char> text,
        BinaryTextFormat format,
        bool isSensitive,
        out Span<byte> bytes
    )
    {
        ValidateFormat(format);

        var owner = BufferFactory.Rent(
            GetMaxDecodedLength(text.Length, format),
            isSensitive,
            out bytes
        );
        try
        {
            bytes = bytes[..Decode(text, format, bytes)];
            return owner;
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes the text into a rented buffer that holds exactly the decoded bytes, returning it as <see cref="Memory{T}"/>.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="format">The text format of <paramref name="text"/>.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
    /// and securely zeroed when disposed.
    /// </param>
    /// <param name="bytes">When this method returns, contains a <see cref="Memory{T}"/> of exactly the decoded bytes.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a defined <see cref="BinaryTextFormat"/> value.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="text"/> is not valid in the given format.</exception>
    /// <remarks>
    /// Hexadecimal decoding accepts both uppercase and lowercase digits for either hexadecimal format.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var owner = BinaryTextEncoding.RentDecoded("FBFF01", BinaryTextFormat.HexUpper, isSensitive: true, out Memory&lt;byte&gt; bytes);
    /// var hash = SHA256.HashData(bytes.Span);
    /// </code>
    /// </example>
    public static IMemoryOwner<byte> RentDecoded(
        ReadOnlySpan<char> text,
        BinaryTextFormat format,
        bool isSensitive,
        out Memory<byte> bytes
    )
    {
        ValidateFormat(format);

        var owner = BufferFactory.Rent(
            GetMaxDecodedLength(text.Length, format),
            isSensitive,
            out bytes
        );
        try
        {
            bytes = bytes[..Decode(text, format, bytes.Span)];
            return owner;
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Encodes the bytes into a rented buffer that holds exactly the encoded text.
    /// </summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <param name="format">The text format to produce.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
    /// and securely zeroed when disposed.
    /// </param>
    /// <param name="text">When this method returns, contains a <see cref="Span{T}"/> of exactly the encoded text.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a defined <see cref="BinaryTextFormat"/> value.</exception>
    /// <example>
    /// <code>
    /// var key = SHA256.HashData("data"u8);
    /// using var owner = BinaryTextEncoding.RentEncoded(key, BinaryTextFormat.HexLower, isSensitive: true, out Span&lt;char&gt; text);
    /// var length = text.Length;
    /// </code>
    /// </example>
    public static IMemoryOwner<char> RentEncoded(
        ReadOnlySpan<byte> bytes,
        BinaryTextFormat format,
        bool isSensitive,
        out Span<char> text
    )
    {
        ValidateFormat(format);

        var owner = BufferFactory.Rent(
            GetEncodedLength(bytes.Length, format),
            isSensitive,
            out text
        );
        text = text[..Encode(bytes, format, text)];
        return owner;
    }

    /// <summary>
    /// Encodes the bytes into a rented buffer that holds exactly the encoded text, returning it as <see cref="Memory{T}"/>.
    /// </summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <param name="format">The text format to produce.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
    /// and securely zeroed when disposed.
    /// </param>
    /// <param name="text">When this method returns, contains a <see cref="Memory{T}"/> of exactly the encoded text.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a defined <see cref="BinaryTextFormat"/> value.</exception>
    /// <example>
    /// <code>
    /// var key = SHA256.HashData("data"u8);
    /// using var owner = BinaryTextEncoding.RentEncoded(key, BinaryTextFormat.HexLower, isSensitive: true, out Memory&lt;char&gt; text);
    /// var length = text.Length;
    /// </code>
    /// </example>
    public static IMemoryOwner<char> RentEncoded(
        ReadOnlySpan<byte> bytes,
        BinaryTextFormat format,
        bool isSensitive,
        out Memory<char> text
    )
    {
        ValidateFormat(format);

        var owner = BufferFactory.Rent(
            GetEncodedLength(bytes.Length, format),
            isSensitive,
            out text
        );
        text = text[..Encode(bytes, format, text.Span)];
        return owner;
    }

    private static void ValidateFormat(BinaryTextFormat format)
    {
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "The format is not a BinaryTextFormat value."
            );
    }

    private static int GetMaxDecodedLength(int textLength, BinaryTextFormat format) =>
        format switch
        {
            BinaryTextFormat.Base64 => Base64.GetMaxDecodedFromUtf8Length(textLength),
            BinaryTextFormat.Base64Url => Base64Url.GetMaxDecodedLength(textLength),
            _ => textLength / 2,
        };

    private static int Decode(
        ReadOnlySpan<char> text,
        BinaryTextFormat format,
        Span<byte> destination
    )
    {
        switch (format)
        {
            case BinaryTextFormat.Base64:
                if (!Convert.TryFromBase64Chars(text, destination, out var base64Written))
                    throw new FormatException("The text is not valid Base64.");
                return base64Written;

            case BinaryTextFormat.Base64Url:
                return Base64Url.DecodeFromChars(text, destination);

            default:
                var status = Convert.FromHexString(text, destination, out _, out var hexWritten);
                if (status != OperationStatus.Done)
                    throw new FormatException("The text is not valid hexadecimal.");
                return hexWritten;
        }
    }

    private static int GetEncodedLength(int byteLength, BinaryTextFormat format) =>
        format switch
        {
            BinaryTextFormat.Base64 => Base64.GetMaxEncodedToUtf8Length(byteLength),
            BinaryTextFormat.Base64Url => Base64Url.GetEncodedLength(byteLength),
            _ => checked(byteLength * 2),
        };

    private static int Encode(
        ReadOnlySpan<byte> bytes,
        BinaryTextFormat format,
        Span<char> destination
    )
    {
        switch (format)
        {
            case BinaryTextFormat.Base64:
                var base64Success = Convert.TryToBase64Chars(
                    bytes,
                    destination,
                    out var base64Written
                );
                Debug.Assert(base64Success);
                return base64Written;

            case BinaryTextFormat.Base64Url:
                return Base64Url.EncodeToChars(bytes, destination);

            case BinaryTextFormat.HexUpper:
                var upperSuccess = Convert.TryToHexString(bytes, destination, out var upperWritten);
                Debug.Assert(upperSuccess);
                return upperWritten;

            default:
                var lowerSuccess = Convert.TryToHexStringLower(
                    bytes,
                    destination,
                    out var lowerWritten
                );
                Debug.Assert(lowerSuccess);
                return lowerWritten;
        }
    }
}
