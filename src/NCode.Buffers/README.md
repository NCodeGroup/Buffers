# NCode.Buffers

[![CI](https://github.com/NCodeGroup/Buffers/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/NCodeGroup/Buffers/actions/workflows/ci.yml?query=branch%3Amain) [![Coverage](https://codecov.io/gh/NCodeGroup/Buffers/branch/main/graph/badge.svg)](https://codecov.io/gh/NCodeGroup/Buffers) [![Target framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FBuffers%2Fmain%2Fsrc%2FNCode.Buffers%2FNCode.Buffers.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4)](https://www.nuget.org/packages/NCode.Buffers#supportedframeworks-body-tab) [![License](https://img.shields.io/github/license/NCodeGroup/Buffers)](https://github.com/NCodeGroup/Buffers/blob/main/LICENSE.txt)

Pinned, pooled .NET buffers that zero themselves on dispose, for keys and other secrets, plus fixed-size buffer writers and string splitting without substring copies.

## Installation

```shell
dotnet add package NCode.Buffers
```

The target frameworks are the ones the package's `.csproj` declares; they are listed on [nuget.org's Frameworks tab](https://www.nuget.org/packages/NCode.Buffers#supportedframeworks-body-tab) and shown by the badge above. [CHANGELOG.md](https://github.com/NCodeGroup/Buffers/blob/main/CHANGELOG.md) records when a framework is added or dropped and which release line to stay on. The package depends on Nerdbank.Streams, whose `Sequence<T>` the pooled buffer writers return.

## Example

This example derives a key from a password, verifies an HMAC-SHA256 signature with it, and zeroes the derived key and the expected MAC when the method returns.

<!-- snippet: QuickStart -->
```cs
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
```
<!-- endSnippet -->

## Learn more

- [Documentation and samples](https://github.com/NCodeGroup/Buffers#readme)
- [Choosing a type](https://github.com/NCodeGroup/Buffers#choosing-a-type)
- [Security notes](https://github.com/NCodeGroup/Buffers#security-notes)
- [Changelog](https://github.com/NCodeGroup/Buffers/blob/main/CHANGELOG.md)
- [Report a vulnerability](https://github.com/NCodeGroup/Buffers/blob/main/SECURITY.md)
