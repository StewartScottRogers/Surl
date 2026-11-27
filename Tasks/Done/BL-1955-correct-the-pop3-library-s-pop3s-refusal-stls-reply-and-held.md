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
completed: 2026-11-27
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

- [x] The "Limits" remarks and `CLAUDE.md` say `WriteRefusalAsync` answers a refused plaintext `pop3` connection and a refused `pop3s` connection is closed with no reply.
- [x] The `isTlsUpgradeAvailable` param no longer says every `STLS` is answered `-ERR STLS not available` when it is `false`.
- [x] `CLAUDE.md` names `IExchangeBytesRedactingServer` among the interfaces `Pop3ProtocolServer` implements.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Delivered by `align-and-document`; diff checked in the lane session. Changed `Pop3ProtocolServer.cs` (`///` lines
  only) and the library's `CLAUDE.md`.
- Same false statements corrected beside the named ones: the "TLS" remarks (every `STLS` answered `+OK`), the
  `WriteRefusalAsync` summary, and the `CLAUDE.md` `STLS` bullet (order of refusals).
- Other inaccuracies found in the folder, outside this goal: filed as BL-1958 with the board script.
- Verify, 2026-11-27: 0 files rewritten to CRLF, build 0 warnings and 0 errors, format check exit 0, fast tests
  exit 0 with 0 failed. Integration tests not run: only comment and prose text changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. POP3 library comments and CLAUDE.md now state the pop3s refusal, STLS refusal order and redacting interface as the code does them
