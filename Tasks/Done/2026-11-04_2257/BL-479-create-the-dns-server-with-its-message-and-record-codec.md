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
completed: 2026-11-04
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

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - a query and a response with each served type round-tripped;
      - RFC 1035 section 4.1.4's compression example (`F.ISI.ARPA`, `FOO.F.ISI.ARPA`, `ARPA`) written and read
        byte for byte;
      - a pointer loop refused;
      - a pointer past the message end refused;
      - a label over 63 bytes and a name over 255 bytes refused;
      - an OPT record read with its payload size.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered (2026-11-04): `DnsMessageWriter.Write` and `DnsMessageReader.Read` over `DnsMessage`,
  `DnsHeader`, `DnsQuestion`, `DnsResourceRecord`, `DnsName`, one RDATA type per served record type,
  `DnsOpaqueData` for the rest, and `DnsOptRecord` for OPT's fields. 77 unit tests; the library
  measures 100% line, 100% branch, worst CRAP 10 (`Measure-CodeQuality.ps1 -SkipTestRun` over a
  coverage run of this test project alone).
- Tests: round trips in `DnsMessageWriterTests.WriteThenRead_*` (query with OPT; A, AAAA, NS, CNAME,
  PTR, SOA, MX, TXT, SRV, CAA, an unknown type, every section); RFC 1035 4.1.4 in
  `DnsMessageWriterTests.Write_Rfc1035CompressionExample_WritesTheRfcsPointers` and
  `DnsMessageReaderTests.Read_Rfc1035CompressionExample_ReadsTheThreeNames`; loops in
  `Read_PointerToItself_*`, `Read_PointerLoopThroughALabel_*`, `Read_PointerLoopBetweenTwoNames_*`;
  `Read_PointerPastTheMessageEnd_IsRefused`; `Read_LabelLengthOver63_IsAReservedLabelType`,
  `DnsNameTests.Constructor_LabelOf64Bytes_Throws`, `Read_NameOf257Bytes_IsNameTooLong`,
  `Read_NamePast255BytesOnlyOnceAPointerIsFollowed_IsNameTooLong`,
  `DnsNameTests.Constructor_NameOf256Bytes_Throws`; OPT in
  `DnsOptRecordTests.FromResourceRecord_OptReadFromTheWire_HasItsPayloadSize`.
- Choice: the RFC's example offsets 20, 40 and 64 are illustrative; no well-formed message puts
  three names 8 and 18 bytes apart, so the tests keep the RFC's three names and its encodings
  (name in full, label plus pointer, pointer alone) with F.ISI.ARPA at offset 12, the pointers then
  naming 12 and 18.
- Choice: loop refusal is structural. A pointer must name an offset strictly before the labels it
  continues (the name's start, or the previous pointer's target), so every jump goes backwards and
  reading always ends; a forward or self pointer is `CompressionPointerNotBackwards`. This is
  ADR-0092 decision 2's "not strictly backwards" rule.
- Choice: compression matches suffixes byte for byte, case included, so a pointer never changes the
  case a name is written in (RFC 4343, ADR-0092's "the question echoed with its own case"). Names
  written uncompressed (SRV targets) are still remembered as pointer targets for later names, which
  RFC 1035 4.1.4 allows; only suffixes written within the first 16384 bytes are remembered.
- Choice: the reader reads pointers in every name it reads field by field, SRV's target included
  (lenient on read, strict on write); a 63-byte-plus label cannot be represented on the wire, so on
  read it is the reserved label types 0x40 and 0x80 (`ReservedLabelType`).
- Choice: the reader does not judge semantics - QR, opcode, QDCOUNT, a second OPT or an OPT not at
  the root - which are BL-480's answers; `DnsOptRecord.FromResourceRecord` does not check the owner.
- No ADR: every choice above sits inside ADR-0092 decision 2.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. DNS message and record codec reads and writes every ADR-0092 type with compression and refuses loops, bad pointers and oversize names
