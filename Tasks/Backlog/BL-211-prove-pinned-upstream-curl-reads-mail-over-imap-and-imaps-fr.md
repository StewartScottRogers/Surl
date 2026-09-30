---
id: BL-211
title: Prove pinned upstream curl reads mail over imap and imaps from surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-208, BL-210]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-044
created: 2026-09-29
completed:
---
# BL-211 — Prove pinned upstream curl reads mail over imap and imaps from surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds read, search, append and manage mail on a live `surl` over `imap`
and `imaps` as BL-187's and BL-185's ADRs expect, including mail another curl delivered over
SMTP to the same `surl`.

## Context

- The cases: BL-187's ADR's list, covering at least the mailbox list, a fetch by `UID`, by
  `MAILINDEX`, a `SECTION` and a `PARTIAL`, a search, a `-T` append, `-X` commands, `LOGIN` and
  each `AUTHENTICATE` mechanism BL-185's ADR offers, `--ssl-reqd` (`STARTTLS`) and `imaps://`
  with `--self-signed` (curl `-k`), a plain-text `LOGIN` refused without TLS and accepted with
  `--allow-plaintext-auth`; and a round trip: curl sends over `smtp` (BL-210's case), then curl
  fetches the same message over `imap` from the same `surl`, byte for byte.
- Harness as BL-210; Linux and macOS legs on CI.
- Any disagreement with the pinned build is fixed through a new task filed by `task-planner`,
  never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [ ] Integration tests exist for every case in Context and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

## Log

- 2026-09-29: Created.
