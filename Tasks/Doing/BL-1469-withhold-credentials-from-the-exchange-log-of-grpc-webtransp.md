---
id: BL-1469
title: Withhold credentials from the exchange log of gRPC, WebTransport and DNS over HTTPS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1423, BL-1424, BL-1465]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
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

- [ ] Each server implements `IExchangeBytesRedactingServer` and a fast test shows the redacted bytes.
- [ ] The raw bytes still reach the server unchanged.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
