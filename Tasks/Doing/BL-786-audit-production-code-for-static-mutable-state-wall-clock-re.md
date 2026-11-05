---
id: BL-786
title: Audit production code for static mutable state, wall-clock reads and unseeded randomness and file a fix task per area
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-822]
touches: [Tasks/Backlog]
requirement: none
created: 2026-11-05
completed:
---
# BL-786 — Audit production code for static mutable state, wall-clock reads and unseeded randomness and file a fix task per area

## Goal

Every place in production code that would make an embedded Surl non-deterministic, or unsafe to run as several
instances in one process, is found, classified against BL-822's ADR decision 6, and has a fix task on the board.

## Context

- Decision: BL-822's ADR decision 6 (determinism): which randomness may be seeded (non-secret choices) and which
  never (keys, nonces, challenges, tokens), how the seed is given, and that nothing holds static mutable state.
- Scope: every production project in `Surl.slnx` - each `*.UnitLibrary`, `Surl.Console`, `Surl.Api`, `Surl.Mcp`.
  Not test projects. Search at least for: non-`readonly` static fields and properties with setters; `static
  readonly` collections or objects that are mutated; `DateTime.Now`/`UtcNow`, `DateTimeOffset.Now`/`UtcNow`,
  `Environment.TickCount`, `Stopwatch` not fed by `TimeProvider`; `Random.Shared`, `new Random(`,
  `Guid.NewGuid()`, `RandomNumberGenerator` used for a non-secret value; process-wide caches.
- Leads already seen on 2026-11-05 (verify each): `AzureReply.NewRequestId`
  (`Surl.Extended.Protocol.AzureStorage`), `SignalRHubConnections.NewId`, `Amqp0BrokerConnection.GenerateName`,
  `Amqp1ContainerOptions.NewContainerId` - non-secret IDs from `RandomNumberGenerator`, candidates for the seedable
  source; `RandomNtlmServerChallengeSource.Instance`, `RandomSaslNonceSource.Instance`,
  `CryptographicCoapRandomSource.Instance` - stateless secret sources, likely fine as singletons but confirm.
- How to file: one `feature` task per library (or per tight group of libraries in one category folder) with
  `task-board.ps1 new`, `-DependsOn BL-822`, exact `-Touches`, each naming the file, member, finding, the fix the
  ADR's rule calls for and a named test that shows two instances (or two seeds) behave as the rule says. If the
  ADR adds a seed option or a seedable random source, file the task that creates it first and make the fix tasks
  depend on it.
- This task changes no code (docs pipeline); its record is the filed tasks and the Notes below.

## Acceptance criteria

- [ ] This task's Notes list every finding as `file - member - kind (static state, wall clock, seedable random,
      secret random kept) - task ID or "no change, because ..."`.
- [ ] Every finding that needs a change has a filed `Backlog` task as described in Context, and none of them
      duplicates a task already on the board (depend on it instead).

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
