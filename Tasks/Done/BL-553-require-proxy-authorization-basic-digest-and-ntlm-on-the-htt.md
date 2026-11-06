---
id: BL-553
title: Require Proxy-Authorization Basic, Digest and NTLM on the HTTP proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Named tests through `InMemoryConnection`: no credentials gives 407 with the ADR's
      challenges; valid Basic, Digest and NTLM credentials each forward the request and each tunnel
      a `CONNECT`; wrong credentials give 407 again; the NTLM handshake keeps one connection across
      its legs; the `Proxy-Authorization` header is not forwarded to the origin.
- [x] Without configured accounts the proxy asks for no credentials, as the ADR decides (secure by
      default is the ADR's call), with a named test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

- **Touches widened** to ADR-0088's file: decision 5 left the CONNECT Digest `uri` to be measured
  here, so the result is recorded as its Amendment 3. No task in Doing names that file.
- **Measured** (pinned Windows curl 8.21.0, `Record-CurlExchange.ps1`, fixtures `proxy-*` in
  `Surl.Protocol.HttpProxy.UnitTests/Fixtures`): curl's Digest `uri` is `/a?b` for a forwarded
  request and the authority `origin.test:8080` for a `CONNECT`; `-U` sends Basic unasked on the
  `CONNECT` and none inside the tunnel; `--proxy-ntlm` completes all three legs on one connection;
  curl completes every exchange against the exact 407 bytes Surl writes (exit 0, or 7 for an
  unanswered `CONNECT` 407).
- **Decided** (ADR-0088 Amendment 3, by Claude under Stewart's delegation): the proxy takes an
  `IAuthenticationPolicy`, as the HTTP server does, so it needs no `Surl.Authentication` reference
  and the isolation test (in `Surl.Protocol.Abstractions.UnitTests`, which BL-565 touches) no new
  row. The one-argument constructor passes `AnonymousAuthenticationPolicy` - no proxy account, no
  login. The session sees `Authorization` removed and `Proxy-Authorization` renamed
  `Authorization`, and the target curl uses as Digest `uri`, so the existing exact-`uri` Digest
  check passes unchanged. Login is judged after the `Host` check and before the target's checks.
  407 keeps the connection unless the request asked to close or announced a body; 403 closes.
- **Tests** use a hand-written `UnitTestProxyAuthenticationPolicy` (Basic, Digest by `uri`, NTLM
  legs bound to the session), as `Surl.Protocol.Http.UnitTests` does for the HTTP server;
  `Surl.Authentication`'s real checks have their own tests. BL-555 composes the real policy from
  `-U`, `--proxy-user-file` and `--proxy-auth`.
- **Upstream cases that send `Proxy-Authorization`** (read from each case's `<verify>` section,
  where it sits in `<proxy>` or `<protocol>`): 80, 275 and 1849, all Basic. The other 21 send none.
- Coverage: `Surl.Protocol.HttpProxy.UnitLibrary` 100% line and 100% branch. The six members
  `Measure-CodeQuality.ps1` flags for complexity (`OriginResponseHead.ParseStatusLine`,
  `ProxyTarget.ParseAuthority`, `OriginLink.ReadFieldsAsync`, `OriginResponseBody.Of`,
  `OriginResponseHead.ParseFieldLine`, `AnswerForwardableAsync`) predate this task and are untouched.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The HTTP proxy answers 407 with Proxy-Authenticate and accepts Basic, Digest and NTLM Proxy-Authorization on forwarded requests and CONNECT, as pinned curl 8.21.0 sends them
