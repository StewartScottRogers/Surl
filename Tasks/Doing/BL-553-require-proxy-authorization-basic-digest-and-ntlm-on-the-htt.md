---
id: BL-553
title: Require Proxy-Authorization Basic, Digest and NTLM on the HTTP proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-553 — Require Proxy-Authorization Basic, Digest and NTLM on the HTTP proxy

## Goal

When accounts are configured, the HTTP proxy answers a request or `CONNECT` without valid
credentials with `407 Proxy Authentication Required` and a `Proxy-Authenticate` challenge for
each scheme the ADR offers, and accepts Basic, Digest and NTLM `Proxy-Authorization` as upstream
curl 8.21.0's `--proxy-basic`, `--proxy-digest`, `--proxy-ntlm` and `--proxy-anyauth` send them.

## Context

- Decision: BL-550's ADR, decision 5. Specifications: RFC 9110 sections 11.7.1 and 11.7.2
  (`Proxy-Authenticate`, `Proxy-Authorization`) and 15.5.8 (407); RFC 7617 (Basic); RFC 7616
  (Digest); MS-NLMP (NTLM, which is connection-bound, so the proxy keeps the connection open across
  its three legs).
- Code to reuse: `Foundation/Surl.Authentication.UnitLibrary` and the `IAuthenticationPolicy`,
  `HttpAuthenticationRequest`, `HttpAuthenticationVerdict` and `IHttpAuthenticationSession` types
  in `Surl.Protocol.Abstractions`, which the HTTP server already checks `Authorization` with. If a
  change to `Surl.Authentication` is needed, file it as its own task and depend on it, rather than
  widening this one.
- Bytes: challenges and the client's headers are measured from pinned upstream curl 8.21.0 with
  `Record-CurlExchange.ps1`, never from the Curl port.
- Upstream test cases that wait on this: the `http-proxy` cases of BL-556 that send
  `Proxy-Authorization` (of 80, 83, 95, 275, 445, 714, 715, 744, 1078, 1230, 1287, 1288, 1297,
  1319, 1320, 1321, 1428, 1849, 1904, 2050, 2055, 2107, 3028, 3106); this task lists in Notes which
  of them do, read from their `<protocol>` sections.

## Acceptance criteria

- [ ] Named tests through `InMemoryConnection`: no credentials gives 407 with the ADR's
      challenges; valid Basic, Digest and NTLM credentials each forward the request and each tunnel
      a `CONNECT`; wrong credentials give 407 again; the NTLM handshake keeps one connection across
      its legs; the `Proxy-Authorization` header is not forwarded to the origin.
- [ ] Without configured accounts the proxy asks for no credentials, as the ADR decides (secure by
      default is the ADR's call), with a named test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
