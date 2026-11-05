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
completed: 2026-11-04
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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 6, each citing its RFC section, and is listed
      in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/DnsQuery.md` exists in the matrix format with the server-binding
      requirements of the RFCs above. Each "covered by" names BL-479, BL-480, BL-481,
      BL-482, BL-483, BL-484 or BL-485, or "n/a: <reason>".

## Notes

- Delivered as ADR-0092 (`Documentation/Planning/Decisions/ADR-0092-how-the-dns-server-answers-dns-and-its-encrypted-transports.md`),
  written in the session rather than through `align-and-document`: the task is one decision record and one matrix,
  and the decisions needed the context already read. No `.cs` or project file changed; `dotnet build` is clean and
  the fast tests are green (44 assemblies).
- Decision 1 departs from the planning assumption: zones are `<path>/.surl/dns/*.zone` under `--directory`, not a
  folder named by a new option, following ADR-0072's LDAP seed, ADR-0007 and ADR-0031 decision 8. BL-485's
  Context says "the zone option"; it reads ADR-0092 decision 1, which has no option.
- Decision 5 adds `--dns-transport <list>` (default `udp,tcp`) and decision 4 adds `--allow-zone-transfer`
  (off); both are BL-485's to build. BL-482's AXFR acceptance follows decision 4 (refused unless the flag).
- DoT's ALPN `dot` is registered by RFC 9103 section 7.1, not RFC 7858 or RFC 8310 as the task's Context said;
  the ADR cites RFC 9103.
- Defaults taken: UDP ceiling 1232 bytes (fragmentation avoidance); CNAME chain cap 16 with `SERVFAIL`; zone
  bounds as LDAP's; no response-rate limiting; no DoH over HTTP/3 (ADR-0086 gives `dns` no HTTP/3 route).
- `Documentation/Product/Requirements.md` gets no row here: BL-542 adds one row per extended server.
- ADR-0006's header gains an "Amended by" line for ADR-0092 (inside this task's `Documentation/Planning/Decisions`).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0092 decides how surl dns:// answers over UDP, TCP, DoT, DoH and DoQ; DnsQuery.md traces each requirement to BL-479..BL-485
