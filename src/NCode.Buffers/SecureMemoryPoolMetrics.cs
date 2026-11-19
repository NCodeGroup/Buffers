// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace NCode.Buffers;

/// <summary>
/// Owns the one static <see cref="Meter"/> and the instruments through which <see cref="SecureMemoryPool{T}"/>
/// reports its metrics.
/// </summary>
// A static meter, not IMeterFactory: ADR-1030 explains why the pool has no host to take a factory from.
internal static class SecureMemoryPoolMetrics
{
    /// <summary>
    /// The attribute key of the source of a rental.
    /// </summary>
    internal const string SourceTag = "ncode.buffers.rental.source";

    /// <summary>
    /// The source of a rental that took a buffer from the pool's cache.
    /// </summary>
    internal const string Cache = "cache";

    /// <summary>
    /// The source of a rental that allocated a new pooled buffer.
    /// </summary>
    internal const string New = "new";

    /// <summary>
    /// The source of a rental that allocated a buffer above the page size, which no pool caches.
    /// </summary>
    internal const string Unpooled = "unpooled";

    /// <summary>
    /// The source of a rental of zero elements, which gets the shared empty buffer.
    /// </summary>
    internal const string Empty = "empty";

    private static readonly Meter Meter = new(BufferMetrics.MeterName);

    private static readonly Counter<long> Rentals = Meter.CreateCounter<long>(
        "ncode.buffers.secure_pool.rentals",
        "{buffer}",
        "The number of buffers the secure memory pool rented."
    );

    private static readonly Counter<long> Returns = Meter.CreateCounter<long>(
        "ncode.buffers.secure_pool.returns",
        "{buffer}",
        "The number of buffers returned to the secure memory pool's cache."
    );

    private static readonly Counter<long> Trimmed = Meter.CreateCounter<long>(
        "ncode.buffers.secure_pool.trimmed",
        "{buffer}",
        "The number of cached buffers the secure memory pool dropped when memory pressure was high."
    );

    /// <summary>
    /// Records one rental.
    /// </summary>
    /// <param name="source">One of <see cref="Cache"/>, <see cref="New"/>, <see cref="Unpooled"/> or <see cref="Empty"/>.</param>
    internal static void RecordRental(string source) =>
        Rentals.Add(1, new KeyValuePair<string, object?>(SourceTag, source));

    /// <summary>
    /// Records one buffer returned to the pool's cache.
    /// </summary>
    internal static void RecordReturn() => Returns.Add(1);

    /// <summary>
    /// Records the cached buffers a high-pressure trim dropped.
    /// </summary>
    /// <param name="count">The number of cached buffers the trim dropped.</param>
    internal static void RecordTrimmed(int count) => Trimmed.Add(count);
}
