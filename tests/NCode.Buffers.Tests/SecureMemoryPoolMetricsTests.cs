// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace NCode.Buffers.Tests;

// The meter is static and process-wide, so the class runs alone, after the parallel test classes,
// and no other test records on the meter while these tests run.
[CollectionDefinition(nameof(SecureMemoryPoolMetricsTests), DisableParallelization = true)]
[Collection(nameof(SecureMemoryPoolMetricsTests))]
public class SecureMemoryPoolMetricsTests
{
    private const int PageSizeRequest = 16;
    private const int UnpooledRequest = 5000;

    #region Rent Tests

    [Fact]
    public void Rent_RecordsOneRentalWithItsSource()
    {
        using var pool = new SecureMemoryPool<byte>();
        using var collector = new MetricCollector<long>(
            null,
            BufferMetrics.MeterName,
            "ncode.buffers.secure_pool.rentals"
        );

        pool.Rent(PageSizeRequest).Dispose();
        using var cached = pool.Rent(PageSizeRequest);
        using var unpooled = pool.Rent(UnpooledRequest);
        using var empty = pool.Rent(0);

        var sources = collector
            .GetMeasurementSnapshot()
            .Select(measurement => measurement.Tags[SecureMemoryPoolMetrics.SourceTag])
            .ToArray();

        Assert.Equal(
            [
                SecureMemoryPoolMetrics.New,
                SecureMemoryPoolMetrics.Cache,
                SecureMemoryPoolMetrics.Unpooled,
                SecureMemoryPoolMetrics.Empty,
            ],
            sources
        );
        Assert.Equal("{buffer}", collector.Instrument?.Unit);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_OfAPooledBuffer_RecordsAReturn()
    {
        using var pool = new SecureMemoryPool<byte>();
        using var collector = new MetricCollector<long>(
            null,
            BufferMetrics.MeterName,
            "ncode.buffers.secure_pool.returns"
        );

        pool.Rent(PageSizeRequest).Dispose();

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
    }

    #endregion

    #region TrimMemory Tests

    [Fact]
    public void TrimMemory_UnderPressure_RecordsTheDroppedBuffers()
    {
        using var pool = new SecureMemoryPool<byte>();
        using var collector = new MetricCollector<long>(
            null,
            BufferMetrics.MeterName,
            "ncode.buffers.secure_pool.trimmed"
        );
        pool.HighPressureThreshold = 0;
        var first = pool.Rent(PageSizeRequest);
        var second = pool.Rent(PageSizeRequest);
        first.Dispose();
        second.Dispose();

        _ = pool.TrimMemory();

        // The Gen2 GC callback can also trim this pool, so assert that a trim dropped both pages.
        Assert.Contains(collector.GetMeasurementSnapshot(), measurement => measurement.Value == 2);
    }

    #endregion
}
