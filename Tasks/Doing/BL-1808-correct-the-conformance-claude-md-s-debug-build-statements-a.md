---
id: BL-1808
title: Correct the conformance CLAUDE.md's Debug build statements and amend ADR-0080 on where port variables and SSH keys are made
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md]
requirement: FR-053
created: 2026-11-26
completed:
---
# BL-1808 — Correct the conformance CLAUDE.md's Debug build statements and amend ADR-0080 on where port variables and SSH keys are made

## Goal

The conformance library's `CLAUDE.md` and ADR-0080 say what the code does about the Debug build, port variables and SSH keys.

## Context

Found during BL-1805 (2026-11-26).

- The conformance `CLAUDE.md`, "Selection is built", says `UpstreamCurlBuildFeatures.For` never adds
  `Debug` or `TrackMemory`. It adds both for a pin whose `features` line lists them (ADR-0173).
- Its "Running the http and https cases is built" paragraph says every case runs on the reference
  build. `UpstreamTestCasesAgainstSurlTests` also runs some on the Debug build (ADR-0173) and the
  admitted libtest cases with the `libtests` binary (ADR-0174).
- ADR-0080 places each server name's port variable and the SSH keys in `Surl.Conformance.UnitLibrary`.
  Only some `UpstreamTestCaseServers` rows carry a `PortVariable`; `Surl.Conformance.UnitTests`
  maps the rest and `UpstreamTestCaseFileServers` there makes the keys. Add a dated amendment.

## Acceptance criteria

- [ ] The conformance `CLAUDE.md` says `Debug` and `TrackMemory` are features of a pin that lists
      them.
- [ ] Its description of `UpstreamTestCasesAgainstSurlTests` names the Debug build and the
      `libtests` binary.
- [ ] ADR-0080 has a dated amendment, marked "Decided by Claude under Stewart's delegation", saying
      where the port variables and SSH keys are made.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
