// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SiteSamples;

// The samples' tests compare a key, a MAC, a hash or other secret bytes in fixed time, as the C# conventions require
// of tests too, through .NET's CryptographicOperations.FixedTimeEquals and not the library's, which the samples test.
// When the values differ, the message shows both: they are each test's own fixtures, never a real secret.
internal static class SecretAssert
{
    public static void Equal(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        if (CryptographicOperations.FixedTimeEquals(expected, actual))
            return;

        string expectedHex = Convert.ToHexString(expected);
        string actualHex = Convert.ToHexString(actual);
        Assert.Fail(
            $"""
            The bytes differ.
            Expected: {expectedHex}
            Actual:   {actualHex}
            """
        );
    }

    // Text that holds a secret, such as a key printed as hex, compared as UTF-16 code units: ordinal and
    // case-sensitive.
    public static void Equal(ReadOnlySpan<char> expected, ReadOnlySpan<char> actual)
    {
        ReadOnlySpan<byte> expectedBytes = MemoryMarshal.AsBytes(expected);
        ReadOnlySpan<byte> actualBytes = MemoryMarshal.AsBytes(actual);
        if (CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            return;

        Assert.Fail(
            $"""
            The text differs.
            Expected:
            {expected}
            Actual:
            {actual}
            """
        );
    }
}
