# 1002. CSharpier formats the code, and every C# file starts with an SPDX header

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group

## Context

Project-template ADR-0148 puts a CSharpier check in `dod.ps1`'s Format stage, and ADR-0117 says a project's own checks wrap `dod.ps1` in a gate script of its
own. Formatting by hand leaves layout to review. The NCode identity repository formats with CSharpier. The 15-line
Apache preamble in a `#region` repeated a year that differed per file.

## Decision

**CSharpier, pinned as a local tool, formats every C# and MSBuild file, `build/dod.ps1`'s Format stage, which `build/gate.ps1` runs, fails on an unformatted file,
and every C# file starts with `// Copyright © NCode Group` and `// SPDX-License-Identifier: Apache-2.0`.**

1. `.config/dotnet-tools.json` pins the CSharpier version with `rollForward: false`.
2. `.csharpierignore` excludes build output and `build/ProjectTemplate.props`.
3. `build/gate.ps1` runs the README samples check and then `build/dod.ps1`, whose Format stage runs `dotnet csharpier check .`. `AGENTS.md`'s gate is
   `./build/gate.ps1 -Fast`, and CI runs `./build/gate.ps1`.
4. A CSharpier update lands with `dotnet csharpier format .` in the same pull request.
5. `.editorconfig` raises IDE0073 to warning, and `NCode.Buffers.slnx.DotSettings` gives Rider the same header.

## Options considered

- No formatter. Rejected: layout debates in review.
- `dotnet format whitespace`. Rejected: a configurable style that drifts across settings.
- CSharpier (chosen).
- Header: keep the Apache preamble. Rejected: 15 lines, and a year that churns.
- Header: the SPDX header (chosen): machine-readable, and `LICENSE.txt` holds the text.

## Consequences

- One formatting commit touches most files once, and `git blame` shows it.
- Dependabot's CSharpier update may need a format pass.

## References

- `build/gate.ps1`, `.config/dotnet-tools.json`, `.csharpierignore`, `.editorconfig`
- [ADR-0117](project-template/0117-one-definition-of-done-gates-every-change.md): one Definition of Done gates every
  change.
- [CSharpier](https://csharpier.com)
- [Apache-2.0 on SPDX](https://spdx.org/licenses/Apache-2.0.html)
