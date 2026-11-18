// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/split-text.mdx. The site's build includes each begin-snippet region in the page by
// its key (ADR-1010). Every comparison is ordinal.
public class SplitTextSamples
{
    [Fact]
    public void SiteSplitTextUsualSample_Runs()
    {
        // begin-snippet: SiteSplitTextUsual
        const string header = "theme=dark;lang=en;id=42";

        // Each Split allocates an array and a string per piece.
        string[] parts = header.Split(';');
        string[] firstPair = parts[0].Split('=');
        // end-snippet

        Assert.Equal(["theme=dark", "lang=en", "id=42"], parts);
        Assert.Equal(["theme", "dark"], firstPair);
    }

    [Fact]
    public void SiteSplitTextHeaderSample_Runs()
    {
        // begin-snippet: SiteSplitTextHeader
        static ReadOnlyMemory<char> FindValue(string list, ReadOnlySpan<char> name)
        {
            foreach (var segment in list.SplitSegments(';'))
            {
                ReadOnlySpan<char> piece = segment.Memory.Span;
                var equals = piece.IndexOf('=');
                if (equals < 0)
                    continue;

                // The pieces are not trimmed: a leading space is part of the name.
                if (piece[..equals].Equals(name, StringComparison.Ordinal))
                    return segment.Memory[(equals + 1)..];
            }

            return default;
        }
        // end-snippet

        const string List = "theme=dark;lang=en;id=42";

        string lang = FindValue(List, "lang").ToString();
        Assert.Equal("en", lang);
        string id = FindValue(List, "id").ToString();
        Assert.Equal("42", id);
        ReadOnlyMemory<char> missing = FindValue(List, "missing");
        Assert.True(missing.IsEmpty);
        ReadOnlyMemory<char> padded = FindValue(" lang=en", "lang");
        Assert.True(padded.IsEmpty);
    }

    [Fact]
    public void SiteSplitTextTokenSample_Runs()
    {
        // begin-snippet: SiteSplitTextToken
        static bool TrySplitToken(
            string token,
            out ReadOnlyMemory<char> signingInput,
            out ReadOnlyMemory<char> signature
        )
        {
            signingInput = default;
            signature = default;

            // It splits; it does not validate the token.
            StringSegments parts = token.SplitSegments('.');
            if (parts.Count != 3)
                return false;

            if (
                parts.First is not { } header
                || header.Next is not { } payload
                || payload.Next is not { } last
            )
            {
                return false;
            }

            // The signing input is one slice of the original: header, the dot, payload.
            var length = header.Memory.Length + 1 + payload.Memory.Length;
            signingInput = parts.Original[..length];
            signature = last.Memory;
            return true;
        }
        // end-snippet

        bool split = TrySplitToken(
            "aaa.bbb.ccc",
            out var signingInput,
            out var signature
        );
        Assert.True(split);
        string signingText = signingInput.ToString();
        Assert.Equal("aaa.bbb", signingText);
        string signatureText = signature.ToString();
        Assert.Equal("ccc", signatureText);

        bool twoParts = TrySplitToken("aaa.bbb", out _, out _);
        Assert.False(twoParts);
        bool fourParts = TrySplitToken("aaa.bbb.ccc.ddd", out _, out _);
        Assert.False(fourParts);
    }

    [Fact]
    public void SiteSplitTextWalkSample_Runs()
    {
        // begin-snippet: SiteSplitTextWalk
        var parts = "one,two,three".SplitSegments(',');
        var pieces = 0;
        var characters = 0;

        if (!parts.IsEmpty)
        {
            // Walking from First allocates no enumerator.
            for (var s = parts.First; s is not null; s = s.Next)
            {
                ++pieces;
                characters += s.Memory.Length;
            }
        }
        // end-snippet

        Assert.Equal(3, parts.Count);
        Assert.Equal(3, pieces);
        Assert.Equal(11, characters);
    }

    [Fact]
    public void SiteSplitTextSpanSplitSample_Runs()
    {
        // begin-snippet: SiteSplitTextSpanSplit
        const string line = "a,b,,c";
        var nonEmpty = 0;

        // One synchronous pass over ranges of the line: no segment objects.
        foreach (Range r in line.AsSpan().Split(','))
        {
            if (!line.AsSpan(r).IsEmpty)
                ++nonEmpty;
        }
        // end-snippet

        Assert.Equal(3, nonEmpty);
    }

    [Fact]
    public void SiteSequenceSplitJsonSample_Runs()
    {
        // begin-snippet: SiteSequenceSplitJson
        byte[] firstBytes = "{\"sub\":"u8.ToArray();
        byte[] lastBytes = "\"42\"}"u8.ToArray();
        var first = new MemorySegment<byte>(firstBytes);
        var last = first.Append(lastBytes);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        // The reader reads across the two segments as one document.
        string? subject = null;
        var reader = new Utf8JsonReader(sequence);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.String)
                subject = reader.GetString();
        }
        // end-snippet

        Assert.False(sequence.IsSingleSegment);
        Assert.Equal("42", subject);
    }

    [Fact]
    public void SiteSplitTextOverSensitiveBufferSample_Runs()
    {
        const string Secret = "user:pass:otp";

        // begin-snippet: SiteSplitTextOverSensitiveBuffer
        using var owner = BufferFactory.Rent<char>(
            Secret.Length,
            isSensitive: true,
            out Memory<char> buffer
        );
        Secret.CopyTo(buffer.Span);

        // The pieces are slices of the rented buffer, not copies.
        var parts = ((ReadOnlyMemory<char>)buffer).SplitSegments(':');
        // end-snippet

        Assert.Equal(Secret.Length, buffer.Length);
        Assert.Equal(3, parts.Count);

        string[] expected = ["user", "pass", "otp"];
        var index = 0;
        for (var s = parts.First; s is not null; s = s.Next)
        {
            ReadOnlySpan<char> expectedPiece = expected[index].AsSpan();
            SecretAssert.Equal(expectedPiece, s.Memory.Span);
            bool overlaps = buffer.Span.Overlaps(s.Memory.Span, out var offset);
            Assert.True(overlaps);
            Assert.True(offset + s.Memory.Length <= buffer.Length);
            ++index;
        }

        Memory<char> kept = buffer;
        owner.Dispose();

        bool keptHasData = kept.Span.ContainsAnyExcept('\0');
        Assert.False(keptHasData);
    }
}
