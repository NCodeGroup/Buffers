# 1018. The library neither locks nor encrypts memory

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

The library allocates memory and writes zeros to it. It calls no operating system memory API.

`ROADMAP.md`'s Not Planned section lists "Locking memory into RAM (`mlock`, `VirtualLock`) or hiding it from a debugger
or crash dump".

The reasons the repository states are in `docs/site/start/limits.mdx`:

- Locking pages "is an operating system call; the project does not plan it".
- Linux's [mlock(2)](https://man7.org/linux/man-pages/man2/mlock.2.html) page warns that suspend mode "will save a copy
  of the system's RAM to disk, regardless of memory locks" (read 2026-11-16).
- "A key that decrypts memory is one more secret in that memory."

## Decision

**The library does not lock pages into RAM, hide memory from a debugger or a dump, or encrypt memory.**

## Options considered

- `VirtualLock` or `mlock` on pooled buffers. Not chosen: it is an operating system call, and suspend saves locked
  pages too.
- Encrypting buffers in memory. Not chosen: the key would be one more secret in memory.
- Neither (chosen).

## Consequences

- A page file, swap or a hibernation file can hold a secret.
- A debugger or a dump reads memory while a secret is there.
- A secret that must not be readable in use belongs outside the process.

## References

- `ROADMAP.md`
- `docs/site/start/limits.mdx`
- [mlock(2)](https://man7.org/linux/man-pages/man2/mlock.2.html)
- [VirtualLock](https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-virtuallock)
