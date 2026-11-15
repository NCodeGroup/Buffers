// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace ReadmeSamples;

// begin-snippet: QuickStart
using System.Security.Cryptography;
using NCode.Buffers;

public static class Signatures
{
    public static bool VerifySignature(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> signature
    )
    {
        // A pinned, pooled 32-byte key, zeroed when keyOwner is disposed.
        using var keyOwner = BufferFactory.Rent(32, isSensitive: true, out Span<byte> key);
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            key,
            iterations: 600_000,
            HashAlgorithmName.SHA256
        );

        // The expected MAC lives on the stack and is zeroed when expectedLifetime is disposed.
        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        using var expectedLifetime = expected.GetSecureLifetime();
        HMACSHA256.HashData(key, payload, expected);

        // Constant time, so the comparison doesn't reveal where the MACs differ.
        return CryptographicOperations.FixedTimeEquals(expected, signature);
    }
}
// end-snippet
