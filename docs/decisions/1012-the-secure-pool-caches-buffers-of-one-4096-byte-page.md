# 1012. The secure pool caches buffers of one 4096-byte page

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

`SecureMemoryPool<T>.PageSize` is the public constant 4096. Its XML documentation says "4096 bytes, which matches the
page size on most operating systems". That is the one reason the repository records.

`Rent(0)` returns `EmptyMemory<T>.Singleton`. `Rent()`, `Rent(-1)` and any request of at most 4096 bytes get a pooled
buffer of `max(1, 4096 / sizeof(T))` elements, from the cache or newly allocated. A request above 4096 bytes gets a
pinned array of exactly that many elements of its own, zeroed on `Dispose` and never cached. A request below -1 throws
`ArgumentOutOfRangeException`. The byte count is computed as a `long`.

`BufferFactory.Rent` slices its `out` span or memory to the requested length. The owner's `Memory` is the whole buffer.

Why the pool caches one size and not several is not recorded.

## Decision

**Pooled secure buffers are one size, 4096 bytes; a larger request gets a pinned array of its own.**

## Options considered

- Several buffer sizes. Not chosen; no reason is recorded.
- A page size the caller sets. Not chosen; no reason is recorded.
- One size of 4096 bytes (chosen).

## Consequences

- A 32-byte secret holds a 4096-byte pinned buffer, and `Dispose` zeroes all of it.
- Every request above a page allocates.
- `owner.Memory` is 4096 bytes for a small request.

## References

- `src/NCode.Buffers/SecureMemoryPool.cs`
- `SecureMemoryPoolTests.Rent_SizeLessThanPage_ReturnsPageSizeBuffer`,
  `SecureMemoryPoolTests.Rent_SizeMoreThanPage_ReturnsExactSizeBuffer` and
  `SecureMemoryPoolTests.Rent_ElementLargerThanPage_ReturnsOneElement`
- [CHANGELOG 5.0.0](../../CHANGELOG.md)
