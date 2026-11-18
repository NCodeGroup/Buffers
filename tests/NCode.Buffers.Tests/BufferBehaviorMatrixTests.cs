// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace NCode.Buffers.Tests;

// The class checks the site's buffers.json against the public API of several types, so it
// mirrors no single type and groups its tests by check.
[CollectionDefinition(nameof(BufferBehaviorMatrixTests), DisableParallelization = true)]
[Collection(nameof(BufferBehaviorMatrixTests))]
public class BufferBehaviorMatrixTests
{
    private const int Size = 32;
    private const byte Fill = 0xA5;

    public static TheoryData<string> OutcomeIds { get; } =
    [
        "rent-span",
        "rent-memory",
        "pooled-writer",
        "secure-pool",
        "pinned-array",
        "secure-lifetime",
        "plain-pooled",
        "array-writer",
        "fixed-span-writer",
        "fixed-memory-writer",
    ];

    #region Outcome Checks

    private static void CheckRentSpan(bool pinned, bool zeroedOnDispose)
    {
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        var owner = BufferFactory.Rent(Size, isSensitive: true, out Span<byte> span);
        span.Fill(Fill);
        var view = owner.Memory[..Size];

        AssertPinned(pinned, view);
        owner.Dispose();

        AssertContentsAfterDispose(view, zeroedOnDispose);
    }

    private static void CheckRentMemory(bool pinned, bool zeroedOnDispose)
    {
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        var owner = BufferFactory.Rent(Size, isSensitive: true, out Memory<byte> memory);
        memory.Span.Fill(Fill);
        var view = memory;

        AssertPinned(pinned, view);
        owner.Dispose();

        AssertContentsAfterDispose(view, zeroedOnDispose);
    }

    private static void CheckPlainPooled(bool pinned, bool zeroedOnDispose)
    {
        var owner = BufferFactory.Rent(Size, isSensitive: false, out Memory<byte> memory);
        memory.Span.Fill(Fill);
        var view = memory;

        AssertPinned(pinned, view);
        owner.Dispose();

        AssertContentsAfterDispose(view, zeroedOnDispose);
    }

    private static void CheckPooledWriter(bool pinned, bool zeroedOnDispose)
    {
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        var span = writer.GetSpan(Size);
        span[..Size].Fill(Fill);
        writer.Advance(Size);
        var view = writer.AsReadOnlySequence.First;

        AssertPinned(pinned, view);
        writer.Dispose();

        AssertContentsAfterDispose(view[..Size], zeroedOnDispose);
    }

    private static void CheckSecurePool(bool pinned, bool zeroedOnDispose)
    {
        using var pool = new SecureMemoryPool<byte>();
        var owner = pool.Rent(Size);
        var view = owner.Memory[..Size];
        view.Span.Fill(Fill);

        AssertPinned(pinned, view);
        owner.Dispose();

        AssertContentsAfterDispose(view, zeroedOnDispose);
    }

    private static void CheckPinnedArray(bool pinned, bool zeroedOnDispose)
    {
        var lifetime = BufferFactory.CreatePinnedArray(Size);
        var array = lifetime.PinnedArray;
        array.AsSpan().Fill(Fill);

        AssertPinned(pinned, array);
        lifetime.Dispose();

        AssertContentsAfterDispose(array, zeroedOnDispose);
    }

    private static void CheckSecureLifetime(bool pinned, bool zeroedOnDispose)
    {
        var array = new byte[Size];
        array.AsSpan().Fill(Fill);
        var lifetime = array.AsSpan().GetSecureLifetime();

        AssertPinned(pinned, array);
        lifetime.Dispose();

        AssertContentsAfterDispose(array, zeroedOnDispose);
    }

    private static void CheckArrayWriter(bool pinned, bool zeroedOnDispose)
    {
        var writer = BufferFactory.CreateArrayBufferWriter();
        var span = writer.GetSpan(Size);
        span[..Size].Fill(Fill);
        writer.Advance(Size);

        AssertPinned(pinned, writer.WrittenMemory);

        var writerType = writer.GetType();
        var isDisposable = typeof(IDisposable).IsAssignableFrom(writerType);

        Assert.False(zeroedOnDispose);
        Assert.False(isDisposable);
    }

    private static void CheckFixedSpanWriter(bool pinned, bool zeroedOnDispose)
    {
        var array = new byte[Size];
        var writer = new FixedSpanBufferWriter<byte>(array);
        var span = writer.GetSpan(Size);
        span[..Size].Fill(Fill);
        writer.Advance(Size);

        AssertPinned(pinned, array);

        var writerType = typeof(FixedSpanBufferWriter<byte>);
        var isDisposable = typeof(IDisposable).IsAssignableFrom(writerType);

        Assert.False(zeroedOnDispose);
        Assert.False(isDisposable);
    }

    private static void CheckFixedMemoryWriter(bool pinned, bool zeroedOnDispose)
    {
        var array = new byte[Size];
        var writer = new FixedMemoryBufferWriter<byte>(array);
        var span = writer.GetSpan(Size);
        span[..Size].Fill(Fill);
        writer.Advance(Size);

        AssertPinned(pinned, array);

        var writerType = writer.GetType();
        var isDisposable = typeof(IDisposable).IsAssignableFrom(writerType);

        Assert.False(zeroedOnDispose);
        Assert.False(isDisposable);
    }

    #endregion

    #region Helper Methods

    // The runtime offers no way to show that an array is not pinned (GC.GetGeneration of an
    // ordinary array can also be GC.MaxGeneration), so a `pinned: false` row is not asserted.
    // Every row whose `pinned` is true tests a freshly allocated array: the rows that rent from
    // SecureMemoryPool<byte>.Shared clear its queue first, so the rent allocates a new page. A
    // freshly allocated ordinary array is in generation 0, so GC.MaxGeneration tells a pinned
    // page from an ordinary one.
    private static void AssertPinned(bool pinned, byte[] array)
    {
        if (!pinned)
            return;

        // The GC reports pinned-object-heap objects as its oldest generation.
        var generation = GC.GetGeneration(array);
        Assert.Equal(GC.MaxGeneration, generation);
    }

    private static void AssertPinned(bool pinned, ReadOnlyMemory<byte> memory)
    {
        if (!pinned)
            return;

        var hasArray = MemoryMarshal.TryGetArray(memory, out var segment);
        Assert.True(hasArray);
        Assert.NotNull(segment.Array);
        AssertPinned(pinned, segment.Array);
    }

    private static void AssertContentsAfterDispose(ReadOnlyMemory<byte> view, bool zeroedOnDispose)
    {
        Assert.Equal(Size, view.Length);

        var expected = zeroedOnDispose ? (byte)0 : Fill;
        var contents = view.ToArray();
        Assert.All(contents, value => Assert.Equal(expected, value));
    }

    private static bool ReadFlag(JsonNode outcome, string name)
    {
        var flag = outcome["behavior"]?[name];

        Assert.NotNull(flag);
        return flag.GetValue<bool>();
    }

    private static JsonNode[] ReadOutcomes()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var path = Path.Combine(baseDirectory, "buffers.json");
        var json = File.ReadAllText(path);
        var root = JsonNode.Parse(json);

        var outcomes = root?["outcomes"]?.AsArray();
        Assert.NotNull(outcomes);

        return
        [
            .. outcomes.Select(outcome =>
            {
                Assert.NotNull(outcome);
                return outcome;
            }),
        ];
    }

    #endregion

    #region Behavior Tests

    [Fact]
    public void OutcomeIds_WhenReadFromBuffersJson_MatchTheTheoryRows()
    {
        var expected = OutcomeIds
            .Cast<object[]>()
            .Select(row => Assert.IsType<string>(row[0]))
            .Order()
            .ToArray();

        var actual = ReadOutcomes()
            .Select(outcome => Assert.IsAssignableFrom<JsonValue>(outcome["id"]).GetValue<string>())
            .Order()
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(OutcomeIds))]
    public void Behavior_ForEachOutcome_MatchesBuffersJson(string id)
    {
        var outcome = ReadOutcomes().Single(node => node["id"]?.GetValue<string>() == id);
        var pinned = ReadFlag(outcome, "pinned");
        var zeroedOnDispose = ReadFlag(outcome, "zeroedOnDispose");

        switch (id)
        {
            case "rent-span":
                CheckRentSpan(pinned, zeroedOnDispose);
                break;
            case "rent-memory":
                CheckRentMemory(pinned, zeroedOnDispose);
                break;
            case "pooled-writer":
                CheckPooledWriter(pinned, zeroedOnDispose);
                break;
            case "secure-pool":
                CheckSecurePool(pinned, zeroedOnDispose);
                break;
            case "pinned-array":
                CheckPinnedArray(pinned, zeroedOnDispose);
                break;
            case "secure-lifetime":
                CheckSecureLifetime(pinned, zeroedOnDispose);
                break;
            case "plain-pooled":
                CheckPlainPooled(pinned, zeroedOnDispose);
                break;
            case "array-writer":
                CheckArrayWriter(pinned, zeroedOnDispose);
                break;
            case "fixed-span-writer":
                CheckFixedSpanWriter(pinned, zeroedOnDispose);
                break;
            case "fixed-memory-writer":
                CheckFixedMemoryWriter(pinned, zeroedOnDispose);
                break;
            default:
                Assert.Fail($"No check is written for the outcome '{id}'.");
                break;
        }
    }

    #endregion
}
