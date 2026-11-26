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
completed:
---
# BL-1741 — Say in the ADR index that ADR-0014 Amendment 1 is proven with pinned upstream curl

## Goal

The ADR index row for ADR-0014 says Amendment 1's pinned upstream curl proof is built (BL-1722).

## Context

`Documentation/Planning/Decisions/README.md`, row 0014, says "its pinned curl proof to come (BL-1722)". BL-1722 added
`Conformance/Surl.Conformance.UnitTests/UpstreamCurlHoldsASubscriptionOnSurlOverMqttTests.cs` and updated the ADR itself,
but left the index alone because another task in progress named that file.

## Acceptance criteria

- [ ] Row 0014 of `Documentation/Planning/Decisions/README.md` says the pinned curl proof is built (BL-1722) and no longer says it is to come.
- [ ] That row names BL-1715 as the task that decided Amendment 1, as the ADR does, instead of BL-1727.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
