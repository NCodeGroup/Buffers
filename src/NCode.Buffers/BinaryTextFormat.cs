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
    /// URL-safe Base64 text, which uses the <c>-</c> and <c>_</c> characters and no padding.
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
}
