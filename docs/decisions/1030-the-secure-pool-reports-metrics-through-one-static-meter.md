# 1030. The secure pool reports metrics through one static meter

- **Status:** Accepted
- **Date:** 2026-11-19
- **Deciders:** NCode Group
- **Supersedes:** project-template ADR-0113, for the rule that meters come from `IMeterFactory`

## Context

A host that runs the secure pool wants to know how many buffers it rents, how many go back and how many a trim drops,
so that it can size the pool and see a leak. `System.Diagnostics.Metrics` is the platform's way to report that, and
OpenTelemetry reads it.

Project-template ADR-0113 creates a project's meters from `IMeterFactory`, never `new Meter(...)`, so that two hosts in
one process never share instruments. This library has no dependency injection and no host. `SecureMemoryPool<T>.Shared`
is static and exists before any host does, and `BufferFactory` takes its pool from it with no host to ask for a
factory.

## Decision

**The secure pool reports its metrics through one static `Meter` named `NCode.Buffers`, which an internal class owns;
the name is a public constant, `BufferMetrics.MeterName`.**

- `public static class BufferMetrics` holds `public const string MeterName = "NCode.Buffers"`, so a host subscribes to
  the meter by that constant.
- An internal `SecureMemoryPoolMetrics` owns the one static `Meter` and the instruments, and the pool's call sites call
  its methods, never `Meter` APIs.
- Three `Counter<long>`s with the unit `{buffer}`:
  - `ncode.buffers.secure_pool.rentals`, with the attribute `ncode.buffers.rental.source`, whose value is `cache`,
    `new`, `unpooled` or `empty`;
  - `ncode.buffers.secure_pool.returns`;
  - `ncode.buffers.secure_pool.trimmed`, which adds the number of cached buffers a high-pressure trim drops.
- Names are lowercase and dotted, units are UCUM annotations and the one attribute has four values, so no instrument's
  series depends on the data (project-template ADR-0113's other rules).
- Tests read the instruments with `MetricCollector<long>` from `Microsoft.Extensions.Diagnostics.Testing`, in the test
  project only.

## Options considered

- An `IMeterFactory` parameter on `SecureMemoryPool<T>`, as project-template ADR-0113 says. Rejected: `Shared` and
  `BufferFactory` have no host to take a factory from, so the parameter would be null on the path almost every caller
  uses.
- A static `Meter` (chosen). The meter exists with the static pool, and a host needs no setup to listen to it.

## Consequences

- There is one meter per process, shared by every host in it. Two hosts in one process, such as integration tests,
  see each other's rentals through the same instruments.
- A test that reads the instruments runs in a collection without parallelization, so another test's rentals don't
  reach its collector.
- Recording costs nothing until a listener subscribes to the meter.
- The library takes no telemetry SDK: it uses `System.Diagnostics.Metrics`, which the platform provides.
- A later instrument of this library joins the same meter, so the name `NCode.Buffers` stays the one a host subscribes
  to.

## References

- [Project-template ADR-0113](project-template/0113-metrics-have-one-owner-per-signal-and-bounded-dimensions.md)
- `AGENTS.md`, which lists this override
- `docs/metrics.md`, which lists the instruments
