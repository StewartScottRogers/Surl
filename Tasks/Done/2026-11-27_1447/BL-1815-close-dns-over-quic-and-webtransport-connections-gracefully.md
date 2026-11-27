---
id: BL-1815
title: Close DNS over QUIC and WebTransport connections gracefully on a close=graceful drop fault
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1801]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Documentation/Planning/Decisions, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Application/Surl.Api, Documentation/Wiki/Surl-As-A-Stand-In.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1815 — Close DNS over QUIC and WebTransport connections gracefully on a close=graceful drop fault

## Goal

A `drop;after-bytes=N;close=graceful` rule closes a DNS-over-QUIC and a WebTransport connection with
`CONNECTION_CLOSE` `NO_ERROR` after byte N, as it closes an HTTP/3 one, and the event says `Graceful`.

## Context

BL-1801 (ADR-0188 amendment 5) added `IGracefulCutDatagramProtocolServer`, which only `Http3ProtocolServer`
implements. `DnsOverQuicProtocolServer` and `WebTransportProtocolServer` ride the same `Surl.Quic`
`QuicDatagramFlowServer`, which already closes a cut flow, but they do not declare the marker, so their flows are only
silenced and the event says `Reset`. DNS over QUIC is served through `Surl.Composition`'s
`DnsDatagramRoutingProtocolServer`, which also serves plain UDP DNS on a datagram flow, so the marker must reach the
engine only for flows that are QUIC. A test uses this to cut a DNS-over-QUIC or WebTransport answer cleanly.

## Acceptance criteria

- [x] A graceful `after-bytes` rule on a WebTransport flow ends in `CONNECTION_CLOSE` `NO_ERROR`, pinned by a unit test
      over an in-memory datagram flow.
- [x] The same for a DNS-over-QUIC flow served as `surl` composes it.
- [x] A plain UDP DNS flow still publishes `FaultClose.Reset`, pinned by a test.
- [x] ADR-0188 amendment 5 and the `--aihelp` `testing` fault paragraph say which datagram flows close gracefully.

## Notes

- 2026-11-27 plan: the engine picks one datagram server per scheme and judged "graceful" by server type alone, so the
  `dns` router could not say yes for DNS over QUIC and no for plain UDP. `IGracefulCutDatagramProtocolServer` gains a
  default member `ClosesCutFlowsGracefully(ListenUrl)` (default `true`), which the engine asks with the flow's listen
  URL and the router answers with `IsImplicitTls`. `DnsOverQuicProtocolServer` and `WebTransportProtocolServer`
  declare the marker; `Surl.Quic` already closes a cut flow.
- 2026-11-27 `touches` widened (no task in `Doing` names any of them; BL-1922 is `Surl.Quic` only):
  `Surl.Protocol.Abstractions` and `Surl.Core` with their tests for the contract member and the engine's question;
  `Surl.Api` (README and `SurlFault.Close` doc) and the stand-in wiki page, which said every datagram protocol but
  HTTP/3 only stops answering.
- 2026-11-27 decision recorded as ADR-0188 amendment 6 (Decided by Claude under Stewart's delegation).
- 2026-11-27 pins: WebTransport `WebTransportProtocolServerTests.ServeAsync_FlowCutByAGracefulDropFault_...`; DNS over
  QUIC as composed `SurlServerCompositionTests.ServeAsync_GracefulDropRuleOnADnsOverQuicFlow_...`; plain UDP
  `SurlServerCompositionTests.ServeAsync_GracefulDropRuleOnAPlainUdpDnsFlow_...`. The two server-level cut tests pass
  without the marker (`Surl.Quic` closes any cut flow it is given); the marker tests and the composition and engine
  tests failed before the change. `OfferDoq` moved to `DnsOverQuicClientHello.cs`, linked into
  `Surl.Composition.UnitTests`.
- 2026-11-27 the `/feature` plan, test and implement stages were done in the session, not by subagents (a change of this
  size); `code-reviewer` found nothing that must be fixed and its should-fix items are fixed. Conformance audit
  skipped: upstream curl speaks neither DNS over QUIC nor WebTransport, and no HTTP/3 byte changed.
- 2026-11-27 gates: `dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30326 passed.
  `Measure-CodeQuality.ps1` was not run.

## Log

- 2026-11-26: Created.
- 2026-11-26: Renumbered from BL-1802 to BL-1815; an archived task holds BL-1802.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A close=graceful after-bytes drop closes DNS-over-QUIC and WebTransport connections with CONNECTION_CLOSE NO_ERROR; plain UDP DNS stays silenced with Reset.
