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
completed: 2026-09-28
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

- [x] The `> **TODO**` at the top of `Requirements.md` is gone.
- [x] "Functional" has one row per Phase 1 behaviour the sources state. At minimum:
      serving HTTP/1.x GET and HEAD from the served directory to upstream curl, listen
      URLs, the Phase 1 options, and the exit codes. Each row names the upstream curl
      request or option it answers and the pinned 8.21.0 build, and has a MoSCoW
      priority and status `Draft`.
- [x] "Non-functional" has one row per measurable quality the sources state. At
      minimum: fast tests open no socket, the coverage, complexity and CRAP gates from
      the root `CLAUDE.md`, and native AOT publish with no runtime. Each row has its
      number.
- [x] The placeholder rows `FR-001 | > **TODO**` and `NFR-001 | > **TODO**` are replaced,
      not left beside real rows.
- [x] "Non-functional" includes a row for each hardening limit in the ADR recorded by
      BL-024, with its number.
- [x] "Out of scope" records that a managed NuGet API is not a deliverable (Stewart,
      2026-09-28), and "Open questions" lists no question Stewart has answered.

## Notes

- Written in the session rather than handed to `align-and-document`: the task is one
  document derived from sources already read in full, and it touches no `.cs` file.
- Functional table gained two columns, "Answers (upstream curl)" and "Measured against",
  so each row names its curl request or option and the pinned build separately. Every row
  names curl 8.21.0 (Git for Windows); rows ADR-0005, ADR-0006 or ADR-0007 already
  measured cite that ADR, the rest are measured by their implementing task and stay
  `Draft` until then.
- Priorities: the rows Phase 1's "Proves" column needs (HTTP GET/HEAD, listen URLs,
  options, exit codes, exposure defaults, limit refusals) are Must; the servers that ride
  alongside (DICT, Gopher, TELNET, TFTP, MQTT), authentication, cookies, HTTPS and the
  verbose log are Should.
- Hardening: every ADR-0006 section 1 limit is its own NFR (NFR-008 to NFR-016), plus the
  refusal write deadline, TLS minimums, peer-visible text and log escaping (NFR-017 to
  NFR-020).
- Open questions keep only BL-002's TLS contract, which is Claude's to decide; none of
  Stewart's answered questions is listed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Requirements.md holds Phase 1's 21 functional and 20 non-functional requirements, citable by ID
