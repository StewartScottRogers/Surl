---
id: BL-1851
title: Bring ADR-0080's amendment list and its https and rtsp rows up to date with the code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1851 — Bring ADR-0080's amendment list and its https and rtsp rows up to date with the code

## Goal

ADR-0080's header lists every amendment to it, and amendments say where its `https` and `rtsp`
rows differ from the code.

## Context

Found during BL-1835 (2026-11-27). Check each against the documents and code before editing:

- The header's "Amended:" lines omit the inline amendments of 2026-11-11 (BL-858) and 2026-11-14
  (BL-1129), and ADR-0123, ADR-0155 and ADR-0156, which the decisions index says amend ADR-0080.
- Decision 2's `https` row names only the `test-localhost` certificate; a case's `<server>` line can
  name another (BL-1130).
- Decision 2's `rtsp` row says `Surl.Protocol.Rtsp` plays the server half, `rtp:` server commands
  included; no such server half was found, and the run composes none. Every case naming an `rtsp`
  server at the tag is a libtest program.
- ADR-0154's row in the decisions index says the HTTP/2 listeners use the `test-localhost`
  certificate; `%HTTP2TLSPORT` serves a certificate a case's `https` line names, if it has one.

Add dated amendments; leave the decision text as it is.

## Acceptance criteria

- [ ] ADR-0080's header lists each amendment that the decisions index or the ADR's body records.
- [ ] A dated amendment, marked "Decided by Claude under Stewart's delegation", says which
      certificate the `https` half serves and that the run composes no `rtsp` server half.
- [ ] The decisions index's ADR-0154 row agrees with the certificate the HTTP/2 listeners serve.

## Notes

## Log

- 2026-11-27: Created.
