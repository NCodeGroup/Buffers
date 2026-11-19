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

    /// <summary>
    /// Gets the one meter, named <see cref="BufferMetrics.MeterName"/>, that owns the instruments.
    /// </summary>
    private static Meter Meter { get; } = new(BufferMetrics.MeterName);

    /// <summary>
    /// Gets the counter of rentals, tagged with the source of each.
    /// </summary>
    private static Counter<long> Rentals { get; } =
        Meter.CreateCounter<long>(
            "ncode.buffers.secure_pool.rentals",
            "{buffer}",
            "The number of buffers the secure memory pool rented."
        );

    /// <summary>
    /// Gets the counter of buffers returned to the pool's cache.
    /// </summary>
    private static Counter<long> Returns { get; } =
        Meter.CreateCounter<long>(
            "ncode.buffers.secure_pool.returns",
            "{buffer}",
            "The number of buffers returned to the secure memory pool's cache."
        );

    /// <summary>
    /// Gets the counter of cached buffers that a high-pressure trim dropped.
    /// </summary>
    private static Counter<long> Trimmed { get; } =
        Meter.CreateCounter<long>(
            "ncode.buffers.secure_pool.trimmed",
            "{buffer}",
            "The number of cached buffers the secure memory pool dropped when memory pressure was high."
        );

    /// <summary>
    /// Records one rental.
    /// </summary>
    /// <param name="source">One of <see cref="Cache"/>, <see cref="New"/>, <see cref="Unpooled"/> or <see cref="Empty"/>.</param>
    internal static void RecordRental(string source)
    {
        var tag = new KeyValuePair<string, object?>(SourceTag, source);
        Rentals.Add(1, tag);
    }

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
