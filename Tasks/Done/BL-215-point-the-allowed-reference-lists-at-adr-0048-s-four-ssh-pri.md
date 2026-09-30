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
completed: 2026-09-29
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

- [x] Root `CLAUDE.md`'s protocol-server reference rule names ADR-0002 decision 3's
      table (as amended by ADR-0048) as the list of allowed horizontal libraries.
- [x] `.claude/agents/protocol-architect.md` says the same.
- [x] `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` names the four ADR-0048 libraries as the
      home of the hand-built primitives and as allowed references.

## Notes

- Delivered in-session rather than through `align-and-document`: three short prose edits, no code. Each rule now points at ADR-0002 decision 3's table "as later ADRs amend it" and names ADR-0048 (and ADR-0050, which amended the same table since this task was filed) only as examples, so the next amendment does not need these documents changed. The SSH CLAUDE.md names the four libraries explicitly, as the criterion asks, and says the rest comes from the BCL (ADR-0048 decision 3).
- Follow-up filed: BL-219, for the fifteen protocol `CLAUDE.md` files and three `.claude` files that still restate or allude to the old two-library list.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Root CLAUDE.md, protocol-architect and the SSH CLAUDE.md point at ADR-0002 decision 3's amended table; the SSH doc names ADR-0048's four primitive libraries
