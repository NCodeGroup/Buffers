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
    /// The message of the <see cref="FormatException"/> thrown for text that is not strict Base64url.
    /// </summary>
    private const string StrictBase64UrlMessage =
        "The text is not strict Base64url: only the unpadded URL-safe alphabet, with no padding, whitespace or unused bits set.";

    /// <summary>
    /// The characters that strict Base64url text may contain.
    /// </summary>
    private static readonly SearchValues<char> Base64UrlAlphabet = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
    );

    /// <summary>
    /// Decodes the text into a rented buffer that holds exactly the decoded bytes.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="format">The text format of <paramref name="text"/>.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
    /// and securely zeroed when disposed.
    /// </param>
    /// <param name="bytes">
    /// When this method returns, contains a <see cref="Span{T}"/> of exactly the decoded bytes.
    /// It is valid until the returned owner is disposed.
    /// </param>
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
        var owner = RentDecoded(text, format, isSensitive, out Memory<byte> memory);
        bytes = memory.Span;
        return owner;
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
    /// <param name="bytes">
    /// When this method returns, contains a <see cref="Memory{T}"/> of exactly the decoded bytes.
    /// It is valid until the returned owner is disposed.
    /// </param>
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

        var maxLength = GetMaxDecodedLength(text.Length, format);
        var owner = BufferFactory.Rent(maxLength, isSensitive, out bytes);
        try
        {
            var written = Decode(text, format, bytes.Span);
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
    /// Encodes the bytes into a rented buffer that holds exactly the encoded text.
    /// </summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <param name="format">The text format to produce.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, the buffer is rented from <see cref="SecureMemoryPool{T}"/>
    /// and securely zeroed when disposed.
    /// </param>
    /// <param name="text">
    /// When this method returns, contains a <see cref="Span{T}"/> of exactly the encoded text.
    /// It is valid until the returned owner is disposed.
    /// </param>
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
        var owner = RentEncoded(bytes, format, isSensitive, out Memory<char> memory);
        text = memory.Span;
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
    /// <param name="text">
    /// When this method returns, contains a <see cref="Memory{T}"/> of exactly the encoded text.
    /// It is valid until the returned owner is disposed.
    /// </param>
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

        var length = GetEncodedLength(bytes.Length, format);
        var owner = BufferFactory.Rent(length, isSensitive, out text);
        var written = Encode(bytes, format, text.Span);
        text = text[..written];
        return owner;
    }

    /// <summary>
    /// Throws when <paramref name="format"/> is not a defined <see cref="BinaryTextFormat"/> value, so that nothing is rented for it.
    /// </summary>
    /// <param name="format">The format to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a defined <see cref="BinaryTextFormat"/> value.</exception>
    private static void ValidateFormat(BinaryTextFormat format)
    {
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "The format is not a BinaryTextFormat value."
            );
    }

    /// <summary>
    /// Gets the largest number of bytes that text of the given length decodes to in the format.
    /// </summary>
    /// <param name="textLength">The number of characters in the text.</param>
    /// <param name="format">
    /// The text format, which is a defined <see cref="BinaryTextFormat"/> value.
    /// The switch reads every value it has no arm for as hexadecimal.
    /// </param>
    /// <returns>The size of the buffer to rent for decoding.</returns>
    private static int GetMaxDecodedLength(int textLength, BinaryTextFormat format) =>
        format switch
        {
            BinaryTextFormat.Base64 => Base64.GetMaxDecodedFromUtf8Length(textLength),
            BinaryTextFormat.Base64Url or BinaryTextFormat.Base64UrlStrict =>
                Base64Url.GetMaxDecodedLength(textLength),
            _ => textLength / 2,
        };

    /// <summary>
    /// Decodes the text into <paramref name="destination"/>.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="format">
    /// The text format, which is a defined <see cref="BinaryTextFormat"/> value.
    /// The switch reads every value it has no arm for as hexadecimal.
    /// </param>
    /// <param name="destination">The buffer that receives the decoded bytes.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="text"/> is not valid in the given format.</exception>
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

            case BinaryTextFormat.Base64UrlStrict:
                if (text.ContainsAnyExcept(Base64UrlAlphabet))
                    throw new FormatException(StrictBase64UrlMessage);
                var strictStatus = Base64Url.DecodeFromChars(
                    text,
                    destination,
                    out _,
                    out var strictWritten,
                    isFinalBlock: true
                );
                if (strictStatus != OperationStatus.Done)
                    throw new FormatException(StrictBase64UrlMessage);
                return strictWritten;

            default:
                var status = Convert.FromHexString(text, destination, out _, out var hexWritten);
                if (status != OperationStatus.Done)
                    throw new FormatException("The text is not valid hexadecimal.");
                return hexWritten;
        }
    }

    /// <summary>
    /// Gets the number of characters that bytes of the given length encode to in the format.
    /// </summary>
    /// <param name="byteLength">The number of bytes to encode.</param>
    /// <param name="format">
    /// The text format, which is a defined <see cref="BinaryTextFormat"/> value.
    /// The switch reads every value it has no arm for as hexadecimal.
    /// </param>
    /// <returns>The size of the buffer to rent for encoding.</returns>
    private static int GetEncodedLength(int byteLength, BinaryTextFormat format) =>
        format switch
        {
            BinaryTextFormat.Base64 => Base64.GetMaxEncodedToUtf8Length(byteLength),
            BinaryTextFormat.Base64Url or BinaryTextFormat.Base64UrlStrict =>
                Base64Url.GetEncodedLength(byteLength),
            _ => checked(byteLength * 2),
        };

    /// <summary>
    /// Encodes the bytes into <paramref name="destination"/>.
    /// </summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <param name="format">
    /// The text format, which is a defined <see cref="BinaryTextFormat"/> value.
    /// The switch reads every value it has no arm for as lowercase hexadecimal.
    /// </param>
    /// <param name="destination">The buffer that receives the encoded text.</param>
    /// <returns>The number of characters written to <paramref name="destination"/>.</returns>
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
            case BinaryTextFormat.Base64UrlStrict:
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
