---
id: BL-1304
title: Add the non-gating curl-port-phase-7 workflow that builds the Curl port on linux-x64, osx-arm64 and win-x64
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/curl-port-phase-7.yml]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1304 — Add the non-gating curl-port-phase-7 workflow that builds the Curl port on linux-x64, osx-arm64 and win-x64

## Goal

A `curl-port-phase-7` GitHub Actions workflow builds the Curl port at the commit `CurlPortBuilds.json` pins on `ubuntu-latest`, `macos-14` and `windows-latest`, hashes it twice and compares with the pin.

## Context

ADR-0089 Amendment 8 (who builds, SHA-256 check, non-gating). Reuse `.github/actions/upstream-curl` for the upstream builds. Triggers: `workflow_dispatch` and weekly `schedule` only. Test running is added by BL-1307 and BL-1308.

## Acceptance criteria

- [ ] `.github/workflows/curl-port-phase-7.yml` exists, triggers only on `workflow_dispatch` and `schedule`, and has one build job per platform running the pin's `publishCommand` twice into two folders.
- [ ] The job fails when the two publishes differ or when the hash differs from the platform's `CurlPortBuilds.json` pin, and prints the hash when there is no pin yet.
- [ ] `ci.yml` is unchanged and `RunDarkFactory.ps1` still reads only the `CI` workflow, so the workflow gates no merge (stated in a comment at the top of the file).

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
