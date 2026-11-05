---
id: BL-783
title: Pad DNS responses on the encrypted transports
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-483]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-783 — Pad DNS responses on the encrypted transports

## Goal

A DNS over HTTPS answer to a query whose OPT carried the EDNS(0) Padding option carries a Padding option of its own
that makes the whole message a multiple of 468 bytes, and the same padding is available to DoT and DoQ; UDP and TCP
answers are never padded.

## Context

- Decision: ADR-0092 decision 2 (EDNS(0) bullet): RFC 7830's Padding option is answered on the encrypted transports
  only, by RFC 8467 section 4.1's recommended policy (responses to padded queries padded to a multiple of 468 bytes).
- `DnsOption.PaddingCode` (12) exists; nothing pads yet. `DnsQueryAnswerer.AnswerOverHttp` and
  `DnsOverHttpsHandler` (BL-483) are the DoH path; BL-484's DoQ path needs the same writer.
- The padding is counted after any cut `DnsResponseWriter.WriteWithin` makes, and the OPT stays last (RFC 6891 section 7).

## Acceptance criteria

- [ ] A named test sends a DoH `POST` whose OPT carries an empty Padding option and checks the answer's length is a
      multiple of 468 and its OPT holds one Padding option of zero bytes making it so (RFC 7830 section 3).
- [ ] A named test shows a DoH query without Padding gets an unpadded answer, and the UDP and TCP servers never pad.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-483, which built DoH without padding to keep to its acceptance criteria.

## Log

- 2026-11-05: Created.
