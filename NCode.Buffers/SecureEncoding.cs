#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides secure encodings that throw an exception when invalid bytes are encountered.
/// </summary>
/// <remarks>
/// <para>
/// The standard <see cref="Encoding.ASCII"/> and <see cref="Encoding.UTF8"/> encodings use replacement fallbacks
/// by default, which silently substitute invalid characters with a replacement character (e.g., '?' or U+FFFD).
/// This can lead to security vulnerabilities or data corruption in scenarios where strict validation is required.
/// </para>
/// <para>
/// This class provides pre-configured encoding instances that use <see cref="EncoderFallback.ExceptionFallback"/>
/// and <see cref="DecoderFallback.ExceptionFallback"/>, ensuring that any invalid byte sequences cause an
/// <see cref="EncoderFallbackException"/> or <see cref="DecoderFallbackException"/> to be thrown.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Encoding with strict validation
/// byte[] bytes = SecureEncoding.UTF8.GetBytes("Hello, World!");
///
/// // Decoding with strict validation - throws on invalid UTF-8 sequences
/// string text = SecureEncoding.UTF8.GetString(bytes);
/// </code>
/// </example>
[PublicAPI]
public static class SecureEncoding
{
    /// <summary>
    /// Creates a secure ASCII encoding instance with exception fallbacks.
    /// </summary>
    /// <returns>An <see cref="ASCIIEncoding"/> configured to throw on invalid characters.</returns>
    private static ASCIIEncoding CreateSecureAsciiEncoding()
    {
        var encoding = (ASCIIEncoding)Encoding.ASCII.Clone();
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        return encoding;
    }

    /// <summary>
    /// Gets an ASCII encoding that throws an exception when invalid bytes are encountered.
    /// </summary>
    /// <value>
    /// An <see cref="ASCIIEncoding"/> instance configured with <see cref="EncoderFallback.ExceptionFallback"/>
    /// and <see cref="DecoderFallback.ExceptionFallback"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// ASCII encoding only supports characters in the range 0x00 to 0x7F. Any character outside this range
    /// will cause an <see cref="EncoderFallbackException"/> when encoding, and any byte value greater than
    /// 0x7F will cause a <see cref="DecoderFallbackException"/> when decoding.
    /// </para>
    /// </remarks>
    // ReSharper disable once InconsistentNaming
    public static ASCIIEncoding ASCII { get; } = CreateSecureAsciiEncoding();

    /// <summary>
    /// Gets a UTF-8 encoding that throws an exception when invalid bytes are encountered.
    /// </summary>
    /// <value>
    /// A <see cref="UTF8Encoding"/> instance configured to throw on invalid byte sequences and without a BOM.
    /// </value>
    /// <remarks>
    /// <para>
    /// This encoding does not emit a UTF-8 byte order mark (BOM) when encoding. Invalid UTF-8 byte sequences
    /// will cause a <see cref="DecoderFallbackException"/> when decoding, and invalid surrogate pairs will
    /// cause an <see cref="EncoderFallbackException"/> when encoding.
    /// </para>
    /// </remarks>
    // ReSharper disable once InconsistentNaming
    public static UTF8Encoding UTF8 { get; } = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
