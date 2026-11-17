---
id: BL-1350
title: Cover the server composition gaps in Surl.Composition
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1349]
touches: [Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1350 — Cover the server composition gaps in Surl.Composition

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 12 failing members. BL-1349 takes
  the five behind the control endpoint; this task takes these seven and closes the library.
- `SurlServerComposition.ComposeSocksGssApiLogin(SurlCommandLine, SurlServerCompositionSeams)` -
  `SurlServerComposition.Start.cs:322`, 83.33% branch; line 323 is 3 of 4 (`seams.ReadStartFile ?? File.ReadAllBytes`
  and the `acceptor is null` check do not see every outcome).
- `<TlsSrpCertificateOf>b__30_0(X509Certificate2)` - `ServerTlsComposition.cs:242`, 0% line: no TLS-SRP certificate is
  composed with an intermediate.
- `<.cctor>b__22()` - `ExtendedProtocolServers.cs:129`, 0% line: the `() => ComposeDnsOverQuic(services)` factory the
  `dns` registration hands `DnsDatagramRoutingProtocolServer` is never invoked through the registration.
- `<.cctor>b__23(SignalRHubPath)` - `ExtendedProtocolServers.cs:171`, 0% line, lines 171 to 174: the SignalR hub factory
  never runs.
- `SignalRHubRoutingProtocolServer.ServeAsync(IConnection, ExchangeContext)` - `SignalRHubRoutingProtocolServer.cs:60`,
  50% line, lines 63 and 64 uncovered: nothing serves a connection through the hub router.
- `IdentifierRandomNumberGenerator.GetBytes(Span<byte>)` - `IdentifierRandomNumberGenerator.cs:17`, 0% line.

## Acceptance criteria

- [ ] Named `Surl.Composition.UnitTests` tests cover every outcome of line 323 in `ComposeSocksGssApiLogin`: a keytab
      read through the seam, no seam (the `File.ReadAllBytes` fallback, without touching the disk unless the test is
      `Integration`), and a keytab that yields no acceptor (`null`).
- [ ] A named test composes TLS-SRP over a `--cert` chain with at least one intermediate and asserts the intermediate's
      bytes follow the leaf's in the composed certificate (line 242).
- [ ] A named test serves a connection through the composed `signalr` registration, so `SignalRHubRoutingProtocolServer.ServeAsync`
      (lines 63, 64) and the hub factory (lines 171 to 174) run, and asserts the hub at the listen URL's path answers.
- [ ] A named test reaches the DNS-over-QUIC factory at line 129 through the composed `dns` registration, and a named test
      calls `IdentifierRandomNumberGenerator.GetBytes(Span<byte>)` and asserts the bytes come from the seeded source.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests only (test-writer). Use `StartTestSeams` and `FakeListenerFactory` as BL-1056 did.

## Log

- 2026-11-17: Created.
