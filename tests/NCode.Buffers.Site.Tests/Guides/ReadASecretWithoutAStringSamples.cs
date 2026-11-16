// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/read-a-secret-without-a-string.mdx. The site's build includes each begin-snippet
// region in the page by its key (ADR-1010).
public class ReadASecretWithoutAStringSamples
{
    private const string ClientSecret = "s3cr3t-Qx9vT2mZ";

    private static readonly byte[] SecretHash = SHA256.HashData(
        Encoding.UTF8.GetBytes(ClientSecret)
    );

    private static readonly byte[] Body = Encoding.UTF8.GetBytes(
        $$"""{"client_id":"reports","client_secret":"{{ClientSecret}}"}"""
    );

    // begin-snippet: SiteReadSecretUsualJson
    public sealed record TokenRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_secret")] string ClientSecret
    );

    public static bool IsKnownClient(byte[] body, byte[] secretHash)
    {
        TokenRequest? request = JsonSerializer.Deserialize<TokenRequest>(body);
        if (request is null)
            return false;

        byte[] secret = Encoding.UTF8.GetBytes(request.ClientSecret);
        byte[] hash = SHA256.HashData(secret);
        return CryptographicOperations.FixedTimeEquals(hash, secretHash);
    }

    // end-snippet

    [Fact]
    public void SiteReadSecretUsualJsonSample_Runs()
    {
        Assert.True(IsKnownClient(Body, SecretHash));
        Assert.False(
            IsKnownClient(
                Encoding.UTF8.GetBytes("""{"client_id":"reports","client_secret":"guess"}"""),
                SecretHash
            )
        );
    }

    [Fact]
    public void SiteReadSecretJsonSample_Runs()
    {
        // begin-snippet: SiteReadSecretJson
        static bool IsKnownClient(ReadOnlySpan<byte> body, byte[] secretHash)
        {
            var reader = new Utf8JsonReader(body);
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;
                if (reader.CurrentDepth != 1 || !reader.ValueTextEquals("client_secret"u8))
                    continue;
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                    return false;

                // The escaped length is an upper bound for the unescaped length.
                using IMemoryOwner<byte> owner = BufferFactory.Rent(
                    reader.ValueSpan.Length,
                    isSensitive: true,
                    out Span<byte> buffer
                );
                int length = reader.CopyString(buffer);
                ReadOnlySpan<byte> secret = buffer[..length];

                Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
                SHA256.HashData(secret, hash);
                return CryptographicOperations.FixedTimeEquals(hash, secretHash);
            }
            return false;
        }
        // end-snippet

        Assert.True(IsKnownClient(Body, SecretHash));
        Assert.False(IsKnownClient("""{"client_secret":"guess"}"""u8, SecretHash));
        Assert.False(IsKnownClient("""{"client_id":"reports"}"""u8, SecretHash));
        Assert.False(
            IsKnownClient("""{"nested":{"client_secret":"s3cr3t-Qx9vT2mZ"}}"""u8, SecretHash)
        );
        Assert.False(IsKnownClient("""{"client_secret":42}"""u8, SecretHash));

        // An escaped value is unescaped into the buffer: - is the hyphen.
        Assert.True(IsKnownClient("""{"client_secret":"s3cr3t-Qx9vT2mZ"}"""u8, SecretHash));
    }

    [Fact]
    public void SiteReadSecretBase64Sample_Runs()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        string header = Convert.ToBase64String(key);

        // begin-snippet: SiteReadSecretBase64
        // Rent the most the text can decode to, then slice to what was written.
        using IMemoryOwner<byte> owner = BufferFactory.Rent(
            Base64.GetMaxDecodedFromUtf8Length(header.Length),
            isSensitive: true,
            out Span<byte> buffer
        );
        if (!Convert.TryFromBase64Chars(header, buffer, out int length))
            throw new FormatException("The value is not base64.");

        ReadOnlySpan<byte> secret = buffer[..length];
        // end-snippet

        Assert.True(secret.SequenceEqual(key));

        // The other decoders the page names write into the same rented buffer, and report what they wrote.
        buffer.Clear();
        Assert.True(
            Base64Url.TryDecodeFromChars(Base64Url.EncodeToString(key), buffer, out length)
        );
        Assert.True(buffer[..length].SequenceEqual(key));

        buffer.Clear();
        OperationStatus status = Base64.DecodeFromUtf8(
            Encoding.UTF8.GetBytes(header),
            buffer,
            out _,
            out length
        );
        Assert.Equal(OperationStatus.Done, status);
        Assert.True(buffer[..length].SequenceEqual(key));

        buffer.Clear();
        status = Convert.FromHexString(Convert.ToHexString(key), buffer, out _, out length);
        Assert.Equal(OperationStatus.Done, status);
        Assert.True(buffer[..length].SequenceEqual(key));

        Assert.False(Convert.TryFromBase64Chars("not base64!", buffer, out _));
    }

    [Fact]
    public void SiteReadSecretCharsSample_Runs()
    {
        byte[] password = Encoding.UTF8.GetBytes("correct horse battery staple é");
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string encryptedPem = original.ExportEncryptedPkcs8PrivateKeyPem(
            Encoding.UTF8.GetString(password),
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000)
        );

        // begin-snippet: SiteReadSecretChars
        static ECDsa ImportKey(ReadOnlySpan<char> pem, ReadOnlySpan<byte> utf8Password)
        {
            // Throws on bytes that are not UTF-8, before it copies any.
            int length = SecureEncoding.UTF8.GetCharCount(utf8Password);

            using IMemoryOwner<char> owner = BufferFactory.Rent(
                length,
                isSensitive: true,
                out Span<char> password
            );
            SecureEncoding.UTF8.GetChars(utf8Password, password);

            var key = ECDsa.Create();
            try
            {
                key.ImportFromEncryptedPem(pem, password);
                return key;
            }
            catch
            {
                key.Dispose();
                throw;
            }
        }
        // end-snippet

        using ECDsa imported = ImportKey(encryptedPem, password);
        Assert.Equal(original.ExportSubjectPublicKeyInfo(), imported.ExportSubjectPublicKeyInfo());

        byte[] invalid = [.. "correct horse"u8, 0xFF];
        var error = Assert.Throws<DecoderFallbackException>(() => ImportKey(encryptedPem, invalid));

        // The exception carries the bytes it rejected, so logging it logs part of the secret.
        Assert.Equal([0xFF], error.BytesUnknown);
        Assert.Contains("[FF]", error.Message, StringComparison.Ordinal);

        // GetMaxCharCount is an upper bound, not the count: for UTF-8 it is one more than the bytes.
        Assert.Equal(password.Length + 1, SecureEncoding.UTF8.GetMaxCharCount(password.Length));
        Assert.True(SecureEncoding.UTF8.GetCharCount(password) < password.Length + 1);
    }
}
