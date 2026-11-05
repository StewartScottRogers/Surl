---
id: BL-832
title: Audit which protocol servers lack the chosen-error fault and file one task per family
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-828, BL-829, BL-830, BL-831, BL-793, BL-794, BL-795, BL-796, BL-797, BL-798]
touches: [Tasks/Backlog]
requirement: FR-062
created: 2026-11-05
completed:
---
# BL-832 — Audit which protocol servers lack the chosen-error fault and file one task per family

## Goal

Every protocol server composed by `Surl.Composition` either answers ADR-0124's `error` fault or has a task on the
board that makes it, so no server built after ADR-0124 is left without it.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decision 9:
  servers filed before the ADR and not built when it was accepted (STOMP, XMPP, gRPC, OCI, WebTransport,
  Socket.IO, SOCKS and any other) are caught here.
- Method: list the schemes `ComposeProtocolServers` and `ExtendedProtocolServers` register; list the schemes the
  `--fault` reply check in `Surl.Cli` accepts; for each scheme in the first and not the second, file one
  `feature` task per family (as BL-828 to BL-831 are written), depending on BL-823 and BL-785, adding the family's
  row to ADR-0124's reply table in the same task.
- This task changes no code; its record is the filed tasks and the Notes below.

## Acceptance criteria

- [ ] Notes list every registered scheme with either its wiring task (Done) or the task filed for it.
- [ ] Every filed task has exact `touches`, `depends-on` BL-823 and BL-785, and checkable acceptance criteria.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9).
