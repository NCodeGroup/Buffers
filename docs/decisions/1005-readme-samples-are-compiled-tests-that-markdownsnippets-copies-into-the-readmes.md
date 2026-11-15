# 1005. README samples are compiled tests that MarkdownSnippets copies into the READMEs

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group

## Context

A sample copied by hand into a README drifts from the API it shows, and nothing notices. Four README samples did not
compile before [#39](https://github.com/NCodeGroup/Buffers/issues/39). Both READMEs show the quick start, and the
package README, `src/NCode.Buffers/README.md`, is the page nuget.org renders.

## Decision

**Every README sample is a compiled xUnit test, MarkdownSnippets copies it into both READMEs, and `build/gate.ps1`
fails when a README differs from its test.**

1. The samples live in `tests/NCode.Buffers.Tests/ReadmeSamplesTests.cs`, one fact per sample between
   `// begin-snippet:` and `// end-snippet`, asserting what the sample's comments claim, and in
   `tests/NCode.Buffers.Tests/ReadmeSamples/Signatures.cs`, the quick start with its `using` lines, in namespace
   `ReadmeSamples`. Both files depart from the test convention of mirroring one production type and nesting sample
   types, because they test the READMEs. Each sample's fact is named `<Sample>Sample_Runs` and asserts what that one
   sample claims, and the facts are grouped in regions named for the README's sections, instead of the
   `Method_State_Expected` names and `{MethodName} Tests` regions of the test conventions.
2. MarkdownSnippets.Tool, pinned in `.config/dotnet-tools.json` with `rollForward: false`, copies the samples with the
   `InPlaceOverwrite` convention and `OmitSnippetLinks` from `mdsnippets.json`.
3. `build/gate.ps1` runs `dotnet mdsnippets` and fails when it changes a Markdown file.
4. A sample is edited in its test, then `dotnet mdsnippets` is run, never in a README.

## Options considered

- Hand-copied samples. Rejected: nothing checks them.
- A test that compares README code blocks with source. Rejected: custom parsing that MarkdownSnippets already does.
- MarkdownSnippets `SourceTransform` with `*.source.md` files. Rejected: two files per README and a generated header.
- Snippet-source links. Rejected: repository-relative links break on nuget.org.
- MarkdownSnippets `InPlaceOverwrite` (chosen): the READMEs keep their names, and the package README stays the package
  readme.

## Consequences

- The samples show CSharpier's formatting, so a long line wraps in the README.
- A README edit inside a `<!-- snippet: -->` block is overwritten by the next `dotnet mdsnippets`.
- Dependabot updates the tool with the other NuGet packages.
- A region name in a test needs a space, or MarkdownSnippets reads it as a snippet.

## References

- `tests/NCode.Buffers.Tests/ReadmeSamplesTests.cs`, `tests/NCode.Buffers.Tests/ReadmeSamples/Signatures.cs`,
  `mdsnippets.json`, `build/gate.ps1`
- [#42](https://github.com/NCodeGroup/Buffers/issues/42)
- [ADR-1002](1002-csharpier-formats-the-code-and-every-csharp-file-starts-with-an-spdx-header.md): CSharpier formats the
  code, and the gate checks it.
- [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets)
