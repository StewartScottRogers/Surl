---
id: BL-1332
title: Confirm two dispatched curl-port-phase-7 runs pass build-curl-port on win-x64 and osx-arm64 after BL-1331
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [CurlPortBuilds.json]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1332 — Confirm two dispatched curl-port-phase-7 runs pass build-curl-port on win-x64 and osx-arm64 after BL-1331

## Goal

Two dispatched curl-port-phase-7 runs pass build-curl-port on linux-x64, osx-arm64 and win-x64 after BL-1331.

## Context

BL-1331 (ADR-0175 Amendment 2) made win-x64 and osx-arm64 compare only their in-run publishes; this confirms it in CI.

## Acceptance criteria

- [x] Runs 43998848915 and 44000208980 (factory/phase-1 at c87b86d5c, workflow_dispatch) both concluded success, every job green.

## Notes

Dispatched one after the other, not together: the workflow's concurrency group cancels an in-flight run on the same ref. No file changed; the workflow is read-only and gates nothing.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Two dispatched curl-port-phase-7 runs (43998848915, 44000208980) passed on all platforms.
