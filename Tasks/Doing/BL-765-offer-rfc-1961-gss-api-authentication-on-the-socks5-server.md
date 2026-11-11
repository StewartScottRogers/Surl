---
id: BL-765
title: Offer RFC 1961 GSS-API authentication on the SOCKS5 server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-559]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-765 — Offer RFC 1961 GSS-API authentication on the SOCKS5 server

## Goal

The SOCKS5 server offers method `0x01` (GSS-API, RFC 1961) with Kerberos when a keytab is
configured, so upstream curl's `--socks5-gssapi` and `--socks5-gssapi-service` complete their
exchange against it, as ADR-0121 decision 3 sequences.

## Context

- Decision: ADR-0121 decision 3 ("GSS-API is sequenced, not refused"). Until this task lands the
  server never selects `0x01`; curl 8.21.0 offers it on every SOCKS5 greeting (`05 02 00 01`,
  measured in ADR-0121).
- Specification: RFC 1961 sections 3 to 5 (context establishment messages `0x01 0x01 <len> <token>`,
  the protection-level negotiation, and the per-message encapsulation, which curl's `socks_gssapi.c`
  at tag `curl-8_21_0` sends as "no protection" by default and `--socks5-gssapi-nec` changes).
- Code: the Kerberos acceptor in `Cryptography/Surl.Kerberos.UnitLibrary` (`KerberosAcceptor`,
  `GssApiToken`), as the HTTP server's Negotiate check uses it; `Surl.Kerberos.TestKdc` issues the
  tickets in tests. Whether the SOCKS server may reference `Surl.Kerberos` directly or reaches it
  through `Surl.Authentication` is decided against ADR-0002 decision 3's table as ADR-0121 amends
  it; if a new reference is needed, amend that table in this task's ADR note.
- Bytes: measured from a pinned upstream curl 8.21.0 build with `Kerberos` (or `GSS-API`) in its
  features, with `Record-CurlExchange.ps1`; never the Curl port (ADR-0003).
- No upstream test case at tag `curl-8_21_0` uses `--socks5-gssapi`.

## Acceptance criteria

- [ ] Named tests through `InMemoryConnection`: with a keytab configured, a greeting offering `0x01`
      selects it, a Kerberos AP-REQ from `Surl.Kerberos.TestKdc` completes the context, the
      protection-level message is answered, and the `CONNECT` that follows reaches the origin;
      a bad token ends with RFC 1961's failure message (`0x01 0xFF`) and a closed connection.
- [ ] Without a keytab, `0x01` is still never selected (ADR-0121 decision 3's table unchanged).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

- Filed by BL-557 (ADR-0121 decision 3): GSS-API is in scope and sequenced after the SOCKS5 server,
  not refused.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
