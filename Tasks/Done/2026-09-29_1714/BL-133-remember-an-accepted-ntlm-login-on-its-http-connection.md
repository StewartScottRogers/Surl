---
id: BL-133
title: Remember an accepted NTLM login on its HTTP connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-120]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-133 — Remember an accepted NTLM login on its HTTP connection

## Goal

Once NTLM has accepted a login on an HTTP connection, later requests on that connection that
carry no `Authorization` are served as that account, as upstream curl expects when it fetches
several URLs over one connection.

## Context

FR-014; ADR-0039 "Consequences" (BL-120). NTLM authenticates the connection, not the request:
after the handshake succeeds, curl 8.21.0 (`lib/http_ntlm.c`, state `NTLMSTATE_LAST`) sends no
`Authorization` on later requests over the same connection. Today `HttpAuthenticationSession`
answers such a request as having no credentials, so it is challenged again (ADR-0032 section
4, step 5).

- Measure first with `Record-CurlExchange.ps1 -ResponsesPerConnection 3`: two URLs with
  `--ntlm -u tester:secret`, the recorded `CHALLENGE_MESSAGE` from
  `Surl.Authentication.UnitTests/Fixtures/README.md`, then two `200`s - and record what the
  second request carries.
- Decide (ADR, amending ADR-0039) how the session remembers the account and whether the
  `IHttpCredentialVerifier` seam needs a way to vouch for a request without credentials; keep
  it inside `Surl.Authentication` (ADR-0032 decision 6) unless that proves impossible.

## Acceptance criteria

- [x] The measurement is saved as a fixture with its README row.
- [x] A test replays it: after the recorded handshake is accepted on a session, a request with no
      `Authorization` on that session `Proceed`s as `tester`; on a new session it is challenged.
- [x] 100% line and branch coverage kept in `Surl.Authentication.UnitLibrary`; no method exceeds
      complexity 10; the fast tests pass.

## Notes

- Measured (fixture `ntlm-two-urls`, port 18133): pinned curl 8.21.0 sent the NTLM handshake for
  `/x`, then `GET /y` on the same connection with no `Authorization`; exit 0, stdout `okok`.
- Decided in ADR-0041 (amends ADR-0039): `HttpAuthenticationSession` remembers the account a
  connection-authenticating method (`AuthenticationMethods.AuthenticatesConnection`, NTLM only)
  last accepted; a request without usable credentials proceeds as it, with no login note; any
  later NTLM `Authorization` replaces the login (none unless accepted). The
  `IHttpCredentialVerifier` seam is unchanged. Negotiate left out until measured: filed BL-135.
- Tests: `NtlmAuthenticationMethodTests.RecordedSecondUrl_*`, `NewHandshake_AfterALogin_ForgetsItUntilAccepted`,
  `AuthenticationMethodsTests.AuthenticatesConnection_IsTrueOnlyForNtlm`. Authentication 367 tests
  green; `Measure-CodeQuality.ps1`: `Surl.Authentication.UnitLibrary` 100% line, 100% branch,
  0 failing members, worst CRAP 10.
- Seen, not this task's: `dotnet format --verify-no-changes` reports end-of-line errors in
  `Surl.Output.UnitTests/VerboseLogEscapingTests.cs`, and `Measure-CodeQuality.ps1` one failing
  member in `Surl.Content.UnitLibrary`; neither file is in this task's `touches`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A connection NTLM logged in serves later requests without Authorization as that account, as curl sends a second URL (ADR-0041)
