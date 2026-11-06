---
id: BL-836
title: Answer a chosen protocol error by fault in the extended DNS and Redis servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-05
completed:
---
# BL-836 — Answer a chosen protocol error by fault in the extended DNS and Redis servers

## Goal

An `error` fault rule naming `dns`, `redis` or `rediss` (or a listener of one) makes that server answer the
matching query or command with the chosen error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4
  and 9 and its extended reply table: DNS an RCODE 1 to 5 (`command` the QTYPE, `path` the QNAME); Redis text sent
  as a RESP error `-<text>` (`command` the command name, `path` the key).
- Each server is judged by its own specification (ADR-0083); unit tests replay bytes written from it. Consult
  `ExchangeContext.Faults` per query or command; publish `FaultAppliedEvent` (BL-823's contracts and doubles).
- `Surl.Cli`: add these schemes' reply shapes to BL-785's `--fault` reply check.

## Acceptance criteria

- [ ] Named tests per server: a query or command matching an `error` rule gets exactly the chosen error, a
      non-matching one is served, and one `FaultAppliedEvent` is published.
- [ ] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme.
- [ ] With no rule, every existing test of the touched libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9); depends on BL-785 and touches `Surl.Cli` for the reply shapes.
