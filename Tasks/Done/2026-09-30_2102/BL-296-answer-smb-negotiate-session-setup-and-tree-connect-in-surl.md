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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Smb.UnitTests` replay the recorded negotiate, session setup and tree
      connect of a login and answer them with the ADR's bytes; a refused login, an unknown share, a
      message past `--max-message`, the idle timeout and the maximum duration each answer as the
      ADR says; no test opens a socket.
- [x] The verbose log notes the ADR lists are written through `IExchangeLog`, and no note carries a
      password or response bytes.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

- **Shape.** `SmbProtocolServer` (public, `IConnectionProtocolServer`, schemes `smb` and
  `smbs`) takes a `ContentStore`, an `ISmbAuthenticationPolicy` (constructor injection, as the
  FTP server takes its policy) and an optional `ISmbChallengeSource` (production
  `SmbSystemChallengeSource`, `RandomNumberGenerator.Fill`). Per connection, `SmbExchange`
  runs the frame loop and the limits, and `SmbSession` answers each request. The library now
  references `Surl.Content.UnitLibrary` (the change is in its csproj, inside this task's
  touches), as the FTP server does; ADR-0002's table allows it.
- **`--max-message`.** `SmbFrameReader` used to stop at a frame over the limit. ADR-0073
  decision 7 wants the header answered and the session to go on, so the reader now keeps the
  first 32 bytes, discards the rest (bounded by the 17-bit length) and reports
  `MessageTooLarge` with the announced length. A too-large frame of another type is reported
  as `UnexpectedFrameType`.
- **Idle timeout or maximum duration.** The engine cancels the exchange the same way for both,
  so the server cannot tell them apart. It writes `SMB connection closed: idle timeout or
  maximum duration` either way. When a request was being answered, it sends `ERRSRV/ERRerror`
  first (decision 7's maximum-duration row); when none was, it sends nothing (the idle row).
  Filed BL-334 to bring decision 8's wording into line.
- **Choices the ADR left open** (sensible defaults, also in BL-334): the session setup response
  sends an empty native OS, an empty LAN manager and the domain `SURL`. The tree connect
  response sends service `A:` and an empty native file system, so nothing about the host goes
  out (ADR-0006 section 3). A session setup before the negotiate, or after a login, is answered
  `ERRSRV/ERRerror`. A NetBIOS keep-alive before the negotiate does not stop the head-timeout
  clock.
- **Fixtures** (`Surl.Protocol.Smb.UnitTests/Fixtures`, README there): `login-tree-connect`
  (78), `login-refused` (67) and `unknown-share` (78), recorded with `Record-CurlExchange.ps1
  -Raw` from the static-curl 8.21.0 Windows pin. Each was fed the exact bytes the server sends
  (challenge `0123456789ABCDEF`, clock 2026-09-30 12:00 UTC). The exit codes match ADR-0073
  rows 14, 10 and 11. The recorder needed no extension.
- **Verified.** `dotnet build -warnaserror` is clean and the fast tests are green (126 SMB
  tests). `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smb.UnitLibrary` reports line 100%,
  branch 100%, 0 failing members, worst CRAP 8.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SmbProtocolServer answers curl's negotiate, NTLMv1 session setup, tree connect and tree disconnect as ADR-0073 decides, with its limits and notes
