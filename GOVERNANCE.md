# Governance

NCode.Buffers belongs to NCode Group, the `NCodeGroup` organization on GitHub, and is licensed under
[Apache-2.0](LICENSE.txt). This page says who does what and how decisions are made.

## Roles

| Role | Who | Responsibilities |
| --- | --- | --- |
| Maintainer | @polewskm | Sets direction, triages issues, reviews and merges pull requests, approves each publication in the `nuget` environment, answers vulnerability reports, owns the repository's settings and the nuget.org package |
| Contributor | Anyone | Opens issues and pull requests, under [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) |
| Automation | `ncode-review[bot]`, Dependabot, GitHub Actions | Reviews and approves a clean pull request, proposes dependency updates, runs CI, tags and publishes releases; never merges a pull request except the pin bump `code-review-auto-merge.yml` merges once a maintainer approves it |

Only a maintainer has write access. A contributor who keeps contributing reviewed, merged work can be invited to become
a maintainer; the existing maintainers decide, and the change is recorded in this file.

## Decisions

- Day-to-day decisions are made in issues and pull requests. Anyone can comment; a maintainer decides.
- A decision someone would otherwise re-argue is recorded as an architecture decision record in
  [docs/decisions/](docs/decisions/README.md), before or with the pull request that builds it. A change consumers can
  observe, such as to the public API, behavior or security posture, starts as an issue.
- Where maintainers disagree, they discuss it on the issue; with no agreement, the change doesn't happen.

## Releases

Versions follow Semantic Versioning. A release is tagged and published when its pull request merges, and its packages
reach nuget.org only after a maintainer approves the `nuget` environment. [SECURITY.md](SECURITY.md) says how to verify a
package.

## Continuity

The repository, its issues, decision records, build, release automation and documentation are all in the `NCodeGroup`
organization, so anyone given access can carry on with no knowledge held elsewhere. nuget.org publishing uses trusted
publishing, with no stored API key to hand over.

One person holds owner access to the organization and the nuget.org package today. Continuity needs a second owner of
both; when one is appointed, they are listed in the Roles table above.

## Changing This Document

Governance changes through a pull request to this file, which a maintainer merges.
