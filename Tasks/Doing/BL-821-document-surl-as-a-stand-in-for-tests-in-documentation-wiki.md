---
id: BL-821
title: Document Surl as a stand-in for tests in Documentation/Wiki/Surl-As-A-Stand-In.md and the README
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-786, BL-811, BL-812, BL-813, BL-817, BL-818, BL-819, BL-820]
touches: [Documentation/Wiki/Surl-As-A-Stand-In.md, README.md, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-821 — Document Surl as a stand-in for tests in Documentation/Wiki/Surl-As-A-Stand-In.md and the README

## Goal

A reader who wants to use Surl in place of a real mail server, bucket, broker or file site in their own
integration tests finds one page that tells them how - from .NET, from an AI agent, and from any other language -
and a README section that sends them there, every statement true of the code as built.

## Context

- Decision: BL-822's ADR (Surl is a stand-in for tests) and root `CLAUDE.md`'s section of the same name.
- New page `Documentation/Wiki/Surl-As-A-Stand-In.md`, sections: what a stand-in is and the principles in one
  paragraph each; embedding with `Surl.Api` (port 0, in memory, several instances); composing protocols over the
  shared stores (ADR-0100); observing (exchange record with listen URL, queries and waits - BL-806, BL-807);
  controlling (seed, reset, snapshot, restore - BL-810); faults (`--fault` grammar, `Surl.Api`, `Surl.Mcp`, the
  control endpoint - BL-785, BL-808, BL-812, BL-814); hooks and their bounds (BL-811); custom protocol servers
  (BL-813); determinism (`TimeProvider`, the seed, what is never seeded - BL-786's outcome); the control endpoint
  for non-.NET suites with a curl example (BL-816); `Surl.Mcp` for agents (BL-809, BL-812); recipes - one
  subsection per recipe test class from BL-817 to BL-820, each linking the test file by path and naming its tests.
- `README.md`: a short "Use Surl as a stand-in in your tests" section linking the page. The README is also shown
  on the package pages; keep it consistent with `Application/Surl.Api/README.md`.
- `Documentation/Wiki/Glossary.md`: check the terms BL-822 added still match the code; add any the page uses.
  `Documentation/Product/Product-Overview.md`: one paragraph on the stand-in role beside `Surl.Api` and `Surl.Mcp`.
- Related documentation tasks: BL-649 (Surl.Api and Surl.Mcp in the README and overview) and BL-674 (shared
  stores and the configuration command line) cover their own subjects; link to their sections rather than repeat
  them. Where one of them is still in Backlog when this runs, write this page's links to where their text will
  land and say so in Notes.
- Every example in the page is one a recipe test runs, or is copied from one, so the page cannot drift silently.

## Acceptance criteria

- [ ] `Documentation/Wiki/Surl-As-A-Stand-In.md` exists with every section named in Context; every type, member,
      option, tool and path it names exists in the repository (checked by search), and each recipe link points to an
      existing test class.
- [ ] `README.md` has the stand-in section linking the page; `Glossary.md` and `Product-Overview.md` are updated
      as Context says.
- [ ] No statement in the page describes behaviour a task has not built (or it is written as intent, naming the
      task).

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-12: Backlog -> Doing.
