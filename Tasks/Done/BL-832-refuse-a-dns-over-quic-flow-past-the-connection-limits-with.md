---
id: BL-832
title: Refuse a DNS over QUIC flow past the connection limits with DOQ_EXCESSIVE_LOAD
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-484, BL-1019]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-832 — Refuse a DNS over QUIC flow past the connection limits with DOQ_EXCESSIVE_LOAD

## Goal

A DNS over QUIC flow past `--max-connections` or `--max-connections-per-address` is answered with a
`CONNECTION_CLOSE` carrying `DOQ_EXCESSIVE_LOAD` (4), as ADR-0092 decision 6's DoQ column says,
instead of being disposed with no reply.

## Context

- ADR-0092 decision 6 (the limits table, DoQ column) and decision 5; RFC 9250 section 4.3.
- `DnsOverQuicProtocolServer` (BL-484) does not implement `IDatagramRefusalWriter`
  (`Surl.Protocol.Abstractions`), so `Surl.Core`'s serving engine disposes a flow past a limit
  with no bytes, as it does for HTTP/3 today.
- Before the handshake only Initial packets can be sent, and RFC 9000 section 10.2.3 turns an
  application close sent there into a transport `CONNECTION_CLOSE` `APPLICATION_ERROR` (0x0c);
  decide (in an ADR amendment if needed) whether the refusal answers the client's first Initial
  with that, with `CONNECTION_REFUSED` (0x02), or completes the handshake first. `Surl.Quic`
  may need a way to send a close in reply to a first Initial without serving the connection.

## Acceptance criteria

- [x] `DnsOverQuicProtocolServer` implements `IDatagramRefusalWriter`, and a named test over the
      in-memory QUIC test connection (`DnsOverQuicTestClient`) shows a refused flow receives the
      decided `CONNECTION_CLOSE`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep
      100% line and branch coverage.

## Notes

Decision (BL-832): CONNECTION_REFUSED (0x02) in the first Initial, shared with HTTP/3 via QuicConnectionRefusal; ADR-0092 amendment 2. DOQ_EXCESSIVE_LOAD cannot reach a client before the handshake (RFC 9000 10.2.3).

Filed by BL-484 (dark factory lane 6, 2026-11-05): BL-484 built the DoQ server; the limit refusal
sits in the engine's refusal seam, which BL-484's acceptance criteria did not cover.

## Log

- 2026-11-05: Created.
- 2026-11-06: depends-on BL-485 replaced by BL-1019 (BL-485 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. DoQ flows past connection limits get a CONNECTION_REFUSED Initial via IDatagramRefusalWriter (ADR-0092 amendment 2)
