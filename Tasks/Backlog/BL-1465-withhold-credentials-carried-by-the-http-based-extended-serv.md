---
id: BL-1465
title: Withhold credentials carried by the HTTP-based extended servers from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1421]
touches: [Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1465 — Withhold credentials carried by the HTTP-based extended servers from the exchange log

## Goal

With the BL-1418 contract implemented, ADR-0182's replacement text is logged in place of the `Authorization`, `Proxy-Authorization` and API-key headers (S3 and the other cloud-storage servers, MCP, SignalR, Socket.IO, gRPC, WebTransport, DNS over HTTPS) on the shared HTTP serving layer, reusing the BL-1421 head redactor on -v, --trace, --trace-ascii and --log-file.

## Context

- Filed by BL-1417 (ADR-0182): an extended server whose client bytes carry a secret in recoverable form. The server's own parser shows where the field sits; the redactor must work across chunk boundaries and keep the user name visible.
- Extended servers are judged by their protocol's specification (ADR-0083), not the Curl port.

## Acceptance criteria

- [ ] A fast test in the server's .UnitTests shows the redacted bytes for a split-chunk exchange and that a server answer is unchanged.
- [ ] The server's raw bytes still reach the server unchanged and the event stream follows ADR-0182.

## Notes

## Log

- 2026-11-20: Created.
