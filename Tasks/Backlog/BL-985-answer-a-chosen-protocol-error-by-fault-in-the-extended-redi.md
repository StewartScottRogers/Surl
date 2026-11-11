---
id: BL-985
title: Answer a chosen protocol error by fault in the extended Redis server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-985 — Answer a chosen protocol error by fault in the extended Redis server

## Goal

An `error` fault rule naming `redis` or `rediss` (or a listener of one) makes the Redis server answer the matching
command with the chosen text as a RESP error `-<text>`.

## Context

- Second of the three tasks BL-836 was split into (see BL-984).
- Decision: ADR-0124 decisions 4 and 9 and its extended reply table: Redis text sent as a RESP error `-<text>`;
  `command` is the command name and `path` the key.
- Unit tests replay bytes written from the RESP specification (ADR-0083). Consult `ExchangeContext.Faults` per
  command; publish `FaultAppliedEvent` (BL-823's contracts and doubles).
- Stand-in: this is the fault-injection point for the Redis server; the event is how a test observes it.

## Acceptance criteria

- [ ] Named tests: a command matching an `error` rule gets exactly the chosen RESP error, a non-matching one is
      answered, and one `FaultAppliedEvent` is published.
- [ ] With no rule, every existing test of the library passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-985-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-985-lane-6-20261111-060243 and fix it.
