---
id: BL-1270
title: Publish HTTP/3 decoded header lists and DATA payloads from Surl.Http3 through the decrypted-bytes sink (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1266]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1270 — Publish HTTP/3 decoded headers through the sink

## Goal

`Surl.Http3` reports each decoded request or response header list as a `Decoded` chunk and each DATA frame payload as a `Decrypted` chunk on the same stream.

## Context

ADR-0169 decision 1. Header chunk text: one `name: value` line per header ending CRLF, pseudo-headers first in received order, then a blank line. QPACK keeps the stream bytes unreadable, which is why this layer exists. The sink is optional, defaulting to discarding; frames sent are byte-identical.

## Acceptance criteria

- [ ] A test: a received request's header list is reported as `Decoded` with the stream id and the exact text above, `:path` included.
- [ ] A test: a response's headers sent are reported as `Decoded`, direction sent.
- [ ] A test: DATA payloads in both directions are reported as `Decrypted` on their stream.
- [ ] A test: frames sent are identical with the discarding sink and a recording one.
- [ ] 100% line and branch coverage kept; `dotnet build` clean.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
