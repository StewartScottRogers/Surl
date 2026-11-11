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
completed: 2026-11-06
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

- [x] Named tests: a query matching an `error` rule gets exactly the chosen RCODE, a non-matching one is answered,
      and one `FaultAppliedEvent` is published.
- [x] With no rule, every existing test of the library passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered: `DnsQueryFaults` (internal) asks `ExchangeContext.Faults` at `FaultStage.Command` for each standard
  query with one question (`DnsQueryAnswerer.StandardQueryOf`), on UDP, TCP and DoQ always, and on DoH when the
  handler is made with the exchange (new constructor `DnsOverHttpsHandler(zones, ExchangeContext, ...)`; the
  `TimeProvider` constructor asks no plan). Tests: `DnsQueryFaultsTests` (33), e.g.
  `ServeAsync_UdpQueryUnderARule_GetsExactlyTheChosenRcodeAndPublishesOneEvent`,
  `ServeAsync_UdpQueryTheRuleDoesNotName_IsAnsweredFromTheZones`, and one per transport. Library coverage 100%/100%.
- Choices (the ADR fixes reply, command and path only in outline; recorded here as defaults):
  - `command` is the QTYPE mnemonic (`A`, `MX`), or RFC 3597's `TYPE<n>` for a type without one here.
  - `path` is the QNAME with ASCII letters lowered (RFC 4343: names compare without case) and no trailing dot, `.`
    for the root, so `path=web.example.test` matches what a user writes; the plan's path-prefix rule then works on
    the dotted text as given.
  - `reply` is one digit `1`..`5` only (ADR-0124's table); a mnemonic such as `SERVFAIL` does not fit, is noted
    and the query answered as usual - BL-986 checks it in `Surl.Cli`.
  - Only messages the answerer would look up are asked about: no header, a response, another opcode, an unreadable
    message, a bad OPT or QDCOUNT other than 1 keep their own RFC answers, so a fault never masks a FORMERR.
  - The chosen answer is the RCODE alone: ID, opcode and RD echoed, AA = 0, RA = 0, the question, no records, and an
    OPT exactly when the query had one - the same shape as the server's other error answers.
  - A Delay or Drop rule at a query is noted as not applied (as CoAP does): UDP has no connection to abort, and the
    task is the error fault only.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. An error fault rule naming dns answers the matching query with the chosen RCODE 1-5 on UDP, TCP, DoQ and DoH, publishing one FaultAppliedEvent
