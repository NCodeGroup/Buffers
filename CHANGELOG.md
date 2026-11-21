# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Package validation compares each package with the published 5.4.0 package.

## [5.4.0] - 2026-11-20

### Added

- `BinaryTextFormat.Base64UrlStrict` decodes only the canonical unpadded Base64url JOSE requires (RFC 7515 section 2): `BinaryTextEncoding.RentDecoded` throws `FormatException` for `=` padding, whitespace, a character outside the URL-safe alphabet or a final character with unused bits set, and `RentEncoded` writes the same text as `Base64Url` ([#123](https://github.com/NCodeGroup/Buffers/issues/123), [ADR-1031](docs/decisions/1031-a-strict-base64url-format-decodes-only-the-canonical-unpadded-text-jose-requires.md)).

### Changed

- The project follows project-template v0.108.0: `/run-issues-all-in` runs a whole set of issues as one plan and one draft pull request, `/best-practices` reports OpenSSF Scorecard results and answers for the default branch, and `build/Update-Baseline.ps1` removes baseline suppressions when it raises the baseline to a new major version ([ADR-0032](docs/decisions/project-template/0032-a-plan-is-released-only-when-it-changes-what-a-release-ships.md), [ADR-0033](docs/decisions/project-template/0033-an-all-in-run-lifts-the-autopilots-limits-on-the-maintainers-word.md), [ADR-0153](docs/decisions/project-template/0153-best-practices-reports-scorecard-and-answers-for-the-default-branch.md)).
- Package validation compares each package with the published 5.3.2 package.
- The documentation of `BinaryTextFormat.Base64Url` says that decoding also accepts `=` padding and whitespace, and the README and the documentation site's guides to turning a secret between text and bytes and to reading a secret without a string name `Base64UrlStrict` ([#123](https://github.com/NCodeGroup/Buffers/issues/123)).

## [5.3.2] - 2026-11-19

### Added

- `build/site.ps1` builds, layout-checks and serves the documentation site with the NCodeGroup/docs toolchain cloned beside the main checkout, from the main checkout and from a plan's worktree alike, and runs the toolchain with `node`, so no option passes through npm; `AGENTS.md` and `CONTRIBUTING.md` give its commands ([#116](https://github.com/NCodeGroup/Buffers/issues/116)).

### Changed

- Package validation compares each package with the published 5.3.1 package.
- The documentation site's "Ways to get it wrong" page gives each pitfall what you write, what happens, why and what to write instead a line each, as the guides do, and lists its compiler errors as the guides list their pitfalls ([#115](https://github.com/NCodeGroup/Buffers/issues/115)).

## [5.3.1] - 2026-11-19

### Changed

- Package validation compares each package with the published 5.3.0 package.
- The documentation site reads more calmly: an evidence mark is its kind's glyph, which names the evidence on hover and to a screen reader and writes out only a ledger row or a record such as `ADR-1027`; pages open without the page-kind label, and guides without the "You'll use" line; each guide's pitfalls give what you write, what happens and what to write instead a line each; and the sidebar's guide groups start closed, with one-line labels.

## [5.3.0] - 2026-11-19

### Added

- `BufferFactory.RentRandom` rents a buffer of the length you ask for, filled from `RandomNumberGenerator`; `isSensitive` chooses the secure pool ([ADR-1027](docs/decisions/1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md)).
- `RentBytes` and `RentChars`, extension methods on `Encoding`, encode text into a rented byte buffer and decode bytes into a rented char buffer of the exact length, so `SecureEncoding.UTF8.RentBytes(password, isSensitive: true, out Span<byte> bytes)` leaves no array behind ([ADR-1027](docs/decisions/1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md)).
- `BinaryTextEncoding.RentDecoded` and `RentEncoded` decode Base64, Base64url and hexadecimal text, named by `BinaryTextFormat`, into a rented byte buffer, and encode bytes into a rented char buffer; invalid text throws `FormatException` after the rented buffer is returned, zeroed when `isSensitive` is true ([ADR-1027](docs/decisions/1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md)).
- `GetSpanLease` on `ReadOnlySequence<T>`, such as a `PipeReader`'s buffer, reads it as one span, copying into a rented buffer only when it spans several segments.
- `BufferFactory.CreateContiguousBufferWriter` returns `ContiguousBufferWriter<T>`, a pooled `IBufferWriter<T>` whose data is one span; with `isSensitive: true` it zeroes each buffer it outgrows and its buffer on `Dispose` ([ADR-1028](docs/decisions/1028-growable-writers-streams-and-text-builders-zero-each-buffer-they-outgrow.md)).
- `BufferFactory.CreatePooledStream` returns `PooledMemoryStream`, a seekable read-write `Stream` over one pooled buffer that zeroes each buffer it outgrows, the bytes `SetLength` cuts off and its buffer on `Dispose`; its `Read`, `Write`, `CopyTo`, `ReadByte` and `WriteByte` copy through no temporary array that nothing zeroes ([ADR-1028](docs/decisions/1028-growable-writers-streams-and-text-builders-zero-each-buffer-they-outgrow.md)).
- `PooledTextBuilder`, a `ref struct` that builds text from strings, characters and formattable values in a pooled buffer, optionally starting in a `stackalloc` span, and zeroes each buffer it outgrows and its buffer on `Dispose` when `isSensitive` is true ([ADR-1028](docs/decisions/1028-growable-writers-streams-and-text-builders-zero-each-buffer-they-outgrow.md)).
- `Secret<T>` and `Secret.Create` hold a secret in a pinned, pooled buffer that `Dispose` zeroes; `ToString()` and the debugger show `[REDACTED]`, `Equals` compares the contents in fixed time, and `Reveal()` is the one way to read it ([ADR-1029](docs/decisions/1029-secret-t-holds-a-secret-it-never-prints.md)).
- `SecureMemoryPool<T>` reports rentals, returns and buffers dropped under memory pressure on the `NCode.Buffers` meter, `BufferMetrics.MeterName`; `docs/metrics.md` lists the instruments ([ADR-1030](docs/decisions/1030-the-secure-pool-reports-metrics-through-one-static-meter.md)).
- The documentation site has guides to turning a secret between text, Base64, hex and bytes, building a secret that grows in one piece, keeping a secret in a field, and watching the secure pool; "Which buffer?", the buffer behavior matrix and the API map cover the new types.

### Changed

- Package validation compares each package with the published 5.2.5 package.
- The documentation site is served at https://buffers.ncode.io/, and the READMEs and the package's project website link there; GitHub Pages redirects every page at https://ncodegroup.github.io/Buffers/ to it ([ADR-1026](docs/decisions/1026-the-documentation-site-is-served-at-buffers-ncode-io.md)).
- `Sequence<T>.GetSpanLease` throws `OverflowException` for data longer than `int.MaxValue`, as the `ReadOnlySequence<T>` overload does, where it threw `ArgumentOutOfRangeException` from a rental of a negative size.
- The roadmap's scope includes the rent-and-fill helpers, the growable buffers, the secret holder and the pool's metrics.
- The documentation site's design decisions, "How the buffers work" and "Objections and alternatives" pages describe the secure pool's meter, where they said the library records no metrics.
- The site's evidence links tests and source lines at a commit that holds 5.3.0's code.

## [5.2.5] - 2026-11-18

### Changed

- Package validation compares each package with the published 5.2.4 package.
- The project follows project-template v0.104.0: the package's project website, https://ncodegroup.github.io/Buffers/, comes from `site.url` in `docs/site/site.config.json` instead of a setting in the project file, so it follows the site if it moves ([ADR-0152](docs/decisions/project-template/0152-a-packages-project-page-is-its-documentation-site.md)).

## [5.2.4] - 2026-11-18

### Changed

- The documentation site is built with NCodeGroup/docs v0.15.0, taken the day it was released, whose build loads every page at 1440, 1280, 1024 and 390 pixels wide and fails an element that leaves the page's content column or hides part of its content sideways; every page passes it ([#105](https://github.com/NCodeGroup/Buffers/issues/105), [ADR-1024](docs/decisions/1024-ncodegroup-docs-releases-are-taken-without-a-cooldown-during-the-build-out.md)).
- An evidence link on the documentation site shows the whole name of the test, sample or source it opens, wrapping a long name, where it cut the name short with an ellipsis ([#105](https://github.com/NCodeGroup/Buffers/issues/105)).
- The objections page sets the six ways to hold a secret side by side as a card per way, each field labelled, where a table hid most of its columns behind a sideways scroll ([#105](https://github.com/NCodeGroup/Buffers/issues/105)).
- The quick start's output wraps its lines instead of scrolling sideways, and on a phone the review checklist and the ownership timeline fit the page's width ([#105](https://github.com/NCodeGroup/Buffers/issues/105)).
- The home page's hero takes its single column beside the sidebar from the documentation toolchain, and looks as it did ([#106](https://github.com/NCodeGroup/Buffers/issues/106)).

## [5.2.3] - 2026-11-18

### Changed

- Package validation compares each package with the published 5.2.2 package.
- The documentation site's home page shows the sidebar, as every other page does, so the navigation stays in place when you choose Overview, and on a phone the home page has the menu button.
- The site's API map and buffer behavior matrix draw each entry as a card, so every field shows at any window width, where they hid columns behind a sideways scroll.
- A card on the documentation site that holds an evidence link fits its column, so the home page and the quick start no longer scroll sideways beside the sidebar.
- The package's project website, the link nuget.org shows as "Project website", is the documentation site, https://ncodegroup.github.io/Buffers/; the source repository link opens GitHub.

## [5.2.2] - 2026-11-18

### Added

- The documentation site is published to GitHub Pages, at https://ncodegroup.github.io/Buffers/, from every push to `main`, and `README.md` and the package README link to it ([#99](https://github.com/NCodeGroup/Buffers/issues/99), [ADR-1025](docs/decisions/1025-the-documentation-site-is-published-to-github-pages-from-the-default-branch.md)).

### Changed

- Package validation compares each package with the published 5.2.1 package.
- The site's proof of fixed-time comparison (R4) cites the test that compares spans whose only difference is in the first chunk, the buffer behavior reference cites the test that rents from `SecureMemoryPool<T>.Shared` after disposing it, and the proof that a fixed buffer writer never writes past its memory (R6) covers `Advance`, with the tests of counts near `int.MaxValue` and both writers' `Advance` ([#93](https://github.com/NCodeGroup/Buffers/issues/93), [#94](https://github.com/NCodeGroup/Buffers/issues/94)).

### Fixed

- The site's links to tests and source lines open the lines they name, at a `main` commit that holds the 5.1.3 fixes, and its pages describe `FixedTimeEquals<T>` comparing in chunks of at most `int.MaxValue` bytes, where they said the library has no comparison loop of its own ([#93](https://github.com/NCodeGroup/Buffers/issues/93), [#94](https://github.com/NCodeGroup/Buffers/issues/94)).

## [5.2.1] - 2026-11-18

### Changed

- Package validation compares each package with the published 5.2.0 package.
- The documentation site is built with NCodeGroup/docs v0.13.0, taken the day it was released while the toolchain is built out, and its components read the brand's public tokens, with the values they had ([#81](https://github.com/NCodeGroup/Buffers/issues/81), [ADR-1024](docs/decisions/1024-ncodegroup-docs-releases-are-taken-without-a-cooldown-during-the-build-out.md)).
- The guide to ban the leaky APIs shows `BannedSymbols.Buffers.txt`, the start of `BannedSymbols.Encoding.txt`, the banned-symbols `ItemGroup` of `Directory.Build.props` and `BufferFactory`'s `RS0030` suppression as they are in the repository, each linked to its lines at the commit the site was built from, and links the lists at v5.2.0 ([#81](https://github.com/NCodeGroup/Buffers/issues/81)).
- The documentation site's build fails when a guide lacks a heading of its kind: The usual code, With NCode.Buffers, Pitfalls, What this does not fix and Checklist, in order, or Check your work for the two numbered procedures; `build/Update-ReviewChecklist.ps1` gathers the guides that rule covers and fails on one without its Checklist ([#82](https://github.com/NCodeGroup/Buffers/issues/82)).

## [5.2.0] - 2026-11-17

### Added

- Tests that a buffer `BufferFactory.Rent` returns into a `Span<T>` with `isSensitive: true` is all zeros after its owner is disposed, past the requested length too, and that a sensitive buffer larger than one page is all zeros after its owner is disposed; that every segment of a sensitive `CreatePooledBufferWriter` with two segments is all zeros after the writer is disposed; and that the copy `GetSpanLease` makes of a sensitive writer with two segments is all zeros after the lease is disposed, while the writer keeps its data. `SecureArrayLifetimeTests.Constructor_AllocatesPinnedArray` asserts that the array is on the Pinned Object Heap ([#87](https://github.com/NCodeGroup/Buffers/issues/87)).
- `./build/gate.ps1` runs `build/Test-XmlDocExamples.ps1`, which compiles every example in the XML documentation and fails on one that does not compile or whose `<code>` tag is not alone on its line ([#83](https://github.com/NCodeGroup/Buffers/issues/83)).

### Changed

- Package validation compares each package with the published 5.1.3 package.
- The package does not list JetBrains.Annotations as a dependency: the reference is build-time only, because none of its attributes reaches the assembly. A consumer that uses JetBrains.Annotations references it directly ([#88](https://github.com/NCodeGroup/Buffers/issues/88), [ADR-1022](docs/decisions/1022-jetbrains-annotations-is-a-build-time-reference-not-a-package-dependency.md)).

### Fixed

- The `SecureArrayLifetime<T>` constructor and `SecureArrayLifetime<T>.Create` throw `ArgumentOutOfRangeException` with `ParamName` `length` for a negative length, as `BufferFactory.CreatePinnedArray` does, instead of `OverflowException`; and `BufferFactory.Rent` checks a negative `minBufferSize` before it rents, throwing `ArgumentOutOfRangeException` with `ParamName` `minBufferSize`, where it rented a buffer first for -1 or reported the pool's parameter name ([#86](https://github.com/NCodeGroup/Buffers/issues/86), [ADR-1021](docs/decisions/1021-a-negative-length-or-size-throws-argumentoutofrangeexception-before-anything-is-allocated.md)).
- The XML documentation and the site's API reference describe what the code does: when `SecureMemoryPool<T>` drops its cache, what `Clear` on the fixed buffer writers leaves for `Reset`, that a lifetime's span still reaches the zeroed memory after `Dispose`, that every `Dispose` of a `RefSpanLease<T>` reaches its owner, and that `FixedTimeEquals<T>` does not hide the length; the `CreateArrayBufferWriter`, `RefSpanLease<T>` and `FixedTimeEquals<T>` examples compile ([#83](https://github.com/NCodeGroup/Buffers/issues/83)).
- `README.md`, `docs/architecture.md` and `docs/assurance-case.md` say which APIs take `isSensitive`, when the pool drops its cache, what a span reaches after its owner is disposed, what a second `Dispose` does, and that CodeQL runs through GitHub's code scanning default setup ([#84](https://github.com/NCodeGroup/Buffers/issues/84)).
- [ADR-1020](docs/decisions/1020-the-unmanaged-constraint-rests-on-byte-views-and-zeroing-not-on-pinned-allocation.md) amends ADR-1006 with the reasons the `unmanaged` constraint holds on the target framework, where a pinned allocation accepts any element type, and the site's design decisions page gives them ([#85](https://github.com/NCodeGroup/Buffers/issues/85)).

## [5.1.3] - 2026-11-17

### Added

- Thirteen pages of the documentation site: guides to reject malformed UTF-8 and ASCII, serialize into a buffer that must not grow, split delimited text without copying substrings, ban the leaky APIs at build time, test that your code zeroes its secrets, review a change that touches secrets, and give pipes and stream readers a pool that cleans up; concepts on how the buffers work and why the library is built this way; objections and alternatives, and the package's versions, support and verification; and references for buffer behavior, sizes and pooling, and every public member ([#68](https://github.com/NCodeGroup/Buffers/issues/68)).
- Decision records ADR-1011 to ADR-1018 for the design rules the code keeps: no finalizer, the 4096-byte page, allocation without clearing, `SecureSpanLifetime<T>` not pinning, the secure pool's cache and when it is dropped, zeroing and comparison through `CryptographicOperations`, Nerdbank.Streams' `Sequence<T>`, and neither locking nor encrypting memory ([#68](https://github.com/NCodeGroup/Buffers/issues/68)).

### Changed

- Package validation compares each package with the published 5.1.2 package.
- `./build/gate.ps1` runs `build/Test-AssertApiMap.ps1`; `build/Test-SiteEvidence.ps1` fails when the site's API map and `PublicAPI.Shipped.txt` differ; and `build/Update-ReviewChecklist.ps1 -Check` fails when the review checklist is out of step with the guides' Checklist sections ([#68](https://github.com/NCodeGroup/Buffers/issues/68)).

### Fixed

- `CryptographicOperations.ZeroMemory<T>` zeroes, and `CryptographicOperations.FixedTimeEquals<T>` compares, spans of more than `int.MaxValue` bytes, in chunks of at most that many bytes. Such a span threw `OverflowException` before any byte was zeroed or compared, so disposing a sensitive buffer, a `SecureArrayLifetime<T>` or a `SecureSpanLifetime<T>` of 2 GiB or more, of an element type wider than one byte, threw and left the buffer as it was ([#89](https://github.com/NCodeGroup/Buffers/issues/89)).
- `FixedSpanBufferWriter<T>.Advance` and `FixedMemoryBufferWriter<T>.Advance` throw `InvalidOperationException` and leave the writer as it was for every count larger than `FreeCapacity`. A count that took `WrittenCount + count` past `int.MaxValue` was accepted and made `WrittenCount` negative, after which `Reset()` threw `ArgumentOutOfRangeException` without zeroing what had been written ([#90](https://github.com/NCodeGroup/Buffers/issues/90)).
- `SecureMemoryPool<T>.Shared` ignores `Dispose`, as `MemoryPool<T>.Shared` does, so it stays usable and keeps its cache whoever disposes it. Disposing it made every later rental of that element type through `Shared` throw `ObjectDisposedException` for the rest of the process, and with it every sensitive rental the library makes: `BufferFactory.Rent` with `isSensitive: true`, a sensitive `CreatePooledBufferWriter` once it grows, and `GetSpanLease` and `ConsumeAsContiguousSpan` with `isSensitive: true` on a multi-segment sequence. A pool created with `new SecureMemoryPool<T>()` still drops its cache and makes `Rent` throw when disposed ([#91](https://github.com/NCodeGroup/Buffers/issues/91), [ADR-1023](docs/decisions/1023-the-shared-secure-pool-ignores-dispose.md)).

## [5.1.2] - 2026-11-17

### Fixed

- `FixedSpanBufferWriter<T>.Reset()` and `FixedMemoryBufferWriter<T>.Reset()` zero the written part with `CryptographicOperations.ZeroMemory`, which the runtime does not optimize away, instead of `Span<T>.Clear()`; an element type that is or contains a reference is still set to its default with `Span<T>.Clear()` ([#74](https://github.com/NCodeGroup/Buffers/issues/74)).

## [5.1.1] - 2026-11-17

### Changed

- Package validation compares each package with the published 5.1.0 package.

### Fixed

- A culture-aware `StringSegments.Split` or `SplitSegments` cuts out exactly the text the separator matched, whose length can differ from the separator's, such as the precomposed `é` matched by `e` and a combining acute accent: it no longer drops or keeps characters next to the match, or throws `ArgumentOutOfRangeException` when the match ends the value ([ADR-1019](docs/decisions/1019-a-string-separator-that-matches-no-text-leaves-the-value-whole.md)).

### Security

- A pooled sensitive buffer is zeroed and returned to the pool every time its owner is disposed. A buffer of 4096 bytes or less rented with `isSensitive: true`, through `BufferFactory.Rent`, `SecureMemoryPool<T>`, a sensitive `CreatePooledBufferWriter`, `GetSpanLease` or `ConsumeAsContiguousSpan`, was zeroed and returned only the first time: from its second rental on, `Dispose` left its contents in memory and dropped the buffer. Every rental gets an owner of its own, so a second `Dispose` of an earlier rental never touches a later one ([GHSA-7mw4-65fw-m3xr](https://github.com/NCodeGroup/Buffers/security/advisories/GHSA-7mw4-65fw-m3xr)).
- `StringSegments.Split` and the `SplitSegments` extensions with a `ReadOnlySpan<char>` separator return one segment holding the whole value when the separator matches no text, such as an empty separator, as `string.Split(string)` does, instead of looping until the process runs out of memory ([GHSA-pmgm-gmr9-6v3g](https://github.com/NCodeGroup/Buffers/security/advisories/GHSA-pmgm-gmr9-6v3g), [ADR-1019](docs/decisions/1019-a-string-separator-that-matches-no-text-leaves-the-value-whole.md)).

## [5.1.0] - 2026-11-16

### Added

- The content of a documentation site, in `docs/site/`, which the shared Astro Starlight toolchain in [NCodeGroup/docs](https://github.com/NCodeGroup/docs) builds, so this repository holds no npm manifest or lockfile. A workflow builds the site on every pull request, and nothing publishes it yet ([#56](https://github.com/NCodeGroup/Buffers/issues/56), [ADR-1009](docs/decisions/1009-the-documentation-site-is-narrative-guides-built-by-a-shared-toolchain-outside-this-repository.md)). The site holds:
  - A landing page: the usual code beside the same method written with NCode.Buffers, what the library does and does not do with the evidence for each, and a guide for each situation.
  - Start: a quick start that shows a key turn to zeros, a "Which buffer?" picker of at most four questions with a card for each of the ten buffers, and what the library protects and what it does not.
  - Six guides, each the usual code, what it leaves in memory, the fix, its pitfalls, what it does not fix and a checklist: derive a key from a password, verify a MAC without leaking timing, work with an API that takes or returns `byte[]`, build a secret of unknown length, read a secret without leaving a string behind, and keep a secret across an `await` or in a field.
  - Concepts: how secrets leak in managed code, with nine leak cards and a memory strip that steps through the life of a key, and who zeroes what, and when.
  - Evidence: each requirement the library accepts, with the tests that fail when it stops being true, the source lines behind it and what that evidence does not show.
  - Reference: the ways to get it wrong, by symptom, with the compiler errors and what to write instead.
- `tests/NCode.Buffers.Site.Tests`, a test project for the site's samples that cannot see the library's internal members; a page includes a sample by its key when the site is built ([ADR-1010](docs/decisions/1010-site-samples-are-compiled-tests-in-a-project-of-their-own-included-by-key-when-the-site-is-built.md)).
- Each release tag from 5.1.0 on is signed with gitsign through Sigstore's keyless signing, and `SECURITY.md`'s Verify a Release section shows how to check one with `gitsign verify-tag` ([#69](https://github.com/NCodeGroup/Buffers/issues/69), [ADR-0028](docs/decisions/project-template/0028-github-releases-are-immutable-and-their-tags-signed.md)).

### Changed

- Package validation compares every package with the published 5.0.3 package ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).
- `./build/gate.ps1` fails on a line that MarkdownSnippets or the site's build takes for a sample marker unless it is `// begin-snippet: <Key>` alone on its line, on a sample key that is not PascalCase letters and digits, on one defined twice, on a site sample's key that does not start with `Site`, on any other key that does, and when `dotnet mdsnippets` changes an `.mdx` file; `mdsnippets.json` keeps MarkdownSnippets off `.mdx` pages and `.astro` components ([ADR-1010](docs/decisions/1010-site-samples-are-compiled-tests-in-a-project-of-their-own-included-by-key-when-the-site-is-built.md)).
- `./build/gate.ps1` runs `build/Test-SiteEvidence.ps1`, which fails when a test, sample or source line the site cites is gone or has moved, and when a line a page marks on a sample does not match exactly one line of it.
- A release is tagged only once CI passes again on the commit its pull request merged ([#69](https://github.com/NCodeGroup/Buffers/issues/69), [ADR-0026](docs/decisions/project-template/0026-tests-gate-coverage-at-the-gold-thresholds-and-a-release-waits-for-them.md)).
- A pull request merges once NCode Review approves it, or a maintainer approves one a bot opened, and `CONTRIBUTING.md` lists the review requirements ([#69](https://github.com/NCodeGroup/Buffers/issues/69), [ADR-0022](docs/decisions/project-template/0022-ncode-reviews-approval-gates-the-merge.md)).
- `SECURITY.md` promises an acknowledgement of a vulnerability report within 14 days ([#69](https://github.com/NCodeGroup/Buffers/issues/69)).
- The workflow and script that raise the package validation baseline after each release are the project template's ([#69](https://github.com/NCodeGroup/Buffers/issues/69), [ADR-0150](docs/decisions/project-template/0150-a-workflow-raises-the-package-validation-baseline-after-each-release.md), [ADR-0151](docs/decisions/project-template/0151-release-follow-ups-are-jobs-publish-calls.md)).

## [5.0.3] - 2026-11-15

### Added

- After each stable release is published, `publish.yml` calls `.github/workflows/raise-baseline.yml`, which opens the draft pull request that raises the package validation baseline, and `build/Update-Baseline.ps1` does it by hand ([ADR-1007](docs/decisions/1007-a-workflow-raises-the-package-validation-baseline-after-each-release.md), [ADR-1008](docs/decisions/1008-release-follow-ups-are-jobs-publish-calls.md)).
- `GOVERNANCE.md`, `CODE_OF_CONDUCT.md`, `ROADMAP.md`, `docs/architecture.md` and `docs/assurance-case.md`, which state the project's roles, conduct, direction, design and security requirements, for the OpenSSF Best Practices silver level ([#60](https://github.com/NCodeGroup/Buffers/issues/60)).

### Changed

- Package validation compares every package with the published 5.0.2 package instead of 5.0.0 ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).

### Fixed

- Each GitHub release carries its packages, a signed provenance file (`<tag>.intoto.jsonl`) and an SBOM: `publish.yml` calls `.github/workflows/release-provenance.yml` after the push, which never ran for a release `tag-release.yml` published, and a maintainer can backfill an earlier release while its Publish run's packages are kept ([#60](https://github.com/NCodeGroup/Buffers/issues/60), [ADR-1008](docs/decisions/1008-release-follow-ups-are-jobs-publish-calls.md)).

## [5.0.2] - 2026-11-15

### Added

- FsCheck property tests for `StringSegments`, the fixed buffer writers, `BufferFactory` and the cryptographic helpers, with a fixed seed so every run is deterministic ([NCodeGroup/project-template#373](https://github.com/NCodeGroup/project-template/issues/373)).
- The OpenSSF Best Practices passing badge, for project 16727, in the README, and `docs/best-practices-badge.md` ([NCodeGroup/project-template#374](https://github.com/NCodeGroup/project-template/issues/374)).
- Each release is attested with build provenance and carries an SPDX SBOM, from `.github/workflows/release-provenance.yml`, and `SECURITY.md` says how to verify a package with `gh attestation verify`.

### Changed

- The project follows project-template v0.92.0: Nerdbank.GitVersioning is referenced once, by `build/ProjectTemplate.props`, and the test project no longer repeats the template's settings.

## [5.0.1] - 2026-11-14

### Changed

- Package validation compares every package with the published 5.0.0 package instead of 4.1.0, and `CompatibilitySuppressions.xml`, which recorded the 5.0.0 breaking changes against 4.1.0, is gone, so later breaking changes fail the pack ([#38](https://github.com/NCodeGroup/Buffers/issues/38), [ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).

## [5.0.0] - 2026-11-14

### Added

- Public API tracking with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, recording the surface released in 4.1.0.
- `CONTRIBUTING.md`, `SECURITY.md`, architecture decision records in `docs/decisions/` and a Definition of Done gate (`build/dod.ps1`).
- Publishing to nuget.org through trusted publishing: `.github/workflows/publish.yml` tests and packs the tagged commit without any credential, then pushes the packages and symbol packages from the protected `nuget` environment with a short-lived key, and runs when a GitHub release is published or when it is dispatched by hand with a tag ([#25](https://github.com/NCodeGroup/Buffers/issues/25), [ADR-0135](docs/decisions/project-template/0135-packages-are-published-from-a-github-release-through-trusted-publishing.md)).
- An [OpenSSF Scorecard](https://scorecard.dev/viewer/?uri=github.com/NCodeGroup/Buffers) workflow, `.github/workflows/scorecard.yml`, scores the repository's supply-chain practices on each push to `main` and every week, publishes the score that the README's badge shows, and uploads its findings to code scanning ([#41](https://github.com/NCodeGroup/Buffers/issues/41)).
- Changelog sections for the 4.0.0 and 4.1.0 releases ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).
- Unit tests for every public member of `BufferFactory` and `CryptographicOperationsExtensions`, and for the internal `SecureMemory<T>` and `Gen2GcCallback`; and `build/Test-Coverage.ps1`, which CI runs to fail a pull request below 98% line or branch coverage.
- Package versions come from `version.json` through Nerdbank.GitVersioning: a release tag must name the version it holds, and a package built from any other commit carries `-g<commit>` in its version ([ADR-1000](docs/decisions/1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md)).
- Package validation compares every package with the published 4.1.0 package and fails the pack on a breaking change; `docs/api-compatibility.md` describes how the public API changes ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).
- CSharpier formats the code, and `./build/gate.ps1`, which CI and the local gate run, checks the formatting before the Definition of Done ([ADR-1002](docs/decisions/1002-csharpier-formats-the-code-and-every-csharp-file-starts-with-an-spdx-header.md)).
- `BannedSymbols.Encoding.txt` and `BannedSymbols.Buffers.txt` keep shipping code on `SecureEncoding` and `BufferFactory` ([ADR-1003](docs/decisions/1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md)).
- A package README for nuget.org with installation, an example and links to the full documentation.
- Coverage on Codecov: on each push to `main`, CI uploads the Cobertura report that `build/Test-Coverage.ps1 -ReportDirectory` keeps to [Codecov](https://codecov.io/gh/NCodeGroup/Buffers) through OIDC, without a stored token, and both READMEs show its coverage badge ([#40](https://github.com/NCodeGroup/Buffers/issues/40)).
- The C# samples in both READMEs are tests in `tests/NCode.Buffers.Tests`, where each one compiles and runs, and [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets) copies them into the READMEs; `./build/gate.ps1`, which CI runs, fails when a README's sample differs from its test, and the quick start is a complete `Signatures` class with its `using` directives ([#42](https://github.com/NCodeGroup/Buffers/issues/42), [ADR-1005](docs/decisions/1005-readme-samples-are-compiled-tests-that-markdownsnippets-copies-into-the-readmes.md)).

### Changed

- `AssemblyVersion` now moves only with the major version; `FileVersion` and `InformationalVersion` keep the exact version. Earlier releases set `AssemblyVersion` to the full version, such as `4.1.0.0`; the 4.x line keeps `4.1.0.0`, so an application built against 4.1.0 still loads later 4.x releases.
- Internal analyzer clean-up (suppressions with justifications and a discarded `Gen2GcCallback` instance); behavior and public API are unchanged.
- Project updated from project-template v0.41.0 to v0.86.1 (build, CI and contribution tooling) ([#26](https://github.com/NCodeGroup/Buffers/issues/26)).
- A published GitHub release starts `publish.yml`, which pushes the packages to nuget.org; `AGENTS.md` sets `release: tag+release`, which has each release tagged and published as its pull request merges ([#25](https://github.com/NCodeGroup/Buffers/issues/25), [ADR-0016](docs/decisions/project-template/0016-releases-are-tagged-and-published-when-their-pull-request-merges.md)).
- `SECURITY.md`, `CONTRIBUTING.md`, `AGENTS.md` and the README describe the new release path ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).
- `BufferFactory` is no longer excluded from code coverage, and `SequenceExtensions` no longer carries exception handlers that no input can reach; behavior and public API are unchanged.
- Every C# file starts with the two-line SPDX header instead of the Apache-2.0 preamble, and the build enforces it ([ADR-1002](docs/decisions/1002-csharpier-formats-the-code-and-every-csharp-file-starts-with-an-spdx-header.md)).
- The solution file is `NCode.Buffers.slnx`, and Source Link comes from the .NET SDK instead of the `Microsoft.SourceLink.GitHub` package.
- The library's source is in `src/NCode.Buffers` and its tests are in `tests/NCode.Buffers.Tests`, as the project template lays out .NET projects; the package and its public API are unchanged ([#34](https://github.com/NCodeGroup/Buffers/issues/34)).
- This release is 5.0.0, and its assemblies carry `AssemblyVersion` 5.0.0.0 ([#35](https://github.com/NCodeGroup/Buffers/issues/35), [ADR-1000](docs/decisions/1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md)).
- The README opens with installation, a quick start and a table for choosing a type, and adds security notes on what pinning and zeroing do and don't guarantee; the package description no longer calls string splitting zero-allocation, neither README nor the description names a .NET version (a badge reads the target framework from the project file), and the package icon is the current NCode mark.
- **Breaking:** `SecureMemoryPool<T>`, `SecureArrayLifetime<T>`, `SecureSpanLifetime<T>`, `CryptographicOperations.ZeroMemory<T>` and `FixedTimeEquals<T>`, `BufferFactory.Rent<T>`, `CreatePinnedArray<T>` and `CreatePooledBufferWriter<T>`, `Span<T>.GetSecureLifetime()` and the `Sequence<T>` extensions require `T : unmanaged` instead of `T : struct`, so a `T` with reference fields fails to compile instead of throwing `ArgumentException` at run time; code that passes such a `T` with `isSensitive: false` uses `MemoryPool<T>.Shared` or `new Sequence<T>()` directly ([#45](https://github.com/NCodeGroup/Buffers/issues/45), [ADR-1006](docs/decisions/1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md)).

### Removed

- The `Commit attribution` CI check and the `.githooks/commit-msg` hook: agent attribution is avoided, not enforced ([ADR-0018](docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md)).
- `.github/workflows/main.yml`: its build and test duplicated `ci.yml`, and its publish job and the `NUGET_APIKEY` secret it used are replaced by `publish.yml`; delete the secret from the repository settings ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).
- **Breaking:** The `net8.0` target framework, because .NET 8 left Microsoft support on 2026-11-10: the package targets `net10.0` only, so applications on .NET 8 stay on 4.x and move to .NET 10 to take 5.0 ([#35](https://github.com/NCodeGroup/Buffers/issues/35), [ADR-1004](docs/decisions/1004-the-package-targets-only-supported-lts-dotnet-versions.md)).

### Fixed

- `BufferFactory.CreatePinnedArray`, `CreatePooledBufferWriter` and `CreateArrayBufferWriter` throw `ArgumentOutOfRangeException` for a negative length, a negative `minimumSpanLength` or a non-positive `initialCapacity`, as their documentation says; they threw `OverflowException` or `ArgumentException`, or accepted a negative `minimumSpanLength`.
- `SecureMemoryPool<T>` pools buffers of `PageSize` (4096) bytes for every `T`, as its documentation says. It pooled `PageSize` elements, so a `SecureMemoryPool<int>` pinned 16 KB per buffer, it measured `char` and `bool` by their marshaled size, and a request whose byte count overflowed `int` could get a pooled buffer smaller than it asked for.
- The package carries its description and tags again: `NCode.Buffers/project.props`, which held them, was no longer imported once the `NCode.ProjectSettings.Scaffold` package was removed.
- The README's samples for `FixedSpanBufferWriter<T>`, `FixedMemoryBufferWriter<T>`, `MemorySegment<T>` and `SecureEncoding` compile.
- The README's "Choosing a type" tables fit GitHub's README column instead of cutting off their last column.
- The XML documentation of `StringSegments` and the `SplitSegments` extensions no longer calls splitting allocation-free: it says each split allocates one segment object per substring and `foreach` allocates an enumerator, and shows how to walk the segments without one; the `SplitSegments` example prints each segment's text instead of its type name ([#44](https://github.com/NCodeGroup/Buffers/issues/44)).
- `SequenceExtensions.GetSpanLease` no longer disposes the sequence it reads when the data is in one segment: disposing the lease left a `Sequence<T>` or pooled buffer writer reset to length 0, as its documentation says it does not ([#43](https://github.com/NCodeGroup/Buffers/issues/43)).

## [4.1.0] - 2026-01-21

### Added

- `CreateArrayBufferWriter` methods on `BufferFactory` for simple, non-pooled buffer writers.

## [4.0.0] - 2026-01-16

### Added

- First release after consolidating several projects into NCode.Buffers.

[Unreleased]: https://github.com/NCodeGroup/Buffers/compare/v5.4.0...HEAD
[5.4.0]: https://github.com/NCodeGroup/Buffers/compare/v5.3.2...v5.4.0
[5.3.2]: https://github.com/NCodeGroup/Buffers/compare/v5.3.1...v5.3.2
[5.3.1]: https://github.com/NCodeGroup/Buffers/compare/v5.3.0...v5.3.1
[5.3.0]: https://github.com/NCodeGroup/Buffers/compare/v5.2.5...v5.3.0
[5.2.5]: https://github.com/NCodeGroup/Buffers/compare/v5.2.4...v5.2.5
[5.2.4]: https://github.com/NCodeGroup/Buffers/compare/v5.2.3...v5.2.4
[5.2.3]: https://github.com/NCodeGroup/Buffers/compare/v5.2.2...v5.2.3
[5.2.2]: https://github.com/NCodeGroup/Buffers/compare/v5.2.1...v5.2.2
[5.2.1]: https://github.com/NCodeGroup/Buffers/compare/v5.2.0...v5.2.1
[5.2.0]: https://github.com/NCodeGroup/Buffers/compare/v5.1.3...v5.2.0
[5.1.3]: https://github.com/NCodeGroup/Buffers/compare/v5.1.2...v5.1.3
[5.1.2]: https://github.com/NCodeGroup/Buffers/compare/v5.1.1...v5.1.2
[5.1.1]: https://github.com/NCodeGroup/Buffers/compare/v5.1.0...v5.1.1
[5.1.0]: https://github.com/NCodeGroup/Buffers/compare/v5.0.3...v5.1.0
[5.0.3]: https://github.com/NCodeGroup/Buffers/compare/v5.0.2...v5.0.3
[5.0.2]: https://github.com/NCodeGroup/Buffers/compare/v5.0.1...v5.0.2
[5.0.1]: https://github.com/NCodeGroup/Buffers/compare/v5.0.0...v5.0.1
[5.0.0]: https://github.com/NCodeGroup/Buffers/compare/v4.1.0...v5.0.0
[4.1.0]: https://github.com/NCodeGroup/Buffers/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/NCodeGroup/Buffers/releases/tag/v4.0.0
