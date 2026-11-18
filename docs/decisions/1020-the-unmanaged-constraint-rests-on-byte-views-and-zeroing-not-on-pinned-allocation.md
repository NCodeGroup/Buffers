# 1020. The unmanaged constraint rests on byte views and zeroing, not on pinned allocation

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group
- **Amends:** 1006

## Context

[ADR-1006](1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md) gives two run-time failures as the
reason for `where T : unmanaged`: `GC.AllocateUninitializedArray<T>(length, pinned: true)` and
`MemoryMarshal.AsBytes<T>` throwing `ArgumentException` for a `T` that is or contains a reference. Only the second
holds on the runtime the package targets:

- Microsoft's documentation of `GC.AllocateUninitializedArray<T>` limits the restriction to ".NET 7 and earlier
  versions: If pinned is set to `true`, `T` must not be a reference type or a type that contains object references"
  ([the API page](https://learn.microsoft.com/dotnet/api/system.gc.allocateuninitializedarray), read 2026-11-18). On
  .NET 10.0.15, `GC.AllocateUninitializedArray<string>(10, pinned: true)`, `GC.AllocateArray<string>(10, pinned: true)`
  and the same call for a struct with a `string` field each return an array, which `GC.GetGeneration` reports in
  generation 2, as it reports every object on the Pinned Object Heap.
- `MemoryMarshal.AsBytes<T>` throws `ArgumentException` ("Only value types without pointers or references are
  supported") for such a `T` on .NET 10.0.15. `CryptographicOperations.FixedTimeEquals<T>` views the elements as
  bytes through it, and `CryptographicOperations.ZeroMemory<T>` reinterprets them as bytes and zeroes them through
  `CryptographicOperations.ZeroMemory`
  ([ADR-1016](1016-zeroing-and-fixed-time-comparison-call-cryptographicoperations.md)): a byte view is defined only
  for a `T` without references.
- A reference held in a buffer points at an object outside it. Zeroing the buffer clears the reference and leaves the
  object, where a secret would live, as it is, so a buffer of references cannot keep R1 of
  [docs/assurance-case.md](../assurance-case.md) for the data it refers to.

## Decision

**The secure APIs keep `where T : unmanaged`, as ADR-1006 decides, for two reasons: the library compares and zeroes
memory as bytes, which is defined only for a `T` without references, and zeroing a reference does not clear the
object it refers to.** Pinned allocation is not a reason: on the target framework it accepts any `T`. A document that
explains the constraint gives these two reasons and does not say that a pinned allocation throws.

## Options considered

- Rewrite ADR-1006's Context. Rejected: an accepted record is not rewritten ([README](README.md#statuses)).
- Supersede ADR-1006 whole. Rejected: its decision, its scope and its consequences hold; only its reasons change.
- Relax the constraint to `struct`, since pinned allocation accepts references. Rejected: `MemoryMarshal.AsBytes<T>`
  rejects such a `T` at run time, and zeroing references does not remove a secret.
- Amend ADR-1006 with the reasons that hold (chosen).

## Consequences

- No code changes: every API ADR-1006 lists keeps `unmanaged`, and a `T` with references fails to compile (CS8377).
- ADR-1006's status reads "Accepted (amended by 1020)", and its index row says so.
- [docs/architecture.md](../architecture.md)'s design rule gives the second reason, and the site's design decisions
  page gives both.

## References

- [ADR-1006](1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md): the constraint this record amends.
- [GC.AllocateUninitializedArray](https://learn.microsoft.com/dotnet/api/system.gc.allocateuninitializedarray)
- [MemoryMarshal.AsBytes](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.memorymarshal.asbytes)
- [Unmanaged constraint](https://learn.microsoft.com/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters#unmanaged-constraint)
