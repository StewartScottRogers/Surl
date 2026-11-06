---
id: BL-687
title: Record how Surl.Quic detects loss and controls congestion in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-379]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-687 — Record how Surl.Quic detects loss and controls congestion in an ADR

## Goal

An ADR in `Documentation/Planning/Decisions`, marked "Decided by Claude under Stewart's
delegation", records the loss recovery and congestion control decisions BL-379 made in
`Surl.Quic.UnitLibrary`, with its row in the Decisions `README.md` index.

## Context

- BL-379 built RFC 9002 loss detection and NewReno in `Surl.Quic` but could not write the ADR:
  BL-470 held `Documentation/Planning/Decisions` at the time. BL-379's `Notes` list the
  decisions; `Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md` ("Loss recovery") describes them
  as built. ADR-0082 decision 4 (NewReno, initial window 10 x 1200, no pacing) is what it builds on.

## Acceptance criteria

- [ ] An ADR file records every decision under BL-379's `Notes` heading "Decisions", each with its
      reason and the RFC 9002 / RFC 9000 section it follows or departs from.
- [ ] `Documentation/Planning/Decisions/README.md` has its row, next free number.
- [ ] The ADR states nothing the code in `Surl.Quic.UnitLibrary` does not do.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
