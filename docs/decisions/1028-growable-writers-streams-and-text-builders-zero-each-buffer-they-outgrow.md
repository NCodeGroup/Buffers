# 1028. Growable writers, streams and text builders zero each buffer they outgrow

- **Status:** Accepted
- **Date:** 2026-11-19
- **Deciders:** NCode Group

## Context

`ArrayBufferWriter<T>`, `StringBuilder` and `MemoryStream` grow by copying into a larger array and dropping the old
one. Nothing zeroes the dropped array, so every secret written before a growth stays on the managed heap, readable,
until the garbage collector reuses its memory.

`Sequence<T>` grows by segments, so no single span covers its content. A hash or a cipher that needs one span needs a
copy of the content first.

The BCL's `ValueStringBuilder`, which builds text in a pooled or stack buffer, is internal, so callers can't use it.

`Stream`'s base `Read(Span<byte>)`, `Write(ReadOnlySpan<byte>)`, `ReadByte`, `WriteByte` and `CopyTo` copy through
`ArrayPool<byte>.Shared` arrays, which nothing zeroes. A stream that holds a secret and doesn't override them leaks
the secret into the shared pool.

## Decision

**A growable writer, stream or text builder keeps its content in one contiguous buffer rented from
`BufferFactory.ChoosePool<T>`, and when it outgrows that buffer it copies the content into a larger one and disposes
the old owner, which zeroes the old buffer when the writer is sensitive.**

- `ContiguousBufferWriter<T>` is an `IBufferWriter<T>` over one buffer. It rents lazily on the first `GetSpan` or
  `GetMemory`, and grows to the largest of the required size, its initial capacity or 256, and twice its capacity,
  capped at `Array.MaxLength`. `Reset` zeroes the written part and keeps the buffer.
- `PooledMemoryStream` is a seekable, readable and writable `Stream` over one such buffer. It grows the same way, and it
  overrides every `Stream` member that the base class implements through `ArrayPool<byte>.Shared`. Writing past the
  end zero-fills the gap, and `SetLength` zeroes the bytes it cuts off.
- `PooledTextBuilder` is a `ref struct` that builds text in a caller's `Span<char>` or in a rented buffer. It grows the
  same way, and leaving a caller's span zeroes all of it, not only the written part, when it is sensitive.
- A growth rents the new buffer and copies into it before it releases the old one, so an input that is the writer's own
  written span stays valid for the call that grows it.
- A span or memory from a writer is invalid after the next `GetSpan` or `GetMemory`, because growth zeroes the old
  buffer. The documentation says so.

## Options considered

- Keep segments only, with no single span. Rejected: a hash or a cipher that needs one span copies the content into an
  array that nothing zeroes.
- Wrap the BCL types. Rejected: `ArrayBufferWriter<T>`, `StringBuilder` and `MemoryStream` can't zero the arrays they
  drop when they grow.
- Write own types that rent from `ChoosePool<T>` and dispose each outgrown owner (chosen). The pool and the owner
  zero the buffer, as they do for every other rental from the package.

## Consequences

- Above 4096 bytes every growth allocates a pinned array of its own, because the secure pool caches only one page
  size (ADR-1012).
- With `isSensitive: false` the outgrown buffers go back to `MemoryPool<T>.Shared` unzeroed.
- `PooledTextBuilder` is a `ref struct`, so it can't cross an `await`, and a copy of it shares its buffer, so it is
  passed by `ref` and never copied.
- `PooledTextBuilder.ToString()` makes a `string`, which can't be zeroed. Its documentation says so.
- A new growable type that holds a secret follows the same rule.

## References

- [`src/NCode.Buffers/BufferFactory.cs`](../../src/NCode.Buffers/BufferFactory.cs): `BufferFactory.ChoosePool<T>`, the
  source of every rented buffer.
- [ADR-1012](1012-the-secure-pool-caches-buffers-of-one-4096-byte-page.md): the secure pool caches one page size, so
  larger buffers are allocated pinned.
- [ADR-1027](1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md): helpers that rent their
  output and return its owner.
