---
id: BL-863
title: Trace the STOMP matrix's BL-532 rows to their named tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-532]
touches: [Documentation/Conformance/Extended/Stomp.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-863 — Trace the STOMP matrix's BL-532 rows to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Stomp.md` that names BL-532 names the unit test that shows it instead.

## Context

- ADR-0118's Consequences: the requirement matrix traces each server-side requirement to BL-531 or BL-532, and each task
  replaces its ID with the named test.
- BL-532 built the routing but could not edit the matrix: `Documentation/Conformance` was in BL-396's `touches` while it
  ran. The tests are in `Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests`, mostly
  `StompServerConnectionTests.Routing.cs` and `StompServerConnectionTests.Refusals.cs`, plus `StompOutboxTests`.

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Stomp.md` names no BL-532; each such row names an existing test method by
      `Class.Method`.
- [x] Each named test exists (`grep` finds the method in `Surl.Extended.Protocol.Stomp.UnitTests`).

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Stomp.md names existing tests for all 36 BL-532 rows and the two partial BL-532 mentions; every named test checked to exist
