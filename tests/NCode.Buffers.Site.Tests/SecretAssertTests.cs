// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit.Sdk;

namespace SiteSamples;

// Every sample test that compares a key, a MAC or another secret relies on SecretAssert, so these tests show that it
// passes equal values held in different memory and fails on each kind of difference, with both values in its
// message (ADR-1010).
public class SecretAssertTests
{
    #region Equal Tests

    [Fact]
    public void Equal_SameBytes_Passes()
    {
        byte[] expected = [0x28, 0xEC, 0x9E, 0x24];
        byte[] actual = [0x28, 0xEC, 0x9E, 0x24];

        var exception = Record.Exception(() => SecretAssert.Equal(expected, actual));

        Assert.Null(exception);
    }

    [Fact]
    public void Equal_LastByteDiffers_FailsWithBothInHex()
    {
        byte[] expected = [0x28, 0xEC, 0x9E, 0x24];
        byte[] actual = [0x28, 0xEC, 0x9E, 0x25];

        var exception = Assert.Throws<FailException>(() =>
            SecretAssert.Equal(expected, actual)
        );

        Assert.Equal(
            """
            The bytes differ.
            Expected: 28EC9E24
            Actual:   28EC9E25
            """,
            exception.Message
        );
    }

    [Fact]
    public void Equal_ActualIsAPrefix_FailsWithBothInHex()
    {
        byte[] expected = [0x28, 0xEC, 0x9E, 0x24];
        byte[] actual = [0x28, 0xEC, 0x9E];

        var exception = Assert.Throws<FailException>(() =>
            SecretAssert.Equal(expected, actual)
        );

        Assert.Equal(
            """
            The bytes differ.
            Expected: 28EC9E24
            Actual:   28EC9E
            """,
            exception.Message
        );
    }

    [Fact]
    public void Equal_SameText_Passes()
    {
        const string expected = "In use:   28EC9E24";
        string actual = new(expected.AsSpan());

        var exception = Record.Exception(() => SecretAssert.Equal(expected, actual));

        Assert.Null(exception);
    }

    [Fact]
    public void Equal_TextDiffersOnlyInCase_FailsWithBothTexts()
    {
        const string expected = "In use:   28EC9E24";
        const string actual = "In use:   28ec9e24";

        var exception = Assert.Throws<FailException>(() =>
            SecretAssert.Equal(expected, actual)
        );

        Assert.Equal(
            """
            The text differs.
            Expected:
            In use:   28EC9E24
            Actual:
            In use:   28ec9e24
            """,
            exception.Message
        );
    }

    #endregion
}
