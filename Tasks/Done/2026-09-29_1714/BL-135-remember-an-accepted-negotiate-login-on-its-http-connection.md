---
id: BL-135
title: Remember an accepted Negotiate login on its HTTP connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-133, BL-134]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-135 — Remember an accepted Negotiate login on its HTTP connection

## Goal

Once Negotiate has accepted a login on an HTTP connection, later requests on that connection
that carry no `Authorization` are served as that account, if pinned upstream curl sends them that way.

## Context

FR-014; ADR-0041 decision 2 (BL-133) remembers NTLM logins only, through
`AuthenticationMethods.AuthenticatesConnection`, because no Negotiate exchange from pinned
upstream curl had been recorded (ADR-0040, "Measured"). curl 8.21.0's `lib/http_negotiate.c`
appears to stop sending the header once the context is done (`GSS_AUTHDONE`/`GSS_AUTHSUCC`),
unless the server sends `Persistent-Auth: false`. Measure first, once BL-134 has found how to get
a Negotiate token out of the reference build: `Record-CurlExchange.ps1 -ResponsesPerConnection 3`
with two URLs and `--negotiate`.

## Acceptance criteria

- [x] The two-URL Negotiate measurement is saved as a fixture in
      `Surl.Authentication.UnitTests/Fixtures` with its README row.
- [x] `AuthenticationMethods.AuthenticatesConnection` answers as that measurement shows, and an
      ADR amending ADR-0041 records it.
- [x] A test replays it: after the recorded Negotiate handshake is accepted on a session, the
      recorded second request `Proceed`s as the account (or is challenged, if that is what curl
      expects).
- [x] 100% line and branch coverage kept in `Surl.Authentication.UnitLibrary`; the fast tests pass.

## Notes

- **Measured** with the unpatched 8.21.0 build (ADR-0042; the reference build sends no Negotiate token
  on the lane machine), `-Port 18135 -ResponsesPerConnection 3`, `--negotiate -u tester:secret` with
  `/x` and `/y`: bare NTLM handshake for `/x`, then `GET /y` with no `Authorization`, exit 0, `okok`.
  Fixture `negotiate-ntlm-two-urls`.
- **Decision (ADR-0044, decided by Claude under Stewart's delegation):** `AuthenticatesConnection` is
  true for Negotiate as for NTLM, whatever mechanism Negotiate carries, since curl's
  `http_negotiate.c` keeps the connection authenticated once the context completes and Surl never
  sends `Persistent-Auth: false`. ADR-0041 decision 2 and its index row carry the amendment note.
- Replayed in `NegotiateAuthenticationMethodTests` (second URL proceeds as `tester` with no login
  note; on a new connection it is challenged; a restarted handshake logs the connection out).
- `Surl.Authentication.UnitLibrary`: 100% line, 100% branch (`Measure-CodeQuality.ps1`); 435 tests.
- Recording note: calling the recorder through `powershell -File` with array arguments hung; call it
  in-process (`& .\Record-CurlExchange.ps1`, e.g. in a `Start-Job`) instead.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. An accepted Negotiate login is remembered by its HTTP connection, as the unpatched curl 8.21.0 build's two-URL --negotiate run shows (ADR-0044)
