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
completed: 2026-09-29
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

- [x] A fast test replays each recorded control-channel fixture that needs no data connection
      (login, `PWD`, `CWD`, `TYPE`, `QUIT`) and asserts surl's replies are the ADR's, byte for
      byte.
- [x] Fast tests cover: a login accepted, refused (`RefusedCredentials`, `RefusedAnonymous`) and
      refused as plain-text, each with the ADR's reply and the login note; a command before
      login; an unknown command; a line past `MaxLineBytes`; the head timeout on a fake
      `TimeProvider`; `CWD` to a missing, a hidden and the `/.surl` directory.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Ftp.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

- Built: `FtpProtocolServer` (`IConnectionProtocolServer`, `IConnectionRefusalWriter`, scheme
  `ftp`), `FtpLineReader` (adapted from `DictLineReader`, returning bytes so a password reaches
  the policy as sent), `FtpCommandLine`, `FtpPath` and `FtpCommandResponder`. References
  `Surl.Content.UnitLibrary` as the Context says. 128 fast tests; coverage 100% line and 100%
  branch, 63 members, worst CRAP 10, 0 failing.
- Fixtures: four sessions recorded 2026-09-29 from the pinned Windows build with
  `Record-CurlExchange.ps1 -Ftp`, each fed the ADR's exact replies (`Fixtures/README.md`):
  `login-cwd-missing` (exit 9), `quote-type-cwd` (`-Q` TYPE A / TYPE I / NOOP / SYST, exit 9),
  `login-refused` (exit 67) and `login-refused-plaintext` (exit 67). Measured: after a `530` to
  `PASS` curl closes without `QUIT`. `RecordedExchangeTests` replays each whole and one byte per
  read, against the `< ` lines of `transcript.txt`.
- Defaults taken (ADR-0052 leaves them open, or they stage the ADR across tasks):
  - `FEAT` lists only what this server does now (` TVFS`, ` UTF8`); BL-178 to BL-181 add their
    lines as they build them, so `FEAT` never advertises a command that answers 502. Same for
    `HELP`, which lists the commands answered, on one line between `214-The following commands
    are recognized:` and `214 End`.
  - Every command a later task builds (`EPSV`, `RETR`, `AUTH`, `STOR`, `SITE`, ...) answers
    `502 Command not implemented` until then, which is true now.
  - `PASS` after a login is `503 Already logged in`, as `USER` is. The name `USER` gave is spent
    by one `PASS` whatever the verdict, so a refused client sends `USER` again (RFC 959).
  - `PASS` with no argument checks an empty password rather than answering 501: curl sends a
    bare `PASS` for an empty password, and only the policy judges.
  - `MODE`/`STRU` with another word: `504 Mode not supported` / `504 Structure not supported`.
  - The 1-second delay on `RefusedCredentials` is the policy's own (`AuthenticationPolicy`
    waits it), so the server adds none.
  - The head timeout starts when the connection is served for the first line and at the first
    byte for later ones, as `Surl.Protocol.Dict` and `Surl.LineProtocol` do.
- Follow-up filed: BL-228, `421 Timeout, closing` on the idle timeout and maximum duration
  (ADR-0052 decision 10), which this task's criteria left out.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FtpProtocolServer greets, reads bounded command lines, logs in with USER/PASS through IAuthenticationPolicy, and answers PWD, CWD, CDUP, TYPE, MODE, STRU, SYST, FEAT, OPTS, NOOP, HELP, ALLO, ACCT and QUIT
