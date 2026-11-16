// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using ReadmeSamples;

namespace SiteSamples.Start;

// The samples of docs/site/start/quick-start.mdx. The site's build includes each begin-snippet region in the page by
// its key (ADR-1010). SiteWatchAKeyDisappear writes to the console, so this class runs in a collection that no other
// test runs beside, and its output is compared with the fixture the page shows,
// docs/site/_components/data/quick-start.watch.txt. SiteQuickStartCall calls the README's own Signatures class, which
// this project compiles from tests/NCode.Buffers.Tests/ReadmeSamples/Signatures.cs, and the page's stills,
// docs/site/_components/data/quick-start.stills.txt, must draw its MAC and the key that makes it.
[Collection(ConsoleTests.Name)]
public class QuickStartSamples
{
    [Fact]
    public void SiteWatchAKeyDisappearSample_Runs()
    {
        using var output = new StringWriter();
        var console = Console.Out;
        Console.SetOut(output);
        try
        {
            // begin-snippet: SiteWatchAKeyDisappear
            Span<byte> key = stackalloc byte[32];

            using (key.GetSecureLifetime())
            {
                // A fixed salt, so the output never changes. A real salt is random.
                Rfc2898DeriveBytes.Pbkdf2(
                    "correct horse battery staple"u8,
                    "salt"u8,
                    key,
                    iterations: 600_000,
                    HashAlgorithmName.SHA256
                );
                Console.WriteLine($"In use:   {Convert.ToHexString(key)}");
            }

            Console.WriteLine($"Disposed: {Convert.ToHexString(key)}");
            // end-snippet

            Assert.False(key.ContainsAnyExcept((byte)0));
        }
        finally
        {
            Console.SetOut(console);
        }

        SecretAssert.Equal(
            ReadFixture("quick-start.watch.txt"),
            output.ToString().ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public void SiteRentLengthsSample_Runs()
    {
        // begin-snippet: SiteRentLengths
        using var keyOwner = BufferFactory.Rent(
            32,
            isSensitive: true,
            out Span<byte> key
        );

        var requested = key.Length; // 32: the length you asked for
        var whole = keyOwner.Memory.Length; // 4096: the whole buffer behind it
        // end-snippet

        Assert.Equal(32, requested);
        Assert.Equal(4096, whole);
    }

    [Fact]
    public void SiteQuickStartCallSample_Runs()
    {
        // begin-snippet: SiteQuickStartCall
        ReadOnlySpan<byte> password = "correct horse battery staple"u8;
        ReadOnlySpan<byte> salt = "salt"u8;
        ReadOnlySpan<byte> payload = "payload"u8;

        // The sender signs the payload with a key derived from the same password and salt.
        Span<byte> signature = stackalloc byte[HMACSHA256.HashSizeInBytes];
        using (BufferFactory.Rent(32, isSensitive: true, out Span<byte> key))
        {
            Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                key,
                600_000,
                HashAlgorithmName.SHA256
            );
            HMACSHA256.HashData(key, payload, signature);
        }

        var valid = Signatures.VerifySignature(password, salt, payload, signature);

        signature[0] ^= 1;
        var tampered = Signatures.VerifySignature(password, salt, payload, signature);
        // valid is true. tampered is false: one bit of the signature changed.
        // end-snippet

        Assert.True(valid);
        Assert.False(tampered);

        // The stills draw this call: the key it derives, and the expected MAC, which is the signature before the
        // sample changed one bit of it. The call never hands out its key, so the drawn key is checked by the MAC it
        // makes of the payload: only the call's own key makes the call's MAC.
        signature[0] ^= 1;
        byte[] stillsKey = ReadStill("key");
        byte[] stillsMac = ReadStill("expected");
        byte[] stillsKeyMac = HMACSHA256.HashData(stillsKey, payload);
        SecretAssert.Equal(stillsMac, signature);
        SecretAssert.Equal(stillsKeyMac, signature);
    }

    // A line of quick-start.stills.txt, "<label>: <hex digits>", as bytes.
    private static byte[] ReadStill(string label)
    {
        foreach (string line in ReadFixture("quick-start.stills.txt").Split('\n'))
        {
            if (line.StartsWith($"{label}:", StringComparison.Ordinal))
            {
                return Convert.FromHexString(line[(label.Length + 1)..].Trim());
            }
        }

        throw new InvalidDataException($"quick-start.stills.txt has no {label}: line.");
    }

    private static string ReadFixture(string name)
    {
        for (
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            folder is not null;
            folder = folder.Parent
        )
        {
            var path = Path.Combine(
                folder.FullName,
                "docs",
                "site",
                "_components",
                "data",
                name
            );
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new FileNotFoundException(
            $"No docs/site/_components/data/{name} above {AppContext.BaseDirectory}."
        );
    }

    // The tests of this class redirect the console, so they run one at a time and never beside another test.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ConsoleTests
    {
        public const string Name = "Console";
    }
}
