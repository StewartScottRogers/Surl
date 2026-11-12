---
id: BL-593
title: Document Phase 7 in the product overview, roadmap, requirements, glossary and README
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-592]
touches: [Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-593 — Document Phase 7 in the product overview, roadmap, requirements, glossary and README

## Goal

The product overview, roadmap, requirements, glossary and README state what Phase 7 built and found, as it
is now: the measured port commit, where the findings and the handoff are, and what is still open.

## Context

- Phase 7's decision is BL-576's ADR. Its harness comes from BL-577, BL-578 and BL-579, its measurements from
  BL-580 to BL-591, and its findings and handoff from BL-592 (`Documentation/Conformance/CurlPort/`).
- `Documentation/Planning/Roadmap.md` has Phase 7 only in "Later". Add a "Milestone 7 — Phase 7" section in
  the shape of the Milestone 5 section (Status, Delivers, Exit criteria, Decisions), and trim "Later" to
  what is still later.
- `Documentation/Product/Product-Overview.md`: the Phasing row 7 and success criterion 5 stay. Add a
  "Built for Phase 7" scope section, in the shape of "Built for Phase 5", and update "Last updated".
- `Documentation/Product/Requirements.md`: BL-576 added a Phase 7 requirement row marked not built. Mark what
  is now built and name what is not, in the style of FR-050's "Not yet recorded".
- `Documentation/Wiki/Glossary.md`: BL-576 defined the Phase 7 terms. Add any term that BL-578 to BL-592
  introduced and that the glossary lacks.
- `README.md`: one sentence and a link saying Phase 7 measures the Curl port, and where its findings are.
- Every statement must be true of the repository at the time (root `CLAUDE.md`, "Say what it does").

## Acceptance criteria

- [x] `Roadmap.md` has a "Milestone 7 — Phase 7" section with Status, Delivers, Exit criteria and Decisions,
      and "Later" no longer describes Phase 7 as future work.
- [x] `Product-Overview.md` has a "Built for Phase 7" section naming the measured port commit and linking
      `Documentation/Conformance/CurlPort/README.md`.
- [x] The Phase 7 requirement row names the tasks and tests that satisfy it and what is not yet measured
      (for example, platforms with no port pin).
- [x] `Glossary.md` defines every Phase 7 type or term used in those documents.
- [x] `README.md` links the Phase 7 findings.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Phase 7 documented in the overview, roadmap, requirements, glossary and README
