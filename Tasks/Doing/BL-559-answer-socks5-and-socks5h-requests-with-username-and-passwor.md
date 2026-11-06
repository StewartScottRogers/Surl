---
id: BL-559
title: Answer SOCKS5 and SOCKS5h requests with username and password authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-558]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-559 — Answer SOCKS5 and SOCKS5h requests with username and password authentication

## Goal

The SOCKS server answers SOCKS5 as RFC 1928 and BL-557's ADR decision 3 say - method selection,
`CONNECT` to IPv4, IPv6 and domain-name addresses (curl's SOCKS5h), every reply code - and RFC 1929
username/password authentication when accounts are configured (decision 4).

## Context

- Decision: BL-557's ADR, decisions 3, 4 and 6. Specifications: RFC 1928 sections 3 to 6, RFC 1929
  section 2.
- Code: the server BL-558 created in `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary`, which tells
  SOCKS4 from SOCKS5 by the version byte; accounts through the `IAuthenticationPolicy` and
  `PasswordLogin` types in `Surl.Protocol.Abstractions`, as the other servers check passwords.
- Bytes: measured from pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1` (`--socks5`,
  `--socks5-hostname`, `--proxy-user`), never from the Curl port.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `socks5`: 701, 703, 707, 709, 710,
  711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 742, 2055; and `socks5unix`: 1467, 1468, 1470
  (the same server on BL-547's Unix listener). BL-562 runs them.

## Acceptance criteria

- [ ] Named tests through `InMemoryConnection`: method negotiation offering no authentication, and
      username/password when accounts are configured; `0xFF` when no offered method is acceptable;
      `CONNECT` with ATYP 1, 3 and 4 each reaching the origin and relaying bytes; RFC 1929 success
      and failure; each reply code the ADR lists (`0x01` to `0x08`) sent in the case the ADR names;
      `BIND` and `UDP ASSOCIATE` answered as the ADR decides.
- [ ] A truncated greeting, a request over the ADR's host-name limit and the handshake timeout (with
      a fake `TimeProvider`) end as the ADR says, each with a named test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
