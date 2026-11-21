// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Specifies a text representation of binary data.
/// </summary>
[PublicAPI]
public enum BinaryTextFormat
{
    /// <summary>
    /// Standard Base64 text, which uses the <c>+</c> and <c>/</c> characters and <c>=</c> padding.
    /// </summary>
    Base64 = 0,

    /// <summary>
    /// URL-safe Base64 text, which uses the <c>-</c> and <c>_</c> characters. Encoding writes no padding; decoding also accepts <c>=</c> padding and whitespace. Use <see cref="Base64UrlStrict"/> to refuse them.
    /// </summary>
    Base64Url = 1,

    /// <summary>
    /// Hexadecimal text that uses the uppercase digits <c>A</c> through <c>F</c>.
    /// </summary>
    HexUpper = 2,

    /// <summary>
    /// Hexadecimal text that uses the lowercase digits <c>a</c> through <c>f</c>.
    /// </summary>
    HexLower = 3,

    /// <summary>
    /// Canonical unpadded URL-safe Base64 text, as JOSE requires (RFC 7515 section 2): decoding accepts only the characters <c>A</c>-<c>Z</c>, <c>a</c>-<c>z</c>, <c>0</c>-<c>9</c>, <c>-</c> and <c>_</c>, and refuses <c>=</c> padding, whitespace, a length that leaves one character over and a final character whose unused bits are not zero. Encoding writes the same text as <see cref="Base64Url"/>.
    /// </summary>
    Base64UrlStrict = 4,
}
