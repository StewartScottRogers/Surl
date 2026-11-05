---
id: BL-479
title: Create the DNS server with its message and record codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-478, BL-404]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-479 — Create the DNS server with its message and record codec

## Goal

`Surl.Extended.Protocol.DnsQuery.UnitLibrary` exists. It reads DNS messages and writes them with name
compression. It refuses compression loops and out-of-bounds pointers, and encodes every record type
BL-478's ADR serves, including unknown types and the EDNS(0) OPT record.

## Context

- Decision: BL-478's ADR, decision 2. Rules: BL-543's ADR.
- New projects in `Protocols/Lookup/`, listed in `Surl.slnx` under `/Protocols/Lookup/`. Namespace
  `Surl.Extended.Protocol.DnsQuery`. References: `Surl.Protocol.Abstractions` only.
- Specifications:
  - RFC 1035 section 4 (header, question, RR, section 4.1.4 compression) and section 3.3 (RDATA of A, NS, CNAME,
    SOA, PTR, MX, TXT);
  - RFC 3596 (AAAA), RFC 2782 (SRV), RFC 8659 (CAA), RFC 3597 (unknown types, and the `\#` presentation form);
  - RFC 6891 section 6 (OPT: the payload size in CLASS, and the extended RCODE and DO in TTL).

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - a query and a response with each served type round-tripped;
      - RFC 1035 section 4.1.4's compression example (`F.ISI.ARPA`, `FOO.F.ISI.ARPA`, `ARPA`) written and read
        byte for byte;
      - a pointer loop refused;
      - a pointer past the message end refused;
      - a label over 63 bytes and a name over 255 bytes refused;
      - an OPT record read with its payload size.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
