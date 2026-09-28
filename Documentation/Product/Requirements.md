# Requirements

> **TODO** — structure only; no requirements have been authored. Phase 1 planning
> authors the first ones.

Each requirement gets a stable identifier so planning, commits and tests can cite
it. Identifiers are never reused or renumbered, even after a requirement is
dropped — mark it `Withdrawn` instead. A functional requirement names the upstream curl
option or request it answers, and the upstream curl build its behaviour was measured
against (`UpstreamCurlBuilds.json`) - never the Curl port (ADR-0003).

## Functional

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-001 | > **TODO** | Must / Should / Could | Draft |

Priorities use MoSCoW (Must / Should / Could / Won't). "Must" means the release is
not shippable without it — if everything is a Must, nothing is.

## Non-functional

Qualities rather than behaviours. Each one needs a number, or it is not a
requirement but a wish.

| ID | Quality | Target | Status |
| --- | --- | --- | --- |
| NFR-001 | > **TODO** (e.g. concurrent connections, startup time, footprint) | measurable target | Draft |

## Out of scope

Requirements considered and explicitly rejected, with the reason. Keeping them
here stops them being re-proposed every few months.

## Open questions

Unresolved points that block requirements from leaving `Draft`. Each should name
who can answer it.
