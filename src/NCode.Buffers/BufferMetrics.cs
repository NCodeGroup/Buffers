// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Names the <see cref="System.Diagnostics.Metrics.Meter"/> through which NCode.Buffers reports its metrics.
/// </summary>
/// <remarks>
/// <para>
/// The meter is named <see cref="MeterName"/>, and one meter serves the whole process. It has three counters with the
/// unit <c>{buffer}</c>:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>ncode.buffers.secure_pool.rentals</c> counts the buffers <see cref="SecureMemoryPool{T}"/> rents. Its attribute
/// <c>ncode.buffers.rental.source</c> is <c>cache</c> for a buffer taken from the pool's cache, <c>new</c> for a new
/// pooled buffer, <c>unpooled</c> for a buffer above <see cref="SecureMemoryPool{T}.PageSize"/> bytes, and
/// <c>empty</c> for a request of zero elements.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>ncode.buffers.secure_pool.returns</c> counts the buffers a disposed owner returns to the pool's cache.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>ncode.buffers.secure_pool.trimmed</c> counts the cached buffers the pool drops when memory pressure is high.
/// </description>
/// </item>
/// </list>
/// <para>
/// A host subscribes to the meter with OpenTelemetry's <c>AddMeter(BufferMetrics.MeterName)</c>. Recording costs nothing
/// until a listener subscribes.
/// </para>
/// </remarks>
[PublicAPI]
public static class BufferMetrics
{
    /// <summary>
    /// The name of the meter through which NCode.Buffers reports its metrics.
    /// </summary>
    /// <value>The value is <c>NCode.Buffers</c>.</value>
    public const string MeterName = "NCode.Buffers";
}
