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
completed: 2026-11-06
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

- [x] An ADR file records every decision under BL-379's `Notes` heading "Decisions", each with its
      reason and the RFC 9002 / RFC 9000 section it follows or departs from.
- [x] `Documentation/Planning/Decisions/README.md` has its row, next free number.
- [x] The ADR states nothing the code in `Surl.Quic.UnitLibrary` does not do.

## Notes

Wrote ADR-0140 (README row added). ADR-0137 is a gap in this checkout, likely held by another lane, so 0140, the next number after the highest, was taken. Every BL-379 decision was checked against the code as it is now; two later changes are folded in and named: `STREAM` after a reset is cut at the Reliable Size (BL-748), and `HandleTimeout` ends a closing period first (BL-608). Padded ACK-only packets are in flight per RFC 9002 section 2, which the ADR says rather than BL-379's "outside the window". Docs only; build clean, 78 fast test assemblies green.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0140 records Surl.Quic's loss detection and congestion control decisions from BL-379
