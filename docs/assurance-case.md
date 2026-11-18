# Assurance Case

This page states NCode.Buffers' security requirements, the threats it considers, and why the design and the way it is
built meet them. [architecture.md](architecture.md) describes the components; README's
[Security notes](../README.md#security-notes) are the user-facing summary.

## Security Requirements

What the library is expected to do:

1. **R1.** A buffer rented with `isSensitive: true`, and the memory of a secure lifetime, is overwritten with zeros
   when its owner is disposed, including the part beyond the slice the caller asked for.
2. **R2.** A pooled sensitive buffer is zeroed before another caller can rent it.
3. **R3.** A sensitive buffer from the library isn't moved by the garbage collector, so no stale copy is left at an
   earlier address. `SecureSpanLifetime<T>` wraps memory the caller owns and is the exception.
4. **R4.** `FixedTimeEquals` takes the same time for any contents of equal length.
5. **R5.** `SecureEncoding` rejects invalid input with an exception instead of substituting characters.
6. **R6.** A fixed buffer writer never writes past the memory it was given.
7. **R7.** The package consumers install is built from this repository's reviewed code, and they can check that.

What it does not do, and users must not rely on: lock memory into RAM, so the OS can page it out; hide memory from a
debugger, a crash dump or a process that can read this one's memory; zero anything without `Dispose`; or zero copies
the caller makes, such as a `string` or a `ToArray()` result.

## Threat Model

| Threat | In scope | Countered by |
| --- | --- | --- |
| A secret stays readable in freed or reused heap memory | Yes | R1, R2: `CryptographicOperations.ZeroMemory`, which the JIT doesn't remove, on `Dispose` and before reuse |
| The GC compacts the heap and leaves a copy of a secret behind | Yes | R3: sensitive arrays come from `GC.AllocateUninitializedArray<T>(pinned: true)` on the Pinned Object Heap |
| A timing side channel reveals where two MACs differ | Yes | R4: comparison by `CryptographicOperations.FixedTimeEquals`; lengths still leak, as documented |
| Malformed text decodes to a different, valid-looking string | Yes | R5: exception fallbacks in `SecureEncoding` |
| A writer overruns its buffer | Yes | R6: capacity checks in `GetSpan`, `GetMemory` and `Advance`, which throw |
| A tampered or substituted package | Yes | R7, below |
| An attacker who can read the process's memory, or the page file | No | Outside what a managed library can prevent |
| A caller that never disposes, or uses a span after dispose | No | Documented misuse; there is no finalizer to rely on |

**Trust boundary.** The library runs inside the caller's process with the caller's privileges, and every input comes
from the caller. It parses no external format and makes no network, file or OS call beyond memory allocation, so its
attack surface is its public API.

## Secure Design Principles Applied

- **Economy of mechanism:** one assembly, one small dependency, no I/O, no native code.
- **Fail-safe defaults:** analyzers ban raw `ArrayPool`, `MemoryPool` and `Encoding` use in shipping code, so buffers and
  encodings come from the secure entry points (ADR-1003); encodings throw rather than guess.
- **Complete mediation of ownership:** memory is returned only through its owner's `Dispose`, which zeroes it. A
  second `Dispose` of one of the library's owners never throws, and what it does depends on the owner. A pooled
  owner's does nothing: every rental gets an owner of its own, so a second `Dispose` of an earlier rental can't zero or
  return a buffer another caller has rented since. The two secure lifetimes zero their memory again. A
  `RefSpanLease<T>` disposes its owner again, so it does what that owner's second `Dispose` does.
- **Least common mechanism:** secret-holding types accept only unmanaged element types, so they never hold object
  references the zeroing can't clear (ADR-1006).
- **Open design:** the code, the tests, the decision records and this case are public.

## Common Weaknesses Countered

- **CWE-226 / CWE-244, sensitive information in a reused or freed resource:** R1, R2, R3.
- **CWE-208, observable timing discrepancy:** R4.
- **CWE-176 / CWE-172, improper handling of Unicode encoding:** R5.
- **CWE-787 / CWE-120, out-of-bounds write:** R6, with C#'s bounds-checked spans; the library has no `unsafe` blocks or pointers.
- **CWE-494 / CWE-829, untrusted code or inclusion:** R7.

## Evidence

- **Tests:** unit and FsCheck property tests cover zeroing, including of a buffer the pool hands out again, pool
  reuse, writer bounds and encodings; the gate fails under 98% line or branch coverage (`./build/Test-Coverage.ps1`).
- **Static analysis:** the .NET analyzers at `latest-recommended` with warnings as errors, banned-API analyzers, and
  CodeQL through GitHub's code scanning default setup, for C# and GitHub Actions with the extended query suite, on
  every pull request into `main`, every push to `main` and weekly. The `main` ruleset blocks a pull request that adds a
  CodeQL security alert of any severity or a CodeQL error or warning. Default setup is a repository setting, not a
  workflow file: read with `gh api repos/NCodeGroup/Buffers/code-scanning/default-setup` and
  `gh api repos/NCodeGroup/Buffers/rulesets/26229931` on 2026-11-17.
- **Review:** every change reaches `main` through a pull request with required CI, resolved review threads and an
  approval, which NCode Review gives a clean pull request and a maintainer gives a bot's; force pushes and deletion
  are blocked.
- **Dependencies:** central package management, Dependabot updates with a cooldown, and NuGet vulnerability warnings as
  errors in CI.
- **Release integrity (R7):** packages are built deterministically with Source Link from a tagged commit on `main`,
  pushed through nuget.org trusted publishing with no stored key after a maintainer approves, signed by nuget.org, and
  each GitHub release carries a Sigstore-signed provenance attestation and an SBOM. A release is tagged only once CI
  passes again on its commit, and its tag is signed with gitsign. [SECURITY.md](../SECURITY.md#verify-a-package)
  says how to verify each.
- **Response:** [SECURITY.md](../SECURITY.md) defines private reporting, coordinated disclosure and credit.

## Security Reviews

[//]: # "Maintainers: record each security review, newest first: its date, who did it, what it covered (the security requirements and the trust boundary above) and what it found. The OpenSSF Best Practices gold level asks for one in the last five years."

None recorded yet.
