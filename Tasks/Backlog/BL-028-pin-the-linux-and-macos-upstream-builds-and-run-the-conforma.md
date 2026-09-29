---
id: BL-028
title: Pin the Linux and macOS upstream builds and run the conformance tests in CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-027, BL-020]
touches: [UpstreamCurlBuilds.json, .github/workflows/ci.yml]
requirement: none
created: 2026-09-28
completed:
---
# BL-028 — Pin the Linux and macOS upstream builds and run the conformance tests in CI

## Goal

`UpstreamCurlBuilds.json` pins the Linux and macOS builds BL-027's ADR names, and the
`CI` workflow obtains them, verifies them, and runs the `Surl.Conformance` integration
tests on Linux and macOS. Those tests pass there instead of reporting Inconclusive.

## Context

- BL-027's ADR, indexed in `Documentation/Planning/Decisions/README.md`, is the
  specification: sources, build or download steps, how the SHA-256 is verified, and
  which job runs the conformance tests.
- BL-007's locator selects by `platform` and verifies the SHA-256. BL-020's tests call
  `Assert.Inconclusive` where no pinned build is present.
- `.github/workflows/ci.yml` is the workflow whose green run on all three platforms
  gates the dark factory's merge to `master` (root `CLAUDE.md`). A red conformance job
  blocks merges, so it must be green before this task is Done.
- The SHA-256 values come from the procedure the ADR fixes, for example a CI run's
  output. Record the run URL in this task's Log.

## Acceptance criteria

- [ ] `UpstreamCurlBuilds.json` has one entry per platform the ADR names, with all the
      fields the existing entry has. The existing Windows entries are unchanged.
- [ ] `ci.yml` obtains each build as the ADR says, refuses to run it if its SHA-256
      differs from the pin, and runs
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` including
      Integration on Linux and macOS.
- [ ] A `CI` run on this task's branch is green on Windows, Linux and macOS, and on
      Linux and macOS the conformance tests report passed, not inconclusive. The run URL
      is in the Log.

## Notes

## Log

- 2026-09-28: Created.
