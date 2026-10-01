---
id: BL-305
title: Document the WebSocket server in the product overview, glossary, README, roadmap and requirements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-304, BL-320]
touches: [Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md, README.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Surl.Protocol.Ws.UnitLibrary/CLAUDE.md]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-305 — Document the WebSocket server in the product overview, glossary, README, roadmap and requirements

## Goal

The repository's documents say, truly, that Phase 4 is built: what `surl ws://` and `surl wss://`
answer and what pinned upstream curl has proven against them.

## Context

- Pattern: BL-214 (Phase 3's documentation task).
- Sources: BL-281's and BL-285's ADRs, the code as built by BL-288, BL-293, BL-301 to BL-303, and
  BL-304's conformance tests.
- What each file gains:
  - `Documentation/Product/Product-Overview.md`: a "Built for Phase 4: WebSocket" section in the shape
    of "Built for Phase 3", the "Also in scope" authentication list naming the WebSocket upgrade's
    challenges, "Layers" and "Project layout" naming `Surl.HttpMessage` as built.
  - `Documentation/Wiki/Glossary.md`: WebSocket terms the code uses (upgrade, frame, message,
    control frame, close code, masking), each with its code column.
  - `README.md`: `ws` and `wss` among the served schemes.
  - `Documentation/Planning/Roadmap.md`: a "Milestone 4 — Phase 4" section with status, what it
    delivers, exit criteria and decisions, as Milestone 3's is written.
  - `Documentation/Product/Requirements.md`: FR-048 (and FR-052's WebSocket part) restated as built,
    ending with what satisfies it.
  - `Surl.Protocol.Ws.UnitLibrary/CLAUDE.md`: what the library holds and references now.
- Every statement true of the code as it is now; anything not built is written as intent.

## Acceptance criteria

- [ ] Each file in Context carries the section or rows it names, and every ADR, type and test it cites
      exists.
- [ ] No document claims a behaviour BL-304's tests do not prove or the code does not have.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Lane 1 could not integrate: push kept being refused. The work is on branch factory/BL-305-lane-1-20260930-162236; start with git cherry-pick --no-commit factory/BL-305-lane-1-20260930-162236 and fix it.
