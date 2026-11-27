---
id: BL-1955
title: Correct the POP3 library's pop3s refusal, STLS reply and held-types statements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1946]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1955 — Correct the POP3 library's pop3s refusal, STLS reply and held-types statements

## Goal

The POP3 library's comments and `CLAUDE.md` say what the code does about a refused `pop3s` connection and about the
`STLS` replies, and `CLAUDE.md` names every interface `Pop3ProtocolServer` implements.

## Context

Found while working BL-1946 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change.
In `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary`:

- `Pop3ProtocolServer.cs`, remarks, "Limits", and `CLAUDE.md`, "What it holds": a connection past a connection limit is
  said to be answered by `WriteRefusalAsync`, without qualification. `ServingEngine.RefuseAsync`
  (`Foundation/Surl.Core.UnitLibrary/ServingEngine.cs`) calls the writer only when the listen URL is not implicit TLS, so
  a refused `pop3s` connection is closed with no reply (ADR-0006, section 5).
- `Pop3ProtocolServer.cs`, the `isTlsUpgradeAvailable` param: when `false`, `STLS` is said to be answered
  `-ERR STLS not available`. `Pop3Session.AnswerStlsAsync` first answers `-ERR Already logged in`,
  `-ERR Invalid arguments` or `-ERR Already using TLS` when one of those applies.
- `CLAUDE.md`, "What it holds": `Pop3ProtocolServer` also implements `IExchangeBytesRedactingServer`.

No store, event or fault is changed: documentation only.

## Acceptance criteria

- [ ] The "Limits" remarks and `CLAUDE.md` say `WriteRefusalAsync` answers a refused plaintext `pop3` connection and a refused `pop3s` connection is closed with no reply.
- [ ] The `isTlsUpgradeAvailable` param no longer says every `STLS` is answered `-ERR STLS not available` when it is `false`.
- [ ] `CLAUDE.md` names `IExchangeBytesRedactingServer` among the interfaces `Pop3ProtocolServer` implements.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
