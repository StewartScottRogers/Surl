---
id: BL-215
title: Point the allowed-reference lists at ADR-0048's four SSH primitive libraries
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-148]
touches: [CLAUDE.md, .claude/agents/protocol-architect.md, Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-215 — Point the allowed-reference lists at ADR-0048's four SSH primitive libraries

## Goal

Every document that lists the libraries a protocol server may reference agrees with
ADR-0002 decision 3's table as ADR-0048 amended it.

## Context

- ADR-0048 (BL-148) adds `Surl.Cryptography.Curve25519`, `.Ed25519`, `.ChaCha20` and
  `.Poly1305` `.UnitLibrary` to ADR-0002 decision 3's reference table.
- Three documents still list only `Surl.Content` and `Surl.Cryptography`:
  root `CLAUDE.md` ("Solution-wide conventions", the protocol-server reference rule),
  `.claude/agents/protocol-architect.md` (its protocol-server reference rule), and
  `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` (its opening paragraph says "over the
  hand-built primitives in `Surl.Cryptography`", and its reference paragraph).
- Prefer pointing at ADR-0002's table over repeating the list, so the next amendment
  does not need this task again. `Surl.Cryptography.UnitLibrary/CLAUDE.md` and
  `ProtocolIsolationTests` are BL-149's, not this task's.

## Acceptance criteria

- [ ] Root `CLAUDE.md`'s protocol-server reference rule names ADR-0002 decision 3's
      table (as amended by ADR-0048) as the list of allowed horizontal libraries.
- [ ] `.claude/agents/protocol-architect.md` says the same.
- [ ] `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` names the four ADR-0048 libraries as the
      home of the hand-built primitives and as allowed references.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
