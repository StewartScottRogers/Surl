---
id: BL-1308
title: Run the Phase 7 families on osx-arm64 and record the osx-arm64 findings
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1304, BL-1305, BL-1306]
touches: [.github/workflows/curl-port-phase-7.yml, Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1308 — Run the Phase 7 families on osx-arm64 and record the osx-arm64 findings

## Goal

The `curl-port-phase-7` workflow runs the Phase 7 families on osx-arm64 and the result is recorded in an osx-arm64 section of each family file.

## Context

ADR-0089 decision 8 and Amendment 8. Adds the macOS test job to the workflow of BL-1304.

## Acceptance criteria

- [ ] The workflow has an osx-arm64 test job running the Phase 7 families after the build, and a run of it completed.
- [ ] Each family file has an osx-arm64 section naming the commit and findings.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
