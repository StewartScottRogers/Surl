---
id: BL-984
title: Answer a chosen protocol error by fault in the extended DNS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-984 — Answer a chosen protocol error by fault in the extended DNS server

## Goal

An `error` fault rule naming `dns` (or a listener of one) makes the DNS server answer the matching query with the
chosen RCODE.

## Context

- First of the three tasks BL-836 ("Answer a chosen protocol error by fault in the extended DNS and Redis
  servers") was split into on 2026-11-06 for the 60-minute size limit: BL-984 DNS, BL-985 Redis, then BL-986 (the
  `--fault` reply check in `Surl.Cli`).
- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4
  and 9 and its extended reply table: DNS an RCODE 1 to 5; `command` is the QTYPE and `path` the QNAME.
- The server is judged by its own specification (ADR-0083); unit tests replay bytes written from RFC 1035. Consult
  `ExchangeContext.Faults` per query, on every transport the server answers; publish `FaultAppliedEvent`
  (BL-823's contracts and doubles).
- Stand-in: this is the fault-injection point for the DNS server; the event is how a test observes it.

## Acceptance criteria

- [ ] Named tests: a query matching an `error` rule gets exactly the chosen RCODE, a non-matching one is answered,
      and one `FaultAppliedEvent` is published.
- [ ] With no rule, every existing test of the library passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
