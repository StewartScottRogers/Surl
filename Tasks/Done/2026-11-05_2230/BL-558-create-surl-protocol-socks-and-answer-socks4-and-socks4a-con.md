---
id: BL-558
title: Create Surl.Protocol.Socks and answer SOCKS4 and SOCKS4a CONNECT requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-557, BL-551]
touches: [Surl.slnx, CLAUDE.md, Documentation/Product/Product-Overview.md, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-558 — Create Surl.Protocol.Socks and answer SOCKS4 and SOCKS4a CONNECT requests

## Goal

`Surl.Protocol.Socks.UnitLibrary` exists with its `.UnitTests` twin and an
`IConnectionProtocolServer` that answers SOCKS4 and SOCKS4a requests as BL-557's ADR decisions 2
and 6 say, relaying bytes between curl and the origin it reaches through BL-551's outbound
connector.

## Context

- Decision: BL-557's ADR, decisions 1, 2 and 6. `touches` assumes the ADR's planning folder
  `Protocols/Proxy/`; if the ADR chooses another, work there and say so in Notes.
- New project steps: add both projects to `Surl.slnx` in the solution folder named after their
  category folder, in ordinal order; reference `Surl.Protocol.Abstractions` and only what the ADR's
  amended ADR-0002 table allows; update `Surl.Protocol.Abstractions.UnitTests`' project-reference
  rules for the new server; give the project its `CLAUDE.md`; if the category folder is new, add it
  to root `CLAUDE.md`'s "Repository layout" tree as the ADR names.
- Bytes: measured from pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1` (`--socks4`,
  `--socks4a`), never from the Curl port.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `socks4`: 700, 702, 706, 708, 729.
  BL-562 runs them.

## Acceptance criteria

- [x] Both projects build in `Surl.slnx`, and `Surl.Protocol.Abstractions.UnitTests`' reference rules
      cover the new server.
- [x] Named tests through `InMemoryConnection` and BL-551's in-memory connector: a SOCKS4 `CONNECT`
      to an IPv4 address gets reply `0x5A` and relays bytes both ways; a SOCKS4a request carrying a
      host name is resolved and connected; an unreachable or refused origin gets `0x5B`; a request
      over the ADR's user ID or host name limit, and a truncated request, end as the ADR says.
- [x] `dotnet build Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and the library has 100% line and branch coverage, complexity of at most 10
      per method and CRAP of at most 30.

## Notes

- 2026-11-05, BL-557: ADR-0121 decision 1 adopts `Protocols/Proxy/`; this task also adds `Proxy/` to the product overview's list of `Protocols/` folders, so `touches` gains `Documentation/Product/Product-Overview.md`.
- 2026-11-05, BL-558: built in `Protocols/Proxy/` as ADR-0121 decision 1 says. Bytes re-measured
  with `Record-CurlExchange.ps1 -Raw` against the pinned win-x64 curl 8.21.0 and committed as
  fixtures (`socks4-connect`, `socks4-user-id`, `socks4a-host-name`, `socks4-rejected`); they
  match the ADR's rows 1 to 4.
- Choices taken (sensible defaults, within ADR-0121): `Schemes` lists only `socks4` and
  `socks4a` until BL-559 adds SOCKS5, so every listener is a SOCKS4 listener and a `05` greeting
  is answered `05 FF` (decision 5). The handshake timer starts after the version byte (decision 6,
  "from the first byte"); the origin connect gets `HeadTimeout` through the connector, whose
  `TimedOut` is `5B`. A truncated request closes without a reply. Port 0 is refused `5B` without
  calling the connector (the connector takes ports 1 to 65535). An empty SOCKS4a host name is
  refused `5B` as `HostNotFound` without calling the connector. Bytes curl sends after the request
  are held by `SocksClientReader` and forwarded first.
- Left to BL-559: decision 2's "a listener with proxy accounts answers every SOCKS4 request `5B`
  unless `--allow-anonymous`" needs the accounts policy BL-559 brings in with decision 4, so this
  server has no account rule yet. Left to BL-562: `SocksUpstreamTestCaseReplies`.
- Gates: 47 tests in `Surl.Protocol.Socks.UnitTests`; `Measure-CodeQuality.ps1 -Library
  Surl.Protocol.Socks.UnitLibrary`: 100% line, 100% branch, max complexity 8, 0 failing members.
  Fast tests green solution-wide.

## Log

- 2026-11-04: Created.
- 2026-11-05: touches gains Documentation/Product/Product-Overview.md (ADR-0121 decision 1).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Protocol.Socks answers SOCKS4 and SOCKS4a CONNECT (5A and relay, 5B refusals, 255-byte names, head timeout)
