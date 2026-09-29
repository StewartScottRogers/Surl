# Architecture Decision Records

One file per decision, named `ADR-NNNN-short-slug.md`. Numbers are assigned in
order and never reused.

An ADR is immutable once **Accepted**. A decision that changes does not get edited
— a new ADR supersedes it, and the old one is marked `Superseded by ADR-NNNN`.
The value of the record is that it shows what was believed *at the time*.

Write one when a choice is expensive to reverse, when a reasonable person would
have chosen differently, or when the reasoning would otherwise be lost. Routine
choices do not need one.

Surl's ADRs are numbered from 0001 on their own. An ADR in the Curl port is not a Surl
decision, and never evidence for one (ADR-0003); where Surl adopts a choice the port
made, a Surl ADR says so and gives its own reasons.

## Index

| ADR | Title | Status | Date |
| --- | --- | --- | --- |
| [0001](ADR-0001-adopt-slnx-and-shared-projects.md) | Adopt the `.slnx` solution format and shared projects for documentation, the task board and the Claude configuration | Accepted | 2026-09-28 |
| [0002](ADR-0002-mirror-the-curl-ports-project-map.md) | Mirror the Curl port's project map, with `Surl.Content` in place of a file protocol library | Accepted | 2026-09-28 |
| [0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) | Upstream curl is Surl's only oracle; the Curl port is measured against Surl last | Accepted | 2026-09-28 |
| [0004](ADR-0004-the-listener-seam-and-the-exchange-context.md) | The listener seam and the exchange context: how a protocol server receives its transport and what it is told about each exchange | Accepted | 2026-09-28 |
| [0005](ADR-0005-surls-exit-code-table.md) | Surl's exit-code table: upstream curl's `CURLE_*` number wherever a server-side meaning carries over, 0 after Ctrl+C or SIGTERM | Accepted | 2026-09-28 |
