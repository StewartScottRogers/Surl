---
id: BL-542
title: Document the extended protocols in the product overview, README, roadmap, requirements and glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-422, BL-428, BL-434, BL-440, BL-446, BL-451, BL-456, BL-462, BL-469, BL-477, BL-486, BL-502, BL-508, BL-515, BL-523, BL-529, BL-534, BL-541, BL-493]
touches: [Documentation/Product/Product-Overview.md, README.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-542 — Document the extended protocols in the product overview, README, roadmap, requirements and glossary

## Goal

Surl's top-level documents state what is now true: Surl answers the Curl port's extended protocols, judged by
their specifications. Each document says which servers exist, their schemes, where they live, how they are
switched off, and how they are tested.

## Context

- Decisions: BL-543's ADR (oracle exception, switch, layout, matrices, roadmap placement), BL-400's ADR
  (`Protocols/DataStore/`), BL-401's and BL-402's ADRs (shared libraries), BL-403's ADR, and
  each server's ADR.
- `Documentation/Product/Product-Overview.md` today:
  - "Non-goals" says "Protocols upstream curl does not request", which the extended servers amend;
  - "Scope" lists "29 schemes, 15 protocol servers";
  - "Architecture", "Layers" and "Project layout" list the libraries;
  - "Phasing" has rows 0 to 7.
  Phase 6's own document task, BL-398, touches the same files. Whichever runs second merges with the other's text,
  never over it.
- Root `README.md` has a protocols list. The port's `Documentation/Protocols.md` "Extended protocols" table is a
  model for the shape, not for the text.
- Write only what is true when this task runs. A server whose integration task found a port defect says what
  works, not what was planned.

## Acceptance criteria

- [x] `Documentation/Product/Product-Overview.md` has an "Extended protocols" section. Its table has one row per
      extended server: library, schemes, specification, and requirement matrix. The section states the oracle
      exception and the switch, and that Plugin is skipped and why. "Non-goals", "Scope", "Architecture",
      "Layers", "Project layout" and "Phasing" are amended so that none contradicts it.
- [x] Root `README.md` has an "Extended protocols" section listing every extended scheme, the switch that turns
      them off, and a pointer to the overview section.
- [x] `Documentation/Planning/Roadmap.md` has the milestone BL-543's ADR named, listing the tasks that
      delivered it.
- [x] `Documentation/Product/Requirements.md` has one functional requirement row per extended server, in FR-048's
      shape, each citing its ADR, its specification and its integration test class, and saying it is judged by
      the specification and not by upstream curl.
- [x] `Documentation/Wiki/Glossary.md` defines each term the extended ADRs introduced that is not yet defined.

## Notes

- 2026-11-12: Wrote the overview section "Extended protocols" (table of 19 servers, oracle exception, switch, Plugin skipped), the README section, the Roadmap track "Extended servers" (no ADR-0083 milestone number; decision 10 says parallel track), FR-066 to FR-084 (one per server, citing ADR, spec and integration test class) and four glossary terms. Roadmap statements on the port-client run count come from the existing Milestone 7 text (4 of 19 recorded in Extended.md); not re-measured here. Docs only, no code touched.

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Top-level docs state the 19 extended servers, oracle exception, SURL_EXTENSIONS switch, FR-066 to FR-084 and glossary terms
