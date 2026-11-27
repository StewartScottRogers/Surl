---
id: BL-1951
title: Point ADR-0107 decision 1 at ADR-0122 decision 5 for the amqp1 TLS header under TLS-SRP
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1932]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1951 — Point ADR-0107 decision 1 at ADR-0122 decision 5 for the amqp1 TLS header under TLS-SRP

## Goal

A reader of ADR-0107 decision 1 learns that TLS-SRP, as well as a certificate, makes `amqp1` upgrade on a TLS header.

## Context

Found while working BL-1932 (2026-11-27). The decision 1 table of
`Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md` conditions the
`AMQP 2 1 0 0` rows on a certificate alone. ADR-0122 decision 5 widens that to a certificate or TLS-SRP, and the code
does so (`ServerTlsComposition.IsTlsUpgradeAvailable`). Add a dated amendment or pointer note, in the form other ADRs
use; do not rewrite the recorded decision.

## Acceptance criteria

- [x] ADR-0107 decision 1 carries a dated note naming ADR-0122 decision 5 and saying TLS-SRP also makes the TLS upgrade available.
- [x] The wording of the original decision 1 table rows is unchanged.

## Notes

- Added the dated note under the decision 1 table and a pointer bullet in the ADR header. The diff
  only adds lines.
- Checked the note against ADR-0122 decisions 2 and 5 and Amendment 4, and against
  `ServerTlsComposition.IsTlsUpgradeAvailable` and its use in `ExtendedProtocolServers.cs`.
- The ADR's other stale statements (decision 7 under TLS-SRP, the ADR-0146 pointer and others) are
  filed as BL-1956, not changed here.
- Only Markdown changed, so the `verify` skill was not needed. `dotnet build` was clean (0 warnings)
  and every fast test project passed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0107 decision 1 now says TLS-SRP, as well as a certificate, makes amqp1 upgrade on a TLS header (ADR-0122 decision 5)
