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
completed:
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

- [ ] The measurement is saved as a fixture with its README row.
- [ ] A test replays it: after the recorded handshake is accepted on a session, a request with no
      `Authorization` on that session `Proceed`s as `tester`; on a new session it is challenged.
- [ ] 100% line and branch coverage kept in `Surl.Authentication.UnitLibrary`; no method exceeds
      complexity 10; the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
