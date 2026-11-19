// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/watch-the-secure-pool.mdx. The site's build includes each begin-snippet region in
// the page by its key (ADR-1010). The class runs alone because a listener on the meter counts every secure pool in
// the process.
[CollectionDefinition(nameof(WatchTheSecurePoolSamples), DisableParallelization = true)]
[Collection(nameof(WatchTheSecurePoolSamples))]
public class WatchTheSecurePoolSamples
{
    [Fact]
    public void SiteWatchPoolListenerSample_Runs()
    {
        // begin-snippet: SiteWatchPoolListener
        var counts = new ConcurrentDictionary<string, long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == BufferMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, state) =>
                counts.AddOrUpdate(instrument.Name, value, (_, total) => total + value)
        );
        listener.Start();
        // end-snippet

        using var pool = new SecureMemoryPool<byte>();
        pool.Rent(16).Dispose();

        var rentals = counts.GetValueOrDefault("ncode.buffers.secure_pool.rentals");
        Assert.True(rentals >= 1);
    }

    [Fact]
    public void SiteWatchPoolCheckSample_Runs()
    {
        var counts = new ConcurrentDictionary<string, long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == BufferMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, state) =>
                counts.AddOrUpdate(instrument.Name, value, (_, total) => total + value)
        );
        listener.Start();

        // begin-snippet: SiteWatchPoolCheck
        using var pool = new SecureMemoryPool<byte>();

        pool.Rent(16).Dispose();
        pool.Rent(16).Dispose();

        listener.RecordObservableInstruments();
        // end-snippet

        var rentals = counts.GetValueOrDefault("ncode.buffers.secure_pool.rentals");
        Assert.True(rentals >= 2);
        var returns = counts.GetValueOrDefault("ncode.buffers.secure_pool.returns");
        Assert.True(returns >= 1);
    }
}
