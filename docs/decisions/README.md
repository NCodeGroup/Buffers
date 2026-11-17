# Architecture Decision Records

How NCode.Buffers records its decisions: one short Markdown file per decision, kept in this folder and changed in
the same pull request as the code it explains.

## When to Write One

Write a record for a non-obvious choice that someone would otherwise question again: a design with real alternatives,
a trade-off that looks wrong without its reasons, or a departure from the project template. The conventions themselves
go in `AGENTS.md` or the instruction files it lists; a record explains why a convention is what it is.
`/analyze-decision` compares the options and writes one, in a worktree of its own, where it lands with the plan that builds it. Work bigger than one plan starts as a design document in
`docs/designs/`, from [its template](../designs/_template.md), and each of its decisions becomes a record here when its
phase lands.

## How to Write One

- Copy [0000-template.md](0000-template.md) to `NNNN-kebab-case-title.md`, starting from `1000`. Numbers below 1000
  are the project template's, in [project-template/](project-template/README.md), so an ADR number names one record
  across both folders. `.template/Test-AdrIndex.ps1` fails a new record numbered below 1000; records the project
  already had keep their numbers.
- Take a number no one else holds. Fetch first, then take the next number above both the highest record numbered 1000
  or more on `origin/<default branch>` and any record an open branch, pull request or worktree is writing. Check again
  after rebasing or merging the default branch, and when the number was taken meanwhile, renumber the file, its title,
  its index row and every link to it. Two records never share a number; `.template/Test-AdrIndex.ps1` fails when they
  do.
- Add the record to the index below, in the same change.

## Statuses

- `Proposed`: the decision is open.
- `Accepted`: the decision holds.
- `Deprecated`: the decision no longer applies, and nothing replaces it.
- `Superseded by NNNN`: a later record replaces it whole.

Never rewrite the decision of an accepted record; fixing a typo or a broken link is fine. A changed decision is a new
record, and the old record's status and index row say so. When the new record changes only part of the old one, the
old one stays `Accepted` with a note, "Accepted (amended by NNNN)", and the new one's index row says "(amends NNNN)".
The old record's text is not edited beyond its status line.

## What a Record Is

A record is a decision the project made, not an authority. Where an external standard or current security practice
governs, it wins: a record that conflicts with one is superseded to align with it, never cited to justify the
deviation.

## Writing Rules

- Write every section in the present tense, so it reads the same a year later: what is, and why. Avoid words that
  date it, such as "now", "new", "currently", "previously", "originally", "we decided to change" or "the first
  version"; the history belongs in the commit log and in the records a record supersedes.
- Give a volatile fact (a price, a quota, a limit, a supported version) its source and the date it was read, and mark
  an estimate as an estimate.
- Keep a record short, and link to the code, issues and records it depends on rather than restating them.

## The Template's Decisions

The project template's own decisions are in [project-template/](project-template/README.md). Template updates replace
that folder. To decide otherwise, write a record here that says "Supersedes: project-template ADR-NNNN" and list the
override in `AGENTS.md`.

## Index

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-1000](1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md) | Package versions come from version.json through Nerdbank.GitVersioning | Accepted |
| [ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md) | Each package is validated against the last released package | Accepted |
| [ADR-1002](1002-csharpier-formats-the-code-and-every-csharp-file-starts-with-an-spdx-header.md) | CSharpier formats the code, and every C# file starts with an SPDX header | Accepted |
| [ADR-1003](1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md) | Shipping code takes buffers and encodings through BufferFactory and SecureEncoding | Accepted |
| [ADR-1004](1004-the-package-targets-only-supported-lts-dotnet-versions.md) | The package targets only supported LTS .NET versions | Accepted |
| [ADR-1005](1005-readme-samples-are-compiled-tests-that-markdownsnippets-copies-into-the-readmes.md) | README samples are compiled tests that MarkdownSnippets copies into the READMEs | Accepted (amended by 1010) |
| [ADR-1006](1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md) | The secure APIs constrain their element type to unmanaged | Accepted |
| [ADR-1007](1007-a-workflow-raises-the-package-validation-baseline-after-each-release.md) | A workflow raises the package validation baseline after each release | Superseded by 0150 |
| [ADR-1008](1008-release-follow-ups-are-jobs-publish-calls.md) | Release follow-ups are jobs that publish.yml calls | Superseded by 0151 |
| [ADR-1009](1009-the-documentation-site-is-narrative-guides-built-by-a-shared-toolchain-outside-this-repository.md) | The documentation site is narrative guides, built by a shared toolchain outside this repository | Accepted |
| [ADR-1010](1010-site-samples-are-compiled-tests-in-a-project-of-their-own-included-by-key-when-the-site-is-built.md) | Site samples are compiled tests in a project of their own, included by key when the site is built (amends 1005) | Accepted |
| [ADR-1011](1011-buffers-have-no-finalizer-and-are-zeroed-only-by-dispose.md) | Buffers have no finalizer and are zeroed only by Dispose | Accepted |
| [ADR-1012](1012-the-secure-pool-caches-buffers-of-one-4096-byte-page.md) | The secure pool caches buffers of one 4096-byte page | Accepted |
| [ADR-1013](1013-pinned-buffers-are-allocated-without-being-cleared.md) | Pinned buffers are allocated without being cleared | Accepted |
| [ADR-1014](1014-securespanlifetime-zeroes-the-callers-span-and-does-not-pin-it.md) | SecureSpanLifetime zeroes the caller's span and does not pin it | Accepted |
| [ADR-1015](1015-the-secure-pools-cache-has-no-size-limit-and-is-dropped-under-memory-pressure.md) | The secure pool's cache has no size limit and is dropped under memory pressure | Accepted |
| [ADR-1016](1016-zeroing-and-fixed-time-comparison-call-cryptographicoperations.md) | Zeroing and fixed-time comparison call CryptographicOperations | Accepted |
| [ADR-1017](1017-the-pooled-buffer-writers-return-nerdbank-streams-sequence.md) | The pooled buffer writers return Nerdbank.Streams' Sequence&lt;T&gt; | Accepted |
