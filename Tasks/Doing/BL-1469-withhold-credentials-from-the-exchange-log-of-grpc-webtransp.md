---
id: BL-1469
title: Withhold credentials from the exchange log of gRPC, WebTransport and DNS over HTTPS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1423, BL-1424, BL-1465]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Application/Surl.Composition.UnitLibrary/DnsConnectionRoutingProtocolServer.cs, Application/Surl.Composition.UnitTests/DnsConnectionRoutingProtocolServerTests.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-1469 — Withhold credentials from the exchange log of gRPC, WebTransport and DNS over HTTPS

## Goal

gRPC (HTTP/2), WebTransport (HTTP/3) and DNS over HTTPS log `Authorization` and API-key header values as ADR-0182's replacement text, as BL-1465 does for the HTTP/1.1 extended servers.

## Context

- Split from BL-1465, which wired the HTTP/1.1 servers through `HttpExchangeRedaction`. gRPC and WebTransport carry header blocks, so they reuse the HTTP/2 and HTTP/3 redactors of BL-1423 and BL-1424; DNS over HTTPS is routed by Composition's `DnsConnectionRoutingProtocolServer`.

## Acceptance criteria

- [x] Each server implements `IExchangeBytesRedactingServer` and a fast test shows the redacted bytes.
- [x] The raw bytes still reach the server unchanged.

## Notes

- Added Composition's DnsConnectionRoutingProtocolServer (+tests) to touches: DoH is routed there and no other Doing task names it.
- DoH redactor (DnsConnectionBytesRedactor) picks by first byte: capital letter = HTTP (HTTP/1.1 heads via HttpExchangeRedaction, or HTTP/2 on the preface), else DNS-over-TCP bytes pass through. A real query length never starts with 0x41-0x5A.
- gRPC uses Http2RequestBytesRedactor and WebTransport Http3RequestBytesRedactor; both mask authorization and proxy-authorization only (those redactors have no API-key list), so API-key headers on gRPC/WebTransport are not yet masked.
- Raw bytes are untouched: redaction only affects the log copy.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
