---
id: BL-481
title: Answer DNS queries over UDP with truncation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-480]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-481 — Answer DNS queries over UDP with truncation

## Goal

The DNS server answers queries over UDP through `IDatagramProtocolServer`. It echoes the ID and question,
truncates with TC at 512 bytes, or at the EDNS(0) payload size when the query has OPT, answers FORMERR and NOTIMP,
and drops what RFC 1035 says to drop.

## Context

- Decision: BL-478's ADR, decisions 3 and 6.
- RFC 1035 section 4.2.1 (UDP, 512 bytes), section 4.1.1 (header bits, RCODE), RFC 6891 sections 6.2.3 to 6.2.5
  (payload size, and the OPT in the response), and RFC 2181 section 9 (TC only when the answer set does not fit).
  A message with QR = 1 is never answered.
- The datagram seam: `IDatagramProtocolServer` and `IDatagramFlow` (ADR-0004), tested as TFTP's server is.

## Acceptance criteria

- [ ] Named tests show:
      - a query answered with its ID and question echoed;
      - an answer over 512 bytes gets TC and only what fits;
      - the same answer fitting under an OPT payload size of 4096 sent whole with an OPT in the response;
      - a response message (QR = 1) ignored;
      - QDCOUNT = 2 gets FORMERR;
      - opcode 2 (STATUS) gets NOTIMP;
      - an unparseable message gets FORMERR when the header can be read, and is dropped otherwise.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
