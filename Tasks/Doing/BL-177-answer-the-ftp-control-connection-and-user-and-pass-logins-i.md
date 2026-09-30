---
id: BL-177
title: Answer the FTP control connection and USER and PASS logins in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-173]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-038
created: 2026-09-29
completed:
---
# BL-177 — Answer the FTP control connection and USER and PASS logins in Surl.Protocol.Ftp

## Goal

`Surl.Protocol.Ftp` has an `FtpProtocolServer` (`IConnectionProtocolServer`, scheme `ftp`) that
greets, reads bounded command lines, logs users in with `USER`/`PASS` only through the
authentication contract, and answers the commands that need no data connection (`SYST`,
`FEAT`, `OPTS`, `PWD`, `CWD`, `CDUP`, `TYPE`, `MODE`, `STRU`, `NOOP`, `HELP`, `QUIT`, and any
other the ADR lists), as BL-173's ADR decides.

## Context

- Decisions: BL-173's ADR (greeting, every reply code and text, login mapping, the
  `CheckedLogin` word); ADR-0032 "Protocol servers not yet built" criteria 1 to 3 (every login
  through `IAuthenticationPolicy.CheckPasswordLoginAsync`; with no account every login refused;
  `PASS` over a connection whose `TlsSession` is null refused unchecked without
  `--allow-plaintext-auth` - the policy returns `RefusedPlaintext`); ADR-0038 (write the login
  note); ADR-0006 sections 1, 3 and 5 (FTP column: a line past `ExchangeLimits.MaxLineBytes`
  answered `500` then close, never reading past the limit; the head timeout, the idle timeout
  and the maximum duration answered `421` then close; nothing a peer may not learn).
- `CWD` checks the directory through the content store, a hidden or `/.surl` directory answered
  as missing (ADR-0006 section 2, ADR-0031 decision 5): add the `ProjectReference` to
  `Surl.Content.UnitLibrary` (ADR-0002 allows it) and take a `ContentStore` in the constructor.
- Seam: ADR-0004 (`InMemoryConnection`); fixtures from BL-173's recordings under
  `Surl.Protocol.Ftp.UnitTests/Fixtures/<case>/` with a `README.md` (build, SHA-256, command
  line, date), replayed line by line.
- Tests use `AnonymousAuthenticationPolicy` and a recording test double (as
  `Surl.Protocol.Mqtt.UnitTests/UnitTestRecordingAuthenticationPolicy.cs` does); never
  `Surl.Authentication` (ADR-0002).
- Code to copy (never expectations): the Curl port's `FtpReply.cs`, `FtpControlChannel.cs`,
  turned to the server's side.

## Acceptance criteria

- [ ] A fast test replays each recorded control-channel fixture that needs no data connection
      (login, `PWD`, `CWD`, `TYPE`, `QUIT`) and asserts surl's replies are the ADR's, byte for
      byte.
- [ ] Fast tests cover: a login accepted, refused (`RefusedCredentials`, `RefusedAnonymous`) and
      refused as plain-text, each with the ADR's reply and the login note; a command before
      login; an unknown command; a line past `MaxLineBytes`; the head timeout on a fake
      `TimeProvider`; `CWD` to a missing, a hidden and the `/.surl` directory.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Ftp.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
