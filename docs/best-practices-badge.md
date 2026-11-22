# OpenSSF Best Practices badge

NCode.Buffers is registered at [bestpractices.dev](https://www.bestpractices.dev/projects/16727) as project 16727 and has
met the passing level since 2026-11-15. The answers to the criteria live on that site, which is the source of truth;
this page doesn't copy them.

[best-practices-criteria.md](best-practices-criteria.md), which the template keeps current, maps every criterion of
the three levels to the answer to expect and its evidence, and holds the prompts that register the project and
update its answers.

## The badge

```html
<a href="https://www.bestpractices.dev/projects/16727"><img alt="OpenSSF Best Practices" src="https://www.bestpractices.dev/projects/16727/badge"></a>
```

OpenSSF Scorecard's CII-Best-Practices check scores the badge level: in progress 2, passing 5, silver 7, gold 10
([ADR-0020](decisions/project-template/0020-github-projects-are-scored-by-openssf-scorecard.md)).

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
| Test policy (`test_policy_mandated`) | [CONTRIBUTING.md](../CONTRIBUTING.md), which says that a change that adds or changes behavior adds or updates the automated tests that cover it |

## Needs a Person

- `access_continuity` (silver MUST): a second owner of the `NCodeGroup` organization and the nuget.org package, as
  [GOVERNANCE.md](../GOVERNANCE.md#continuity) says. Silver can't be claimed until there is one.
- `bus_factor` (silver SHOULD, gold MUST): a bus factor of 2 or more, which the same second owner gives.
- `two_person_review` (gold MUST): a second maintainer.
- `contributors_unassociated` (gold MUST): two significant contributors from different organizations.
- `require_2FA` (gold MUST) and `secure_2FA` (gold SHOULD): the GitHub organization requires two-factor
  authentication, with secure methods only, which only an owner can set and confirm.
- `security_review` (gold MUST): a security review in the last five years, recorded in
  [assurance-case.md](assurance-case.md#security-reviews).
- `dco` (silver SHOULD): unmet. The project doesn't ask for `Signed-off-by`
  ([ADR-0023](decisions/project-template/0023-a-project-ships-the-documents-the-best-practices-silver-level-asks-for.md)).

## Also Supported

Provenance attestations and an SBOM on each release (`.github/workflows/release-provenance.yml`, which `publish.yml`
calls, [ADR-0151](decisions/project-template/0151-release-follow-ups-are-jobs-publish-calls.md)), and OpenSSF Scorecard on every push
(`.github/workflows/scorecard.yml`).
