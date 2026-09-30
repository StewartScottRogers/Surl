---
id: BL-111
title: Challenge and verify HTTP Basic and Bearer credentials in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-110]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0035-basic-credentials-are-read-as-utf-8-and-bearer-tokens-as-the-bytes-sent.md, Documentation/Planning/Decisions/README.md]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-111 — Challenge and verify HTTP Basic and Bearer credentials in Surl.Authentication

## Goal

`Surl.Authentication` issues the `WWW-Authenticate: Basic` and `WWW-Authenticate: Bearer`
challenges ADR-0032 decision 4 fixes and verifies the `Authorization: Basic ...` and
`Authorization: Bearer ...` values upstream curl 8.21.0 sends for `-u name:password --basic`
and `--oauth2-bearer <token>`, both refused on an unencrypted connection unless
`--allow-plaintext-auth`.

## Context

FR-014; ADR-0032 (BL-100) decisions 3 and 4 (challenge text, realm, plain-text rule) and 2
(where Bearer tokens are configured). BL-110 built the account store and policy; this task
adds two methods behind BL-109's contract.

- Measure first, with `Record-CurlExchange.ps1` and the pinned reference build
  (`C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0; the script refuses any other),
  `-Connections 2`, a canned `401` carrying the decided challenge then a `200`:
  `-sS --basic -u tester:secret http://127.0.0.1:P/x`, `-sS -u tester:secret` (curl's
  default, which sends Basic without waiting), `-sS --oauth2-bearer tok http://127.0.0.1:P/x`,
  and a user name and password with non-ASCII and `:` characters. Save each `request.bin`
  as a fixture in `Surl.Authentication.UnitTests/Fixtures/<case>/` with a `README.md` naming
  the command line, build SHA-256 and date (the layout `Surl.Protocol.Http.UnitTests/Fixtures/README.md`
  uses).
- RFC 7617 (Basic, `charset="UTF-8"`), RFC 6750 (Bearer).
- Decoding uses `Convert.FromBase64String`/`TryFromBase64String`; a bad value is a refusal,
  never an exception.

## Acceptance criteria

- [x] Tests replay each measured `Authorization` value from the fixtures and prove it is
      accepted for the configured account or token, and refused for a wrong password, an
      unknown user, a malformed base64 value, a missing `:` and an empty token.
- [x] Tests prove Basic and Bearer are refused on an unencrypted connection without
      `--allow-plaintext-auth` and accepted with it, and accepted on an encrypted one.
- [x] Tests pin the exact challenge header values ADR-0032 decision 4 gives.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage kept; no test needs `TestCategory=Integration`.

## Notes

- Measured 2026-09-29 with the pinned reference build (SHA-256 `0E773709…8778`), five cases
  in `Surl.Authentication.UnitTests/Fixtures` (README there): `basic`, `user-default` (both
  `Basic dGVzdGVyOnNlY3JldA==`, sent unasked), `bearer` (`Bearer tok`), `basic-non-ascii`
  and `basic-utf8-config`. The Windows build sends a non-ASCII command-line argument in the
  ANSI code page (Windows-1252), a UTF-8 config file's bytes as UTF-8.
- Decided in ADR-0035 (Claude under Stewart's delegation): Basic's user-id is read as UTF-8,
  the charset announced, with no second charset tried; the password and the Bearer token are
  compared as the bytes sent. So `basic-non-ascii` is pinned as refused and
  `basic-utf8-config` as accepted.
- Added the ADR file and `Documentation/Planning/Decisions/README.md` (its index row) to
  `touches`: the decision needed an ADR. No task in `Doing` names either
  (BL-103: Cli and Console; BL-115: Mqtt).
- Default taken: both methods are stateless, so `StartConnection` returns the method itself;
  an accepted Bearer token's `AccountName` is the empty string, the token's account name
  (ADR-0032 section 1). Wiring them into `Surl.Console` is BL-117's.
- Tests: `BasicAndBearerAuthenticationTests`, 151 tests in the project pass; coverage of
  `Surl.Authentication.UnitLibrary` 100% line, 100% branch (`Measure-CodeQuality.ps1`).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Authentication challenges and verifies HTTP Basic and Bearer, replaying the Authorization values pinned upstream curl 8.21.0 sends
