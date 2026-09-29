---
id: BL-021
title: Author the Phase 1 requirements in Requirements.md
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-003]
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-021 — Author the Phase 1 requirements in Requirements.md

## Goal

`Documentation/Product/Requirements.md` holds the first functional and non-functional
requirements, derived from the Phase 1 row of the product overview and the ADRs decided
so far. Its "structure only" TODO is gone, so later tasks can cite requirement IDs.

## Context

- `Documentation/Product/Requirements.md` says Phase 1 planning authors the first
  requirements. Every task filed so far carries `requirement: none` for that reason.
- Sources, and only these: `Documentation/Product/Product-Overview.md` (the Phase 1 row
  of "Phasing", "Success criteria", "Constraints") and the ADRs in
  `Documentation/Planning/Decisions/`, in particular the listener-seam, exit-code and
  command-line ADRs recorded by BL-000, BL-001 and BL-003, and the hardening ADR
  recorded by BL-024 (Stewart, 2026-09-28: internet-facing; executable only, no NuGet
  API). Do not invent a requirement
  that no source states.
- The file's own rules: a functional requirement names the upstream curl option or
  request it answers and the pinned build its behaviour was measured against (the Git
  for Windows 8.21.0 build in `UpstreamCurlBuilds.json`), never the Curl port.
  A non-functional requirement has a number. IDs are never reused.

## Acceptance criteria

- [ ] The `> **TODO**` at the top of `Requirements.md` is gone.
- [ ] "Functional" has one row per Phase 1 behaviour the sources state. At minimum:
      serving HTTP/1.x GET and HEAD from the served directory to upstream curl, listen
      URLs, the Phase 1 options, and the exit codes. Each row names the upstream curl
      request or option it answers and the pinned 8.21.0 build, and has a MoSCoW
      priority and status `Draft`.
- [ ] "Non-functional" has one row per measurable quality the sources state. At
      minimum: fast tests open no socket, the coverage, complexity and CRAP gates from
      the root `CLAUDE.md`, and native AOT publish with no runtime. Each row has its
      number.
- [ ] The placeholder rows `FR-001 | > **TODO**` and `NFR-001 | > **TODO**` are replaced,
      not left beside real rows.
- [ ] "Non-functional" includes a row for each hardening limit in the ADR recorded by
      BL-024, with its number.
- [ ] "Out of scope" records that a managed NuGet API is not a deliverable (Stewart,
      2026-09-28), and "Open questions" lists no question Stewart has answered.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
