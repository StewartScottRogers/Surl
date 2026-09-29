---
id: BL-023
title: Record Stewart's answers to the product overview's open questions
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-023 — Record Stewart's answers to the product overview's open questions

## Goal

`Documentation/Product/Product-Overview.md` states Stewart's 2026-09-28 answers to its
four open questions as settled scope, and no longer lists them as open.

## Context

Stewart answered on 2026-09-28, in the `/task-plan` session that filed BL-000 to BL-043:

1. **Pin a build with SMB, HTTP/2 and HTTP/3:** yes, and use the latest. BL-022
   records the approval. Stewart then chose to take curl.se's current Windows build
   (8.22.0_2) as a supplementary build, used only for SMB, HTTP/2 and HTTP/3, and to
   keep 8.21.0 as the reference release. BL-026 downloads and pins it. The overview's
   "reference release: 8.21.0" statements stay true.
2. **Linux and macOS builds:** download them. The approval covers downloading upstream
   8.21.0 builds for Linux and macOS. BL-027 decides which builds and how CI gets them,
   and BL-028 pins them.
3. **Local testing only, or internet-facing:** internet-facing. Surl must be hardened
   for internet-facing use. BL-024 decides what that means in an ADR.
4. **Managed NuGet API or executable only:** the executable. The `surl` executable is
   the only product. Its libraries are implementation, not a published API.

## Acceptance criteria

- [ ] "Open questions": each of rows 1 to 4 is removed or marked answered, with the
      answer, "Stewart, 2026-09-28" and the task that carries it out (BL-026, BL-027 and
      BL-028, BL-024, none).
- [ ] "Non-goals": the `> **TODO**` about open question 3 is gone. A non-goal states
      that a managed NuGet API is not a deliverable (Stewart, 2026-09-28).
- [ ] "Users" or "Scope" states that Surl is built to be exposed to the internet, and
      points to BL-024's ADR for what that requires. Until that ADR exists, say it is
      decided by BL-024.
- [ ] Nothing else in the file changes meaning.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
