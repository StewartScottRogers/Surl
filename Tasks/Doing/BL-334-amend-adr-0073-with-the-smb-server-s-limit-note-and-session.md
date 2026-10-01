---
id: BL-334
title: Amend ADR-0073 with the SMB server's limit note and session setup and tree connect strings
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-296]
touches: [Documentation/Planning/Decisions/ADR-0073-how-the-smb-server-answers-upstream-curl-and-checks-its-ntlmv1-session-setup.md]
requirement: FR-050
created: 2026-09-30
completed:
---
# BL-334 — Amend ADR-0073 with the SMB server's limit note and session setup and tree connect strings

## Goal

ADR-0073 states the choices BL-296 made where the ADR was silent or could not be built as
written, so the ADR is true of `Surl.Protocol.Smb` as it is.

## Context

- BL-296 (its Notes) built ADR-0073 decisions 1 to 5, 7 and 8 in `SmbProtocolServer`,
  `SmbSession` and `SmbExchange`.
- Decision 8 lists the notes `SMB connection closed: idle timeout` and `... maximum duration`,
  but the engine cancels the exchange alike for both (`ExchangeContext.IsCancelledForALimit`
  cannot tell them apart), so the server writes `SMB connection closed: idle timeout or
  maximum duration` for either, answering `ERRSRV/ERRerror` first when a request was being
  answered.
- Decision 1 does not name the session setup response's strings, nor the tree connect
  response's service and file system: the server sends an empty native OS, an empty LAN
  manager and the domain `SURL`, and service `A:` with an empty native file system.
- A session setup before any negotiate, and a second session setup after a login, are
  answered `ERRSRV/ERRerror`; decision 4's table does not list them.

## Acceptance criteria

- [ ] ADR-0073 decision 8's table has the one limit note `SMB connection closed: idle timeout
      or maximum duration` and says why, and keeps `SMB connection closed: head timeout`.
- [ ] ADR-0073 decision 1 (or a new row) names the session setup response's strings and the
      tree connect response's service and file system as `SmbSession` sends them.
- [ ] ADR-0073 decision 4's table lists a session setup before a negotiate or after a login
      as `ERRSRV/ERRerror`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
