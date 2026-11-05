---
id: BL-552
title: Create Surl.Protocol.HttpProxy and forward absolute-form requests and CONNECT tunnels
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-550, BL-551]
touches: [Surl.slnx, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Both projects build in `Surl.slnx` under `/Protocols/Http/`, and
      `Surl.Protocol.Abstractions.UnitTests`' reference rules cover the new server.
- [ ] Named tests drive the server through `InMemoryConnection` and the in-memory connector: a
      forwarded `GET` in absolute-form (the request bytes the origin receives and the response bytes
      curl receives), a `CONNECT` answered with the ADR's exact 200 reply followed by bytes relayed
      in both directions until either side closes, an unreachable origin (502), a refused origin and
      a refused tunnel port, each with the ADR's status line.
- [ ] The forwarded request's added and removed headers match the ADR, with a test per header rule.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary -warnaserror` is clean, the
      fast tests are green, and the library has 100% line and branch coverage, complexity of at most
      10 per method and CRAP of at most 30.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
