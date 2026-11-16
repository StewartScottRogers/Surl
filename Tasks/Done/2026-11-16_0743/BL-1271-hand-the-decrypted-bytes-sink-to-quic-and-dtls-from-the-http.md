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
completed: 2026-11-16
---
# BL-1271 — Hand the sink to QUIC and DTLS from the four servers

## Goal

The `https` server over HTTP/3, `webtransport`, `dns` over QUIC and `coaps` pass `context.DecryptedBytes` to the QUIC, HTTP/3 and DTLS code they construct, so their exchanges publish what the client sent.

## Context

ADR-0169 decisions 3 and 7. If the work is too large for one run, split it per server with `task-planner` and depend on the parts. Servers never reference each other.

## Acceptance criteria

- [x] A test per server: with a recording sink in the `ExchangeContext`, a client exchange reaches it (an HTTP/3 `:path`, a WebTransport stream payload, a DNS question, a CoAP request).
- [x] No byte a server sends changes: existing tests pass untouched.
- [x] 100% line and branch coverage kept in each library; `dotnet build` clean.

## Notes

Chosen: each server sets `DecryptedBytes = context.DecryptedBytes` on its QUIC options (https HTTP/3, its gateway upstream-test-case twin, WebTransport, DNS over QUIC) or DTLS options (coaps). HTTP/3 request streams already took the sink from the context (BL-1270). One test per server in its own test project.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Sink handed to QUIC and DTLS from the four servers; tests green
