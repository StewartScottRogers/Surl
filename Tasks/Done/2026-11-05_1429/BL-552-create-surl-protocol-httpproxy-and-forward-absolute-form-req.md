---
id: BL-552
title: Create Surl.Protocol.HttpProxy and forward absolute-form requests and CONNECT tunnels
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-550, BL-551]
touches: [Surl.slnx, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-552 — Create Surl.Protocol.HttpProxy and forward absolute-form requests and CONNECT tunnels

## Goal

`Surl.Protocol.HttpProxy.UnitLibrary` exists with its `.UnitTests` twin and an
`IConnectionProtocolServer` that forwards absolute-form HTTP/1.1 requests to the origin and
tunnels `CONNECT` requests, as BL-550's ADR decisions 3 and 4 say, reaching the origin through
BL-551's outbound-connection seam.

## Context

- Decision: BL-550's ADR, decisions 1 to 4 and 7's limits. The library name and folder are the
  ADR's; `touches` assumes `Protocols/Http/`.
- New project steps: add both projects to `Surl.slnx` under `/Protocols/Http/` in ordinal order,
  reference `Surl.Protocol.Abstractions`, `Surl.HttpMessage` and whatever else the ADR's amended
  ADR-0002 table allows, and update `Surl.Protocol.Abstractions.UnitTests`' project-reference rules
  so the new server is held to "protocol servers never reference each other". The project's
  `CLAUDE.md` names what the library holds.
- Code to reuse: `Protocols/Http/Surl.HttpMessage.UnitLibrary` (request reading, response writing),
  `InMemoryConnection` and BL-551's in-memory connector for tests.
- Bytes: request and reply bytes are measured from pinned upstream curl 8.21.0 with
  `Record-CurlExchange.ps1` (curl `-x http://127.0.0.1:<port>` and `-p`), never from the Curl port.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `http-proxy`: 80, 83, 95, 275, 445,
  714, 715, 744, 1078, 1230, 1287, 1288, 1297, 1319, 1320, 1321, 1428, 1849, 1904, 2050, 2055,
  2107, 3028, 3106. BL-556 runs them.

## Acceptance criteria

- [x] Both projects build in `Surl.slnx` under `/Protocols/Http/`, and
      `Surl.Protocol.Abstractions.UnitTests`' reference rules cover the new server.
- [x] Named tests drive the server through `InMemoryConnection` and the in-memory connector: a
      forwarded `GET` in absolute-form (the request bytes the origin receives and the response bytes
      curl receives), a `CONNECT` answered with the ADR's exact 200 reply followed by bytes relayed
      in both directions until either side closes, an unreachable origin (502), a refused origin and
      a refused tunnel port, each with the ADR's status line.
- [x] The forwarded request's added and removed headers match the ADR, with a test per header rule.
- [x] `dotnet build Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary -warnaserror` is clean, the
      fast tests are green, and the library has 100% line and branch coverage, complexity of at most
      10 per method and CRAP of at most 30.

## Notes

- Built `Surl.Protocol.HttpProxy.UnitLibrary` (references Abstractions and `Surl.HttpMessage` only;
  `Surl.Authentication` waits for BL-553, which also has to allow it in `ProtocolIsolationTests`)
  and its `.UnitTests` twin, both in `Surl.slnx` under `/Protocols/Http/` after `Surl.Protocol.Http`.
- Measured: `Fixtures/` holds five exchanges recorded today with `Record-CurlExchange.ps1` against
  the pinned Windows reference build (forwarded `GET`, `CONNECT` plus the tunnelled `GET`, the
  forwarded `502`, and the `CONNECT` `502` and `403` curl exits 7 on). The tests replay curl's bytes
  and assert Surl writes exactly the bytes curl was measured accepting.
- Tests (172): `HttpProxyProtocolServerTests` (the fixture cases, the `CONNECT` failures, the tunnel),
  `HttpProxyExchangeTests` (forwarding, bodies, 1xx, `101`, reconnect, origin failures, timeouts),
  `ProxyFieldRulesTests` (one test per header rule), and one class per helper. 100% line and branch
  coverage of the library (coverage collector, cobertura); CA1502 clean at build.
- `ProtocolIsolationTests` gains `ProtocolServers_IncludeTheHttpProxy` and two rows forbidding the
  proxy and the HTTP server from referencing each other.
- Choices, recorded as ADR-0088 Amendment 1 (Decided by Claude under Stewart's delegation): `Host`
  generated from the authority (RFC 9112 3.2.2's MUST; same bytes for curl); `Proxy-Authorization`
  always stripped; trailers dropped; `Max-Forwards` decremented only on `OPTIONS`/`TRACE`; fixed-text
  answer format; which answers keep curl's connection; reconnect rule; `Expect` handling;
  `Schemes` = `http`, `https`.
- `touches` widened: ADR-0088's file, for that amendment. No task in `Doing` names it.
- Left for later tasks, already filed: auth (BL-553), HTTPS proxy (BL-554), command line and
  composition (BL-555), upstream cases (BL-556), `ftp://` forwarding (BL-594).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Protocol.HttpProxy forwards absolute-form HTTP/1.1 requests and tunnels CONNECT through IOriginConnector, proven against bytes from pinned curl 8.21.0, 100% covered
