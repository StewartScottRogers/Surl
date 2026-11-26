---
id: BL-1741
title: Say in the ADR index that ADR-0014 Amendment 1 is proven with pinned upstream curl
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1722]
touches: [Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1741 — Say in the ADR index that ADR-0014 Amendment 1 is proven with pinned upstream curl

## Goal

The ADR index row for ADR-0014 says Amendment 1's pinned upstream curl proof is built (BL-1722).

## Context

`Documentation/Planning/Decisions/README.md`, row 0014, says "its pinned curl proof to come (BL-1722)". BL-1722 added
`Conformance/Surl.Conformance.UnitTests/UpstreamCurlHoldsASubscriptionOnSurlOverMqttTests.cs` and updated the ADR itself,
but left the index alone because another task in progress named that file.

## Acceptance criteria

- [x] Row 0014 of `Documentation/Planning/Decisions/README.md` says the pinned curl proof is built (BL-1722) and no longer says it is to come.
- [x] That row names BL-1715 as the task that decided Amendment 1, as the ADR does, instead of BL-1727.

## Notes

- 2026-11-25: Two phrases in row 0014 changed. Checked against ADR-0014's Amendment 1 and the presence of
  `UpstreamCurlHoldsASubscriptionOnSurlOverMqttTests.cs`.
- Made the edit in the session rather than delegating to `align-and-document`: it is a one-row change with the
  wording fixed by the acceptance criteria.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: all passed. No `.cs` or project file changed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. ADR index row 0014 says Amendment 1's pinned curl proof is built (BL-1722) and names BL-1715 as its deciding task
