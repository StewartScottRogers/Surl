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
completed: 2026-11-04
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

- [x] Named tests show:
      - a query answered with its ID and question echoed;
      - an answer over 512 bytes gets TC and only what fits;
      - the same answer fitting under an OPT payload size of 4096 sent whole with an OPT in the response;
      - a response message (QR = 1) ignored;
      - QDCOUNT = 2 gets FORMERR;
      - opcode 2 (STATUS) gets NOTIMP;
      - an unparseable message gets FORMERR when the header can be read, and is dropped otherwise.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Plan: three types. `DnsQueryAnswerer` turns query bytes into a `DnsResponse` (or `null` to drop)
  and has no transport in it, so BL-482's TCP server reuses it; `DnsUdpResponseWriter` bounds and
  cuts it for UDP; `DnsUdpProtocolServer` is the `IDatagramProtocolServer` (scheme `dns`).
- Tests: `DnsUdpProtocolServerTests` (each acceptance case, through `InMemoryDatagramFlow`),
  `DnsQueryAnswererTests` (every row of ADR-0092 decision 2's request-error table, BADVERS,
  DO copied, AXFR/IXFR) and `DnsUdpResponseWriterTests` (the limit clamp, each cut step). Query
  bytes are written by hand in `DnsTestQueries`. 241 tests in the project; library 100% line and
  branch coverage (cobertura from the MSTest collector).
- Choices within ADR-0092, recorded here because the ADR leaves the order open:
  - The opcode is checked before the body is read, so a non-QUERY opcode gets `NOTIMP` even
    when the body cannot be read: the opcode is a header fact, and another opcode may define its
    body otherwise.
  - The question is echoed only when QDCOUNT is 1 and that question was read (QDCOUNT is taken
    from the raw header, since a fault may stop reading before the second question).
  - A response carries an OPT whenever the query was read whole with exactly one root-owned OPT,
    `NOTIMP` and the QDCOUNT `FORMERR` included (RFC 6891 section 7); an unreadable message,
    two OPTs or a non-root OPT get none, since there is no OPT to trust.
  - AXFR and IXFR are answered `REFUSED`, ADR-0092 decision 4's default with zone transfer off;
    `--allow-zone-transfer` and UDP's `NOTIMP`/SOA-only answers under it are BL-482's and BL-485's.
  - The server answers the flow's first datagram and then every later datagram on the flow until
    the engine cancels the exchange (idle timeout or `--max-time`, decision 6); the cancellation
    escapes, which the engine notes as the exchange's end, as for every datagram server.
  - A dropped datagram is noted in the exchange log with one fixed text.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. The DNS server answers queries over UDP: ID and question echoed, TC at 512 or the OPT payload size (max 1232), FORMERR, NOTIMP, BADVERS, and drops for QR=1 or no header
