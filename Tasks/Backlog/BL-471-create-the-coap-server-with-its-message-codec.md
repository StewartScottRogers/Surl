---
id: BL-471
title: Create the CoAP server with its message codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-470, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-471 — Create the CoAP server with its message codec

## Goal

`Surl.Extended.Protocol.Coap.UnitLibrary` exists. It encodes and decodes CoAP messages (version, type, token
length, code, message ID, token, options with delta and length extensions, the payload marker). It implements
`IDatagramProtocolServer`, answering a malformed CON with RST and ignoring a malformed NON.

## Context

- Decision: BL-470's ADR. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Coap`. References:
  `Surl.Protocol.Abstractions` and `Surl.Content`.
- RFC 7252 section 3 (message format, Figure 7's option format, the 13 and 14 extended encodings, 15 reserved),
  section 4.2 and 4.3 (RST for a CON that cannot be processed, silent drop for a NON), section 5.4.1 (critical
  and elective options), and section 12.2 (option numbers).
- The datagram seam: `IDatagramProtocolServer` and `IDatagramFlow` (ADR-0004). Tests use the in-memory flow TFTP's
  tests use.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - RFC 7252 Appendix A's example messages encoded and decoded byte for byte;
      - option deltas of 12, 13 (one extension byte) and 269 (two bytes);
      - a payload marker with no payload refused as a format error;
      - option delta 15 refused;
      - a malformed CON answered with RST of the same message ID;
      - a malformed NON ignored.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
