---
id: BL-963
title: List a running server's fault rules with what is left of each count through Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-963 — List a running server's fault rules with what is left of each count through Surl.Api

## Goal

A .NET test (and `Surl.Mcp`'s `list_faults`, BL-964) can read a running `SurlServer`'s fault plan: each rule's ID,
its `--fault` item and how many applications are left of its `times`.

## Context

- ADR-0124 decision 8 gives `list_faults` the result `{"faults": [{"fault_id", "item", "remaining"}]}`, and
  decision 4 says the plan's rules are `f1`, `f2` ..., tried in the order added. `Surl.Core`'s `FaultPlan.Rules`
  already returns each `FaultRule` with `Times` as what is left of its count; `SurlServerFaults` (in
  `Application/Surl.Api/SurlServerFaults.cs`) holds the plan privately and offers only `Add`, `Remove` and `Clear`.
- Split out of BL-812: `Surl.Mcp.UnitLibrary` cannot read the plan without this, and `Surl.Api` was in BL-811's
  `touches` while BL-812 ran.
- Suggested shape: `SurlServerFaults.List()` returning `IReadOnlyList<SurlFaultRule>` (a new record with `Id`,
  `Item` - the `--fault` item spelt from the rule, as `SurlFault.ToItem` spells one, with values
  percent-encoded so `surl`'s reader reads it back unchanged - and `Remaining`, `null` for a rule without `times`).
- Stand-in: observable and fault-injectable - a test can see which rules are left and how often they still fire.

## Acceptance criteria

- [x] Named tests in `Surl.Api.UnitTests`: start rules and added rules are listed in plan order with IDs `f1` ...;
      each listed item, read back by `CommandLineParser`, gives the same rule; `Remaining` drops by one each time a
      `times` rule is applied and is `null` for a rule without `times`; a removed rule is not listed.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps 100% line and branch
      coverage.

## Notes

- Shape as suggested: `SurlServerFaults.List()` returns `IReadOnlyList<SurlFaultRule>` (`Id`, `Item`, `Remaining`),
  a snapshot of `FaultPlan.Rules`. Response overrides are not listed: the plan does not list them (no kind until asked).
- Decision (default taken): `Item` leaves out `times`. `FaultPlan` keeps only what is left of a count, not the count
  given, and a spent rule's 0 is not a `times` value `surl` takes (1 to 1000000), so spelling `times=<remaining>`
  would make a spent rule's item unreadable. `Remaining` carries the count, and the item stays the same across
  applications. "Read back gives the same rule" is tested as the same rule without its count
  (`List_EachItemReadBackByCommandLineParser_GivesTheSameRuleWithoutItsCount`). BL-964's `list_faults` can pass
  `Item` and `Remaining` through unchanged.
- The item is built by `SurlFault.WithoutCountFrom(FaultRule)` (internal) then `SurlFault.ToItem`, so values are
  percent-encoded exactly as start and runtime rules are.
- Tests: `SurlServerTests.FaultList.cs` (4: plan order with IDs and items, read back by `CommandLineParser`,
  `Remaining` 2 -> 1 -> 0 and null, removed rule and override not listed). Surl.Api.UnitTests 221 green, every fast
  test project green; `Measure-CodeQuality.ps1 -Library Surl.Api`: 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. SurlServer.Faults.List() reads each fault rule's ID, its --fault item and what is left of its times
