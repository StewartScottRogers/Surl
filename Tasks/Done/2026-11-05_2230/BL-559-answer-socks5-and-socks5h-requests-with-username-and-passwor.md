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
completed: 2026-11-05
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

- [x] Named tests through `InMemoryConnection`: method negotiation offering no authentication, and
      username/password when accounts are configured; `0xFF` when no offered method is acceptable;
      `CONNECT` with ATYP 1, 3 and 4 each reaching the origin and relaying bytes; RFC 1929 success
      and failure; each reply code the ADR lists (`0x01` to `0x08`) sent in the case the ADR names;
      `BIND` and `UDP ASSOCIATE` answered as the ADR decides.
- [x] A truncated greeting, a request over the ADR's host-name limit and the handshake timeout (with
      a fake `TimeProvider`) end as the ADR says, each with a named test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

- 2026-11-05: Delivered in `Surl.Protocol.Socks`: `Socks5Exchange` (method selection, RFC 1929
  login, request, replies `00` to `08`, relay), `Socks5RequestReader`, `Socks5Request`,
  `Socks5ReadOutcome`, `Socks5Reply`, `Socks5ReplyCode` and `SocksProxyAccounts`. The server's
  schemes are now `socks4`, `socks4a`, `socks5`, `socks5h`, and the listener's scheme picks the
  version it serves (ADR-0121 decision 5): a SOCKS4 request on a SOCKS5 listener gets `00 5B`.
- Seven SOCKS5 fixtures recorded from pinned upstream curl 8.21.0 (win-x64, SHA-256 `0E77…8778`)
  with `Record-CurlExchange.ps1 -Raw`; every byte matched ADR-0121's table (rows 5 to 11).
  `Fixtures/README.md` lists the command lines.
- Choices taken (sensible defaults, within ADR-0121):
  - Proxy accounts arrive as an optional `IAuthenticationPolicy` in a second
    `SocksProtocolServer` constructor; `null` means the listener has no accounts. The ADR's
    "accounts and `--allow-anonymous`" row is detected by asking the policy about a login with
    no user name and no password: `AcceptedUnchecked` means anonymous use is allowed. That
    question carries no credentials, so `AuthenticationPolicy` neither delays it nor publishes a
    `LoginJudgedEvent`. The same question applies decision 2's rule that SOCKS4 is refused
    `5B` on a listener with accounts unless `--allow-anonymous`.
  - The plaintext refusal and the 1-second delay after a wrong password are the policy's
    (`RefusedPlaintext`, `AuthenticationPolicy.RefusalDelay`), as for every other server; the
    SOCKS server writes the ADR's log line for `RefusedPlaintext` and `CheckedLogin` notes for
    checked logins, never the password.
  - The RFC 1929 user name is decoded as UTF-8 (accounts are named on the command line as
    text); a domain name as Latin-1, as SOCKS4a's host name is.
  - `0.0.0.0`, `::` and port 0 are answered `02` before any origin is tried, matching SOCKS4's
    refusals; an empty domain name `04`; `ATYP` other than `01`, `03`, `04` is answered `08`
    without reading further. A request with an unsupported command is read whole, then `07`.
  - "A request over the host-name limit": SOCKS5's domain-name length is one byte, so 255 is
    its bound by construction (decision 6); the named test is
    `Socks5ExchangeTests.ServeAsync_DomainNameOf255Bytes_IsConnected`.
- Coverage of `Surl.Protocol.Socks.UnitLibrary`: 100% line, 100% branch (106 tests).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Protocol.Socks answers SOCKS5 and SOCKS5h CONNECT to IPv4, IPv6 and domain names with every reply code, and RFC 1929 username/password logins through the proxy accounts
