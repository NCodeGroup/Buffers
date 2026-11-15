# OpenSSF Best Practices badge

NCode.Buffers is registered at [bestpractices.dev](https://www.bestpractices.dev/projects/16727) as project 16727 and has
met the passing level since 2026-11-15. The answers to the criteria live on that site, which is the source of truth;
this page doesn't copy them.

## The badge

```html
<a href="https://www.bestpractices.dev/projects/16727"><img alt="OpenSSF Best Practices" src="https://www.bestpractices.dev/projects/16727/badge"></a>
```

OpenSSF Scorecard's CII-Best-Practices check scores the badge level: passing 5, silver 7, gold 10.

## Toward Silver

The silver criteria that need a document point to these:

| Criteria | Where |
| --- | --- |
| Governance, roles and responsibilities | [GOVERNANCE.md](../GOVERNANCE.md) |
| Code of conduct | [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md) |
| Roadmap | [ROADMAP.md](../ROADMAP.md) |
| Architecture | [architecture.md](architecture.md) |
| Security requirements, assurance case | [assurance-case.md](assurance-case.md) |
| Signed releases and how to verify them | [SECURITY.md](../SECURITY.md#verify-a-package) and each GitHub release's `<tag>.intoto.jsonl` |
| Vulnerability response and credit | [SECURITY.md](../SECURITY.md) |
| Coding standards and their enforcement | [CONTRIBUTING.md](../CONTRIBUTING.md), `.editorconfig`, CSharpier and the analyzers, in the gate |
| Test coverage and policy | `./build/Test-Coverage.ps1` (98% line and branch) and [CONTRIBUTING.md](../CONTRIBUTING.md) |

Access continuity needs a person: a second owner of the `NCodeGroup` organization and the nuget.org package, as
[GOVERNANCE.md](../GOVERNANCE.md#continuity) says. Silver can't be claimed until there is one.

## Also Supported

Provenance attestations and an SBOM on each release (`.github/workflows/release-provenance.yml`, which `publish.yml`
calls, [ADR-1008](decisions/1008-release-follow-ups-are-jobs-publish-calls.md)), and OpenSSF Scorecard on every push
(`.github/workflows/scorecard.yml`).
