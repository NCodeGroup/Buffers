# OpenSSF Best Practices badge

A draft for registering NCode.Buffers at [bestpractices.dev](https://www.bestpractices.dev): the badge markdown, and
the answers to the passing level's criteria, with the evidence the repository gives for each. Nothing here is
registered yet, and the badge is not earned until the site says so. Scorecard's CII-Best-Practices check scores 0
until then ([project-template#374](https://github.com/NCodeGroup/project-template/issues/374)).

## The badge

Replace `PROJECT_ID` with the number the site assigns, then uncomment the line in [README.md](../README.md) next to the
Scorecard badge:

```markdown
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/PROJECT_ID/badge)](https://www.bestpractices.dev/projects/PROJECT_ID)
```

## What a person does

1. Sign in at https://www.bestpractices.dev with the GitHub account that administers `NCodeGroup/Buffers`.
2. Choose **Get Your Badge Now** and add `https://github.com/NCodeGroup/Buffers`. The form pre-fills what it can.
3. Enter the answers below. Choose **Met** only for a criterion you agree is met; the ones marked *You decide* are
   commitments or settings only the maintainers can confirm.
4. Save. The site assigns the project ID; put it in the badge markdown above and in the README.
5. Edit this page to say the badge is registered, and keep the answers current as the project changes.

## Answers

Each row's evidence is a file or a setting in this repository.

### Basics

| Criterion | Status | Evidence |
| --- | --- | --- |
| Project description | Met | [README.md](../README.md) |
| How to contribute | Met | [CONTRIBUTING.md](../CONTRIBUTING.md) |
| Contribution requirements | Met | [AGENTS.md](../AGENTS.md), the gate |
| FLOSS license, in a standard place | Met | Apache-2.0, `LICENSE.txt` |
| Basic and reference documentation | Met | READMEs, `docs/`, XML docs |
| HTTPS for the project sites | Met | github.com, nuget.org |
| Discussion that is searchable | Met | GitHub issues and pull requests |
| Documentation in English | Met | All of it |

### Change control

| Criterion | Status | Evidence |
| --- | --- | --- |
| Public version-controlled repository | Met | Git on GitHub |
| Unique version numbers, SemVer | Met | `version.json` |
| Release notes | Met | [CHANGELOG.md](../CHANGELOG.md) |
| Fixed vulnerabilities named in notes | Met | `### Security` heading |

### Reporting

| Criterion | Status | Evidence |
| --- | --- | --- |
| Bug-reporting process and tracker | Met | GitHub issues |
| Respond to most bug reports | *You decide* | Your own record |
| Published vulnerability process | Met | [SECURITY.md](../SECURITY.md) |
| Private vulnerability reporting | *You decide* | Settings → Security |
| Initial response within 14 days | *You decide* | A commitment |

### Quality

| Criterion | Status | Evidence |
| --- | --- | --- |
| Working build system | Met | `dotnet build` |
| Automated FLOSS test suite | Met | xUnit and FsCheck in `tests/` |
| Standard way to run the tests | Met | `dotnet test` |
| Tests for new functionality | Met | The gate, 98% coverage floor |
| Compiler warnings are errors | Met | `build/ProjectTemplate.props` |

### Security

| Criterion | Status | Evidence |
| --- | --- | --- |
| Knowledge of secure design | *You decide* | The maintainers' own |
| Delivery resists man-in-the-middle | Met | nuget.org over HTTPS, signed |
| No leaked private credentials | Met | Trusted publishing, no stored key |
| Medium or worse flaws fixed promptly | *You decide* | Your own record |
| Cryptography criteria | N/A | The library implements none |

### Analysis

| Criterion | Status | Evidence |
| --- | --- | --- |
| Static code analysis | Met | Roslyn analyzers in every build, CI |
| Fix found analysis issues | Met | Warnings fail the build |
| Dynamic analysis | Suggested | FsCheck property tests |

### Beyond the passing level

These are not needed to pass, and the repository already supports them: provenance attestations and an SBOM on each
release (`.github/workflows/release-provenance.yml`), and OpenSSF Scorecard on every push
(`.github/workflows/scorecard.yml`).
