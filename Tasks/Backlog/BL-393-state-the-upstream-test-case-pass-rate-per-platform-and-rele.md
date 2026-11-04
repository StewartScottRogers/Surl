---
id: BL-393
title: State the upstream test-case pass rate per platform and release and keep it from falling
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance, .github/workflows/release.yml]
requirement: none
created: 2026-11-04
completed:
---
# BL-393 — State the upstream test-case pass rate per platform and release and keep it from falling

## Goal

The pass rate of upstream curl's test cases against surl is stated per platform and per release in the
file that BL-386's ADR names, and a test fails when a case recorded as passing stops passing. This is
success criterion 2's "stated per release and only rises".

## Context

- Decision: BL-386's ADR, decision 5 (the file, its format, how a release states it, and the test
  that keeps the pass rate from falling).
- BL-392 writes per-case results. This task turns them into the stated rate and the guard.
- No release has been tagged. Tags are Stewart's to make (root `CLAUDE.md`, "Git and GitHub"). The
  rate is stated against the commit until he tags one, and `.github/workflows/release.yml` attaches or
  quotes it on a tag if the ADR says so.
- CI runs the conformance tests on Windows, Linux and macOS with each platform's reference build.
  Each platform has its own rate.

## Acceptance criteria

- [ ] The pass-rate file exists with one row per platform: runnable, passed, failed and skipped
      counts, the pinned build's SHA-256, and the commit. The Windows row comes from a local run, and
      the Linux and macOS rows from a CI run whose URL is recorded in Notes.
- [ ] A test fails, naming the case, when a case listed as passing in the file fails, and tells the
      reader to update the file when the count rises.
- [ ] If the ADR has `release.yml` state the rate, a `workflow_dispatch` dry run shows it.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
