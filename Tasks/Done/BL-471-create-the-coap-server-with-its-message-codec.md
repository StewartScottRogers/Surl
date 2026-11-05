---
id: BL-471
title: Create the CoAP server with its message codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-470, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Surl.slnx, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - RFC 7252 Appendix A's example messages encoded and decoded byte for byte;
      - option deltas of 12, 13 (one extension byte) and 269 (two bytes);
      - a payload marker with no payload refused as a format error;
      - option delta 15 refused;
      - a malformed CON answered with RST of the same message ID;
      - a malformed NON ignored.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered by hand against ADR-0097 (decisions 0 and 2), which already is the plan; no new ADR was
  needed. Upstream curl has no CoAP (ADR-0083), so every expected byte is written from RFC 7252: the
  Appendix A figures 16, 17, 20, 21, 22 and 23 (`CoapAppendixAExampleTests`), section 3.1's option
  format (`CoapMessageReaderTests`, `CoapMessageWriterTests`: deltas 12, 13, 268, 269 and 65535, the
  same for lengths), and sections 4.2 and 4.3 (`CoapProtocolServerTests.ServeAsync_MalformedCon_IsAnsweredWithRstOfTheSameMessageId`,
  `ServeAsync_MalformedNon_IsIgnored`).
- Types: `CoapMessage`, `CoapMessageType`, `CoapCode`, `CoapOption` (with `FromUInt` in the fewest
  bytes and `ReadUInt` reading leading zeros), `CoapMessageReader` returning a `CoapReadOutcome`
  (`Message`, `NotCoap`, `FormatError` with the header's type and Message ID), `CoapMessageWriter`,
  and `CoapProtocolServer` (scheme `coap`).
- Default taken: until BL-472 serves resources, a well-formed request is answered `4.04 Not Found`
  with its token (piggybacked ACK for a CON, NON with the flow's next Message ID for a NON), so a
  client gets a valid answer rather than retransmitting into silence. BL-472 replaces it.
- Default taken: each flow's first Message ID comes from an injected `Func<ushort>`
  (`RandomNumberGenerator` when none), as ADR-0097 decision 0 asks for an injected random source.
- `IDatagramRefusalWriter` (ADR-0097 decision 7's `5.03` refusal) is left to BL-476, which owns the
  limits and the composition; it is not in this task's acceptance criteria.
- Added `Documentation/Conformance/Extended/Coap.md` to `touches`: ADR-0097's Consequences make each
  task replace its ID in the matrix with the named tests. No task in `Doing` touches it. Every
  `BL-471` cell now names its tests.
- Coverage: `dotnet test --collect "Code Coverage;Format=cobertura"` reports line-rate 1 and
  branch-rate 1 for `Surl.Extended.Protocol.Coap.UnitLibrary` (105 tests).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.Coap encodes and decodes RFC 7252 messages and its coap server RSTs a malformed CON and ignores a malformed NON
