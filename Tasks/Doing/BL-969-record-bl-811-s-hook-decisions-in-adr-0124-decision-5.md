---
id: BL-969
title: Record BL-811's hook decisions in ADR-0124 decision 5
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-811, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-969 — Record BL-811's hook decisions in ADR-0124 decision 5

## Goal

ADR-0124 decision 5 states the choices BL-811 made while building hooks, marked "Decided by Claude under
Stewart's delegation", so the ADR says what the code does.

## Context

- BL-811 built `SurlServer.OnEvent`, `SurlServerFaults.AddOverride`, `SurlFaultScope`,
  `SurlServerOptions.HookTimeLimit` (`Application/Surl.Api`) and `FaultPlan.AddOverride` with `FaultOverrideScope`
  and the `FaultOverride` delegate (`Foundation/Surl.Core.UnitLibrary`). Its task file's Notes list the decisions.
  It could not edit the ADR because BL-707 held `Documentation/Planning/Decisions` at the time.
- The decisions to record: an override's answer applies to the point it was asked at (its own scope and `Times`
  ignored; an error's reply shape is the point's scheme's); an answer `surl` refuses, or whose kind cannot act at
  the point's stage, is published as `HookFailedEvent` `Threw` and ignored; an override's hook ID is its rule ID
  `f<n>`, a callback's is `h<n>`; a `SurlFaultScope` with no `Stage` is asked at every stage; overrides count toward
  the 256 rules and are not listed in `FaultPlan.Rules`; a callback that throws on its own `HookFailedEvent` is not
  published again; a callback's token is cancelled when its registration is disposed, and the server disposes every
  callback when it stops.
- `Application/Surl.Api/README.md`, section "Hooks", already states them for users.

## Acceptance criteria

- [ ] ADR-0124 decision 5 has an amendment paragraph stating each decision listed in Context, marked "Decided by
      Claude under Stewart's delegation", naming BL-811.
- [ ] Every statement in the amendment is true of `Application/Surl.Api/ResponseOverrideHook.cs`,
      `EventCallbackHook.cs` and `Foundation/Surl.Core.UnitLibrary/FaultPlan.cs` as they are.

## Notes

- 2026-11-11 (lane 3): The ADR amendment is already written on factory/BL-969-lane-5-20261111-060243 (commit 734c5b67); cherry-pick it. `dotnet build` cannot run here because global.json pins SDK 10.0.401, which is not installed, so the task waits on BL-1035.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 5 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-969-lane-5-20261111-060243; start with git cherry-pick --no-commit factory/BL-969-lane-5-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails; ADR text is ready on factory/BL-969-lane-5-20261111-060243
- 2026-11-12: Backlog -> Doing.
