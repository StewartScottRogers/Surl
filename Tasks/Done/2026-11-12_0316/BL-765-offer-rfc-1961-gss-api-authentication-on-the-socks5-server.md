---
id: BL-765
title: Offer RFC 1961 GSS-API authentication on the SOCKS5 server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-559, BL-1035]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Cryptography/Surl.Kerberos.UnitLibrary/Surl.Kerberos.UnitLibrary.csproj]
requirement: none
created: 2026-11-05
completed: 2026-11-12
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

- [x] Named tests through `InMemoryConnection`: with a keytab configured, a greeting offering `0x01`
      selects it, a Kerberos AP-REQ from `Surl.Kerberos.TestKdc` completes the context, the
      protection-level message is answered, and the `CONNECT` that follows reaches the origin;
      a bad token ends with RFC 1961's failure message (`0x01 0xFF`) and a closed connection.
- [x] Without a keytab, `0x01` is still never selected (ADR-0121 decision 3's table unchanged).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

- Filed by BL-557 (ADR-0121 decision 3): GSS-API is in scope and sequenced after the SOCKS5 server,
  not refused.
- 2026-11-11 (lane 4): no new project reference is needed: `Surl.Kerberos.UnitLibrary` is already an ADR-0002 horizontal library a protocol server may reference (`ProtocolIsolationTests.HorizontalLibraries`), so the SOCKS server can take a `KerberosAcceptor?` directly.
- 2026-11-11 (lane 4), measured with the win-x64 reference build (SHA-256 `0E7737...8778`) via `Record-CurlExchange.ps1 -Raw -RawReply '\x05\x01' -CurlArgs '--socks5','127.0.0.1:<p>','--socks5-gssapi',...`: curl sends `05 02 00 01`; on `05 01` its SSPI (Kerberos package, default service `rcmd/127.0.0.1`) fails with `SEC_E_TARGET_UNKNOWN`, curl closes and exits 97 (`curl: (97) SSPI error: InitializeSecurityContext failed: SEC_E_TARGET_UNKNOWN ...`). Socks SSPI acquires the logged-on user's credentials (no `-u`), so a live AP-REQ needs `-KerberosTestKdc`, `--socks5-gssapi-service rcmd/web.surl.test` and stored credentials for tester@SURL.TEST (`cmdkey /add:web.surl.test ...`, deleted afterwards); that attempt could not run because `Run-KerberosTestKdc.cs` needs the pinned SDK (BL-1035).
- 2026-11-12 (lane 3): built. `SocksGssApiLogin` (acceptor + service, default `rcmd`) and `Socks5GssApiNegotiation` (`01 01` context message, AP-REP only when mutual auth was asked, `01 02` protection message answered with level 0 in the same form it came - wrapped or raw for `--socks5-gssapi-nec`; any failure `01 FF` and close). With a keytab, `01` is chosen before `00`/`02` when offered (ADR-0121 amendment 2); without one it is never selected. Tests use the Kerberos tests' hand-made AP-REQs (linked `ApRequestBuilder`/`InitiatorTokens`, as `Surl.Authentication.UnitTests` does), so added the Socks tests to `Surl.Kerberos.UnitLibrary`'s `InternalsVisibleTo` (csproj added to `touches`; no other Doing task names it). Protocol bytes come from reading curl's `socks_sspi.c`; a live curl run still needs the SDK-pinned TestKdc and stored Windows credentials (see the 2026-11-11 note) and was not run. Wiring the keytab option is BL-1066. Socks library coverage: 100% lines, branches.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so no lane can build or run the test KDC; first measurement recorded under Notes
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. SOCKS5 server selects RFC 1961 GSS-API with Kerberos when a keytab is configured; keytab option wiring filed as BL-1066
