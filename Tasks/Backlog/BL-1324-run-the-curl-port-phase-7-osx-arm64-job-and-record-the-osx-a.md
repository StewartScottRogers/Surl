---
id: BL-1324
title: Run the curl-port-phase-7 osx-arm64 job and record the osx-arm64 findings in each family file
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1308, BL-1327]
touches: [Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1324 — Run the curl-port-phase-7 osx-arm64 job and record the osx-arm64 findings in each family file

## Goal

A completed run of the `test-curl-port-osx-arm64` job is recorded in an osx-arm64 section of each Phase 7 family file.

## Context

BL-1308 added the job to `.github/workflows/curl-port-phase-7.yml`. A lane cannot push or dispatch a workflow, so the run itself is this task's first step: dispatch `curl-port-phase-7` (`gh workflow run`), download the `phase-7-osx-arm64-results` artifact. ADR-0089 decision 8 and Amendment 8.

## Acceptance criteria

- [ ] A run of `curl-port-phase-7` with the osx-arm64 test job completed.
- [ ] Each family file in `Documentation/Conformance/CurlPort` has an osx-arm64 section naming the commit and findings.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Waits on BL-1327: win-x64 build-curl-port hash mismatch (run 43982591865) skips the osx-arm64 test job
