---
id: BL-478
title: Decide how the DNS server answers dns and its encrypted transports
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-402]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/DnsQuery.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-478 — Decide how the DNS server answers dns and its encrypted transports

## Goal

An ADR decides how `surl dns://` answers DNS queries as an authoritative server. It covers where the zone comes
from, the record types, the answer for every outcome, UDP truncation and TCP, how DNS over TLS, HTTPS and QUIC
listeners are asked for, and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - RFC 1035 (messages, name compression, master files) and RFC 3596 (AAAA);
  - RFC 2782 (SRV), RFC 8659 (CAA), RFC 3597 (unknown types) and RFC 6891 (EDNS(0));
  - RFC 7766 (DNS over TCP) and RFC 4501 (the `dns:` URI);
  - RFC 7858 and RFC 8310 (DNS over TLS), RFC 8484 (DNS over HTTPS) and RFC 9250 (DNS over QUIC);
  - RFC 2181 (clarifications) and RFC 4592 (wildcards), if served.
- Which queries a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.DnsQuery.UnitLibrary` and its matrices `DnsQuery.md` and `DnsQuery-Encrypted.md` (its
  `--dns-transport` choices: udp, tcp, tls, https and quic). The port's `DnsReferenceServer` is copyable code.
- The project name is Stewart's: `Surl.Extended.Protocol.DnsQuery`, in `Protocols/Lookup/`.
- The seams: `IDatagramProtocolServer` for UDP and `IConnectionProtocolServer` for TCP (ADR-0004); HTTP/1.1 from
  BL-407; HTTP/2 from BL-409; QUIC from `Surl.Quic` as BL-402's ADR decides.

Decide and record:

1. The zone source. Planning assumption: RFC 1035 section 5 master files in a directory named by an option, with
   no zone built in.
2. The record types served and the answer for each outcome: an answer, NODATA, NXDOMAIN with the SOA in
   authority, in-zone CNAME chasing, REFUSED outside every zone, `RA = 0` (no recursion), FORMERR, NOTIMP for
   another opcode, and wildcards if served.
3. UDP: the 512-byte limit, the EDNS(0) payload size, and TC.
4. TCP: persistent connections, pipelining and the idle timeout (RFC 7766 section 6), and whether AXFR is served
   or refused.
5. How DoT (port 853, ALPN `dot`), DoH (the path, GET and POST, over HTTP/1.1 and HTTP/2) and DoQ (UDP port 853,
   ALPN `doq`) listeners are asked for on the command line, since Stewart's scheme list has `dns` only.
6. Each ADR-0006 limit, and response-rate limiting if any.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 6, each citing its RFC section, and is listed
      in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/DnsQuery.md` exists in the matrix format with the server-binding
      requirements of the RFCs above. Each "covered by" names BL-479, BL-480, BL-481,
      BL-482, BL-483, BL-484 or BL-485, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
