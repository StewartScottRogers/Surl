---
id: BL-296
title: Answer SMB negotiate, session setup and tree connect in Surl.Protocol.Smb
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-283, BL-290, BL-294]
touches: [Surl.Protocol.Smb.UnitLibrary, Surl.Protocol.Smb.UnitTests]
requirement: FR-050
created: 2026-09-30
completed:
---
# BL-296 — Answer SMB negotiate, session setup and tree connect in Surl.Protocol.Smb

## Goal

`SmbProtocolServer` in `Surl.Protocol.Smb` implements `IConnectionProtocolServer` for `smb`, answers
upstream curl's `SMB_COM_NEGOTIATE`, `SMB_COM_SESSION_SETUP_ANDX` (checked through BL-294's
contract), `SMB_COM_TREE_CONNECT_ANDX` and `SMB_COM_TREE_DISCONNECT` as BL-283's ADR decides, and
enforces the ADR's limits, replaying request bytes recorded from the pinned build.

## Context

- Decision: BL-283's ADR (the negotiate response's fields, the challenge from an injected random
  source, the server time from the injected `TimeProvider`, share names and an unknown share's
  status, each refusal's status, the verbose notes, the limits and what each sends).
- Codec: BL-290's types. Contract: BL-294's, through the `ExchangeContext` or constructor injection
  as the ADR says; the server never references `Surl.Authentication` (ADR-0002 decision 3).
- Pattern: `Surl.Protocol.Ftp.UnitLibrary/FtpProtocolServer.cs` and its tests (a stateful session
  over `InMemoryConnection`, `RecordingExchangeLog`, idle timeout and maximum duration answered as
  ADR-0059 tells a limit from shutdown).
- Fixtures: record the requests of BL-283's cases again with `Record-CurlExchange.ps1` against the
  static-curl 8.21.0 Windows build (ADR-0030) and commit them as test data - bytes from upstream
  curl, never from the Curl port (ADR-0003). The LM and NT responses in a recording belong to the
  password the recording used; the test's account has that password.
- Files are not served yet (BL-297): an `SMB_COM_NT_CREATE_ANDX` here is answered with the ADR's
  not-found status so a session can end cleanly.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Smb.UnitTests` replay the recorded negotiate, session setup and tree
      connect of a login and answer them with the ADR's bytes; a refused login, an unknown share, a
      message past `--max-message`, the idle timeout and the maximum duration each answer as the
      ADR says; no test opens a socket.
- [ ] The verbose log notes the ADR lists are written through `IExchangeLog`, and no note carries a
      password or response bytes.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
