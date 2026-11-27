---
id: BL-1802
title: Close DNS over QUIC and WebTransport connections gracefully on a close=graceful drop fault
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1801]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-26
completed:
---
# BL-1802 — Close DNS over QUIC and WebTransport connections gracefully on a close=graceful drop fault

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

- [ ] A graceful `after-bytes` rule on a WebTransport flow ends in `CONNECTION_CLOSE` `NO_ERROR`, pinned by a unit test
      over an in-memory datagram flow.
- [ ] The same for a DNS-over-QUIC flow served as `surl` composes it.
- [ ] A plain UDP DNS flow still publishes `FaultClose.Reset`, pinned by a test.
- [ ] ADR-0188 amendment 5 and the `--aihelp` `testing` fault paragraph say which datagram flows close gracefully.

## Notes

## Log

- 2026-11-26: Created.
