---
id: BL-398
title: Document Phase 6 in the product overview, roadmap, requirements, glossary and README
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-360, BL-371, BL-385, BL-394, BL-395, BL-396, BL-397]
touches: [Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-398 — Document Phase 6 in the product overview, roadmap, requirements, glossary and README

## Goal

The product overview, roadmap, requirements, glossary and README describe Phase 6 as it was built and
proven: HTTP/2, HTTP/3 over the hand-built QUIC, the upstream test-case pass rate, and native publishing
on each runtime identifier. Every statement in them is true of the code.

## Context

- The work: BL-358 to BL-361 (native publish), BL-364 to BL-371 (HTTP/2), BL-372 to BL-385 (HTTP/3 and
  QUIC), and BL-386 to BL-397 (upstream test cases). Read each one's ADR and Notes.
- Shape to copy: BL-319 ("Document the LDAP, SMB and RTSP servers in the product overview, glossary,
  README, roadmap and requirements"), and the overview's "Built for Phase 5: LDAP, SMB and RTSP"
  section.
- The `align-and-document` agent owns name and document alignment (root `CLAUDE.md`). This task
  changes no behaviour.

## Acceptance criteria

- [ ] `Documentation/Product/Product-Overview.md` has a "Built for Phase 6" section stating what surl
      answers over HTTP/2 and HTTP/3, the libraries added and their places in "Layers" and "Project
      layout", the pass rate per platform with a link to the pass-rate file, and which runtime
      identifiers are proven and how. Its "Last updated" is the day of the edit.
- [ ] `Documentation/Planning/Roadmap.md` has a "Milestone 6 — Phase 6" entry with status, delivers,
      exit criteria and decisions (the ADR numbers), in the shape of the Milestone 5 entry.
- [ ] `Documentation/Product/Requirements.md`'s Phase 6 rows end with what satisfies them, as FR-048 to
      FR-052 do, and its header paragraph names them.
- [ ] `README.md`'s status paragraph (around "native publishing on every platform") and
      `Documentation/Wiki/Glossary.md` match.

## Notes

## Log

- 2026-11-04: Created.
