# 1010. Site samples are compiled tests in a project of their own, included by key when the site is built

- **Status:** Accepted
- **Date:** 2026-11-15
- **Deciders:** NCode Group
- **Amends:** ADR-1005, items 2 and 3, and adds a rule for the keys of its samples.

## Context

The documentation site's pages show C# ([ADR-1009](1009-the-documentation-site-is-narrative-guides-built-by-a-shared-toolchain-outside-this-repository.md)),
and a sample typed into a page drifts from the API as a README sample does (ADR-1005). Three facts shape how the
pages get compiled samples:

- MarkdownSnippets cannot write into a page. It processes `.mdx` files and puts `<!-- snippet: Key -->` comments
  around the code it copies, and MDX rejects an HTML comment: the site's build stops with "Unexpected character `!`"
  (MarkdownSnippets.Tool 29.0.0 and the toolchain's Astro 7.4.11, tested 2026-11-15). A gate that hashes only `*.md`
  passes a page the tool rewrote.
- `tests/NCode.Buffers.Tests` sees the library's internal members, so a sample written there can use a type no reader
  of the page can.
- MarkdownSnippets reads every C# file in the repository for sample regions, and the site's build reads every C# file
  under `tests/`. The keys of all samples share one space. MarkdownSnippets does not reject a key defined twice: it
  writes both blocks at each use. Both match a key exactly, letter case included (MarkdownSnippets 29.0.0, tested
  2026-11-16): where two keys differ only in case, a key typed in the wrong case includes the other sample, and
  nothing fails.

## Decision

**Every C# sample on the site is a region of a compiled, passing xUnit test in `tests/NCode.Buffers.Site.Tests`, and
the site's build includes it in the page by its key. The READMEs keep MarkdownSnippets.**

1. `tests/NCode.Buffers.Site.Tests` holds the site's samples and nothing else. The library grants it no
   `InternalsVisibleTo`, and its namespace, `SiteSamples`, is outside `NCode.Buffers`, so a sample needs
   `using NCode.Buffers;` and compiles only against the public API, as a consumer's code does. The project is named
   `.Tests` so that the template's test settings apply and `dotnet test` runs it. It departs from the convention that
   a test project is named for the project it tests, because it tests the site's pages.
2. A page's samples are one class named for the page's file, in a folder named for the page's folder:
   `docs/site/guides/verify-a-mac.mdx` has `Guides/VerifyAMacSamples.cs`, class `VerifyAMacSamples` in namespace
   `SiteSamples.Guides`, and samples of `docs/site/index.mdx` would be in `IndexSamples.cs`. Each sample is one fact
   named `<Key>Sample_Runs`, with the sample between `// begin-snippet: <Key>` and `// end-snippet`, asserting what the
   sample's comments claim; it compares a key, a MAC or another secret with the project's `SecretAssert.Equal`, in
   fixed time. These names replace the test conventions' `Method_State_Expected` and mirrored types, as ADR-1005's do.
3. A key is PascalCase letters and digits, and is defined once in the repository; keys that differ only in letter
   case count as one key. The key of a site sample starts with `Site`, and no other key does. A page may also include
   a README sample by its key; the missing prefix shows that a change to that sample changes the READMEs.
4. A page includes a sample with an empty fenced block whose info string is `cs snippet=<Key>`. A page holds no copy
   of C# that must compile. The site's build stops on a key no sample defines, a key defined twice and a region that
   is never closed.
5. A page is an `.mdx` file. `mdsnippets.json` excludes `*.mdx` documents and `*.astro` sources by file name, so
   MarkdownSnippets reads neither the pages nor the site's components.
6. `build/Assert-SampleKeys.ps1` fails on a key defined twice, on a site sample whose key lacks the prefix and on any
   other key that has it, and `build/Test-AssertSampleKeys.ps1` runs it against fixture repositories: a clean one,
   and one for each failure. `build/gate.ps1` runs the test, then the check. It hashes `*.mdx` with `*.md` around
   `dotnet mdsnippets` and fails when the tool changes either.
7. `build/Test-Coverage.ps1` runs `tests/NCode.Buffers.Tests` alone, and the sample project references no coverage
   collector: a line of the library that only a sample runs counts as not covered.

## Options considered

- MarkdownSnippets copying the samples into the pages, as it does into the READMEs. Rejected: MDX rejects its
  markers, and every use would hold a committed copy.
- A folder of samples in `tests/NCode.Buffers.Tests`, with a check that no sample uses an internal member. Rejected:
  a separate project gets that check from the compiler.
- A directory exclusion in `mdsnippets.json`. Rejected: MarkdownSnippets matches it as a substring of the absolute
  path, so a checkout whose path contains the word excludes the whole repository (tested 2026-11-15).
- Keys without a prefix, kept apart only by the duplicate check. Rejected: a page's source would not show which of
  its samples the READMEs also use.
- Counting the samples toward coverage. Rejected: the minimum is a statement about the library's own tests.
- Compiled tests in a project of their own, included by key when the site is built (chosen).

## Consequences

- A sample that uses an internal member fails the build with CS0122.
- The samples show CSharpier's formatting, and a title or marked lines are written on the page's code block, never
  after the key in the test.
- The gate does not build the site. A page that names a key no sample defines stops the site's build in
  `.github/workflows/docs.yml`, not the gate.
- A Markdown file under `docs/site/` whose name ends in `.md` is processed by MarkdownSnippets like every other
  Markdown file in the repository.
- A README sample's key cannot start with `Site`.

## References

- `tests/NCode.Buffers.Site.Tests/`, `docs/site/`, `mdsnippets.json`, `build/gate.ps1`, `build/Assert-SampleKeys.ps1`,
  `build/Test-AssertSampleKeys.ps1`, `build/Test-Coverage.ps1`
- [ADR-1005](1005-readme-samples-are-compiled-tests-that-markdownsnippets-copies-into-the-readmes.md): the README
  samples.
- [ADR-1009](1009-the-documentation-site-is-narrative-guides-built-by-a-shared-toolchain-outside-this-repository.md):
  the site and its toolchain, whose `CONTRACT.md` defines the include and the marker lines.
- [#56](https://github.com/NCodeGroup/Buffers/issues/56)
- [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets)
