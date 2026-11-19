---
id: BL-1394
title: State in --aihelp which silence --idle-timeout counts while a delay fault holds the server
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1394 — State in --aihelp which silence --idle-timeout counts while a delay fault holds the server

## Goal

`surl --aihelp` says whether `--idle-timeout` closes a connection while the server itself is silent because a
`delay` fault holds it, and the statement is true of the code.

## Context

- Surly cookbook bug report 2026-11-19 06:42, finding 2 (a question).
- `surl --directory site --idle-timeout 1 --fault "delay;stage=command;ms=3000" http://127.0.0.1:8080/`, then
  `curl http://127.0.0.1:8080/hello.txt`, gets `200` after about 3.7 s although no byte moved for 3 s.
- ADR-0006 (`Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`) defines idle as "no
  byte moves in either direction". The behaviour is likely intended: the client is waiting on the server, and a
  delay fault exists to make the server slow, so idle-timeout closing it would defeat the fault.
- Find where the idle deadline is applied around fault delays in `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs`
  (the delay paths, e.g. `ApplyAcceptDelayOrFindDropAsync` near line 792) before writing anything.

## Acceptance criteria

- [x] The task's Log states, with file:line, what the code does today: whether a `delay` fault's wait is counted as
      idle at each stage (accept, command, reply).
- [x] The `--idle-timeout` paragraph and the fault paragraph in `AiHelpProse.cs` each state that rule in one sentence.
- [x] ADR-0006 gains an amendment line ("Decided by Claude under Stewart's delegation") saying a server-side fault
      delay is not the peer's idle time (or whatever the code shows, if it differs by stage).
- [x] `dotnet build` clean and the fast tests green (the aihelp tests included).

## Notes

- Docs only. If the code turns out to be inconsistent between stages, file a feature task for that and keep this
  one to stating what is true now.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Code today: a `delay` wait is not idle at either stage it has. Accept: `ServingEngine.cs:807` `PauseIdleClock()` before the wait, `:809` restart. Command: `CommandStageFaults.cs:82` `context.IdleClock.Pause()`, `:89` restart (HTTP via `HttpCommandFaults.FindAndApplyAsync`). `delay` has no reply stage (`FaultPlan.cs:96`). The 3.7 s `200` in the report is therefore intended. Consistent, so no feature task.
- 2026-11-19: Doing -> Done. --aihelp and ADR-0006 state a delay fault stops the idle clock at accept and command
