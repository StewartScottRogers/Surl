---
id: BL-1271
title: Hand the decrypted-bytes sink to QUIC and DTLS from the https HTTP/3, WebTransport, DNS-over-QUIC and coaps servers (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1267, BL-1268, BL-1269, BL-1270]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1271 — Hand the sink to QUIC and DTLS from the four servers

## Goal

The `https` server over HTTP/3, `webtransport`, `dns` over QUIC and `coaps` pass `context.DecryptedBytes` to the QUIC, HTTP/3 and DTLS code they construct, so their exchanges publish what the client sent.

## Context

ADR-0169 decisions 3 and 7. If the work is too large for one run, split it per server with `task-planner` and depend on the parts. Servers never reference each other.

## Acceptance criteria

- [ ] A test per server: with a recording sink in the `ExchangeContext`, a client exchange reaches it (an HTTP/3 `:path`, a WebTransport stream payload, a DNS question, a CoAP request).
- [ ] No byte a server sends changes: existing tests pass untouched.
- [ ] 100% line and branch coverage kept in each library; `dotnet build` clean.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
