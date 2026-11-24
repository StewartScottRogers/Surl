---
id: BL-1664
title: Refuse a forward proxy's request for its own port when it listens on port 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1664 — Refuse a forward proxy's request for its own port when it listens on port 0

## Goal

A `--proxy` (and a `socks*`) listener given port 0 refuses a request whose origin is its own bound end point as
`NotAllowed`, so the HTTP proxy answers `403` ("Origin <host>:<port> is not an allowed origin; answered 403."),
exactly as a proxy on a fixed port does, instead of forwarding the request to itself.

## Context

- Source: the Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-24T1330-Surl-bug-report.md`,
  finding 2 (Medium), against Surl v0.1.5 `d64faa4bb` on Windows 11, re-run 2026-11-24. Repro, copied from the
  report:
  - `surl -v --directory site http://127.0.0.1:0/ --proxy http://127.0.0.1:0/` (`site/hello.txt` present). The
    log prints two `Listening on` lines; here 59793 (files) and 59794 (proxy).
  - `curl -s -o /dev/null -w "%{http_code}" -x http://127.0.0.1:59794/ http://127.0.0.1:59794/hello.txt`
    prints `400`. The log shows `Forwarded GET to 127.0.0.1:59794; the origin answered 400.`
  - Expected: `403 NotAllowed`, as a proxy on a fixed port answers for its own address.
- Cause: `Application/Surl.Composition.UnitLibrary/ProxyComposition.cs`, `ComposeAllowedOriginRule`, adds each
  proxy listen URL's end point with `AllowedOriginRule.WithProxyEndPoint` only `Where(listenUrl => listenUrl.Port > 0)`,
  and the rule is built (in `SurlServerComposition.Start.cs`, `ComposeOriginConnector`) before the listeners
  bind, so a port-0 proxy's bound port is never refused. `AllowedOriginRule`
  (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/AllowedOriginRule.cs`) is immutable and
  `SocketOriginConnector` (`Foundation/Surl.Networking.UnitLibrary`) applies it to every resolved address.
- Rules: ADR-0088 decision 2 ("The proxy's own endpoint is never an origin ... refused as `NotAllowed`, so a
  request cannot loop through the proxy"). ADR-0088's implementation notes (around "a proxy on port 0 has no port
  to refuse until it binds, so a request to the proxy's own ephemeral port is not refused") record the gap this
  task closes; port 0 is how `Surl.Api` and every test stand-in start a proxy, so the gap must go.
- Approach (the lane may choose another that keeps the rule's check on resolved addresses): let the rule also
  refuse end points learned after binding, e.g. an `AllowedOriginRule` member that takes a source of bound proxy
  end points, which Composition fills from each proxy and `socks*` listener's `ListenUrl.BoundPort` once it
  binds. Keep `Surl.Networking` unchanged; no static mutable state (many instances run side by side).
- Stand-in: no store. The refusal is published as today's proxy exchange note on the instance's event stream; a
  test observes it by status `403` or the event. Applies equally under `Surl.Api` and `Surl.Mcp`.
- The `403` reply bytes are the ones the fixed-port refusal already sends; no new curl-facing bytes.

## Acceptance criteria

- [ ] Named tests in `Foundation/Surl.Protocol.Abstractions.UnitTests/AllowedOriginRuleTests.cs` show a rule
      refuses an end point learned after it was built (an IPv4 address, `0.0.0.0` on that port, and an
      IPv4-mapped IPv6 one), and admits the same port on another loopback address when the proxy bound a specific one.
- [ ] A named regression test in `Application/Surl.Composition.UnitTests` composes `http://127.0.0.1:0/` with
      `--proxy http://127.0.0.1:0/` through the in-memory listener and origin seams and shows the composed origin
      rule refuses the proxy's bound end point, and a second one shows the same for a `socks5://127.0.0.1:0/` listener.
- [ ] Existing `ProxyCompositionTests` for fixed-port proxies still pass unchanged.
- [ ] Notes record the decision, marked "Decided by Claude under Stewart's delegation", replacing ADR-0088's
      port-0 sentence, and the run files a `docs` task to amend ADR-0088 with it.
- [ ] `dotnet build -warnaserror` is clean for both libraries, `dotnet test --filter "TestCategory!=Integration"`
      is green, and both libraries keep 100% line and branch coverage on every member this task changes; no
      test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-11-24: Created.
