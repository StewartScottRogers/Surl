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
completed:
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

- [ ] The two-URL Negotiate measurement is saved as a fixture in
      `Surl.Authentication.UnitTests/Fixtures` with its README row.
- [ ] `AuthenticationMethods.AuthenticatesConnection` answers as that measurement shows, and an
      ADR amending ADR-0041 records it.
- [ ] A test replays it: after the recorded Negotiate handshake is accepted on a session, the
      recorded second request `Proceed`s as the account (or is challenged, if that is what curl
      expects).
- [ ] 100% line and branch coverage kept in `Surl.Authentication.UnitLibrary`; the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
