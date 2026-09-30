---
id: BL-219
title: Point the remaining allowed-reference lists at ADR-0002 decision 3's amended table
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-215]
touches: [.claude/agents/protocol-implementer.md, .claude/commands/protocol.md, .claude/skills/new-project/SKILL.md, Surl.Protocol.Dict.UnitLibrary/CLAUDE.md, Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md, Surl.Protocol.Gopher.UnitLibrary/CLAUDE.md, Surl.Protocol.Http.UnitLibrary/CLAUDE.md, Surl.Protocol.Imap.UnitLibrary/CLAUDE.md, Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md, Surl.Protocol.Mqtt.UnitLibrary/CLAUDE.md, Surl.Protocol.Pop3.UnitLibrary/CLAUDE.md, Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md, Surl.Protocol.Smb.UnitLibrary/CLAUDE.md, Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md, Surl.Protocol.Telnet.UnitLibrary/CLAUDE.md, Surl.Protocol.Tftp.UnitLibrary/CLAUDE.md, Surl.Protocol.Ws.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-219 — Point the remaining allowed-reference lists at ADR-0002 decision 3's amended table

## Goal

No document other than ADR-0002 itself restates the list of horizontal libraries a
protocol server may reference as only `Surl.Content` and `Surl.Cryptography`; each points
at ADR-0002 decision 3's table as later ADRs amend it.

## Context

- ADR-0048 and ADR-0050 amended ADR-0002 decision 3's table (four SSH primitive
  libraries, then `Surl.LineProtocol` and `Surl.MailStore`).
- BL-215 fixed root `CLAUDE.md`, `.claude/agents/protocol-architect.md` and
  `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md`; use their wording ("the horizontal libraries
  in ADR-0002 decision 3's table, as later ADRs amend it") as the model.
- Found by `grep -rn "ADR-0002 lists"` on 2026-09-29: the files in `touches`. Every
  protocol `CLAUDE.md` there says "the horizontal libraries ADR-0002 lists
  (`Surl.Content.UnitLibrary`, `Surl.Cryptography.UnitLibrary`)"; the three `.claude`
  files say "the horizontal libraries ADR-0002 lists" and may already be true, but check
  `new-project/SKILL.md` step 7's list.
- `ProtocolIsolationTests.cs` doc comments are BL-149's, not this task's.

## Acceptance criteria

- [x] `grep -rn "Surl.Content.UnitLibrary\`,\s*$" Surl.Protocol.*/CLAUDE.md` and a read of
      each file in `touches` finds no restated two-library list; each names ADR-0002
      decision 3's table as amended.
- [x] `.claude/skills/new-project/SKILL.md` step 7 names the table rather than a fixed list.

## Notes

- 2026-09-29: Replaced the restated two-library list in 14 protocol CLAUDE.md files (Mqtt keeps its explicit Surl.Content reference for ADR-0031 and points at the table for the rest) and the "ADR-0002 lists" wording in the three .claude files with BL-215's wording. Grep for "ADR-0002 lists" in Surl.Protocol.*/CLAUDE.md and .claude now finds nothing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Every protocol CLAUDE.md and the three .claude docs point at ADR-0002 decision 3's amended table instead of restating a two-library list
