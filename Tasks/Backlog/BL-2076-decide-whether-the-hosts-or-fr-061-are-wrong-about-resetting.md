---
id: BL-2076
title: Decide whether the hosts or FR-061 are wrong about resetting one store at a time
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md, Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-28
completed:
---
# BL-2076 — Decide whether the hosts or FR-061 are wrong about resetting one store at a time

## Goal

An ADR decides whether `Surl.Api`, `Surl.Mcp` and the control endpoint gain a one-store reset, snapshot and restore or FR-061 stops claiming it, and the follow-up work is filed.

## Context

Found while working BL-2034 (2026-11-28). FR-061 says stores are reset, snapshotted and restored "one at a time or all together" through all three hosts. `SurlServer`, `reset_server` and the control endpoint's `ComposedControlTarget` act only on all stores together; `ComposedStores.ResetAsync(StoreKind, ...)` is called by no host (search of `Application/` outside tests). CLAUDE.md says nothing is left out because it is hard; ADR-0124 decision 3 is the design.

## Acceptance criteria

- [ ] An ADR marked "Decided by Claude under Stewart's delegation" states the choice and why.
- [ ] FR-061 is true of the code as it is, with any unbuilt part written as intent.
- [ ] If the hosts are to gain the members, tasks for them are on the board.

## Notes

## Log

- 2026-11-28: Created.
