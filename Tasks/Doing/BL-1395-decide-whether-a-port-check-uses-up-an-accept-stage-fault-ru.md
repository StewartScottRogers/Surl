---
id: BL-1395
title: Decide whether a port check uses up an accept-stage fault rule's times count, and align the engine and --aihelp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-19
completed:
---
# BL-1395 — Decide whether a port check uses up an accept-stage fault rule's times count, and align the engine and --aihelp

## Goal

A test that waits for surl's port and then expects an accept-stage fault on its first real request knows, from
`--aihelp`, whether the readiness probe spent the fault, and the engine does what the help says.

## Context

- Surly cookbook bug report 2026-11-19 06:42, finding 3. A fault rule at stage `accept` with `times=N` is used up by
  any TCP connection that opens and closes without a request, such as a readiness probe or a load-balancer health
  check.
- The accept stage acts before any byte, the TLS handshake included
  (`Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:764`, ADR-0124; `ApplyAcceptDelayOrFindDropAsync` at `:792`;
  counting in `Foundation/Surl.Core.UnitLibrary/FaultPlan.cs`). So a `drop` at accept cannot wait for a byte without
  changing what the stage means.
- Stand-in rule (CLAUDE.md): a test must get the same answer every run. Port checks are how tests wait for a server,
  so a fault spent by the check is a determinism trap.

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions` ("Decided by Claude under Stewart's delegation") decides between
      (a) keeping today's count and documenting that port checks count, and (b) counting an accept-stage rule only
      when the connection then sends a byte (or completes a TLS handshake), with what that means for `drop` and
      `delay` at accept.
- [ ] The engine follows the ADR; named fast tests in `Surl.Core.UnitTests` pin a connect-and-close followed by a
      real request against an accept-stage `times=1` rule.
- [ ] The fault paragraph in `AiHelpProse.cs` states the rule in one sentence.
- [ ] `dotnet build` clean, fast tests green, `Surl.Core` stays 100/100.

## Notes

- Option (a) is simplest; option (b) suits stand-in use better. A third way worth weighing is an explicit rule
  field, but configuration only grows and defaults keep today's behaviour, so a new field would leave the default
  as (a).

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
