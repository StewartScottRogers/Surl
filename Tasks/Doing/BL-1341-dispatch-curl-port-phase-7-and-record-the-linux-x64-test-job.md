---
id: BL-1341
title: Dispatch curl-port-phase-7 and record the linux-x64 test job outcome in each family file (BL-1340 follow-up)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1340]
touches: [Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1341 — Dispatch curl-port-phase-7 and record the linux-x64 test job outcome in each family file (BL-1340 follow-up)

## Goal

The linux-x64 test job of `curl-port-phase-7` runs the Phase 7 families against the Curl port it publishes, and each family file records it.

## Context

BL-1340 added the `SURL_CURL_PORT_CI_SHA256` acceptance (ADR-0175 Amendment 3) but a lane cannot dispatch a workflow. Once BL-1340 is integrated on the pushed branch, run `gh workflow run curl-port-phase-7` and read the run.

## Acceptance criteria

- [ ] A dispatched `curl-port-phase-7` run's linux-x64 test job runs the Phase 7 families with no `UnpinnedCurlPortClientException`.
- [ ] Each family file's linux-x64 section records the outcome.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Blocked. Stewart: push BL-1340 (9317c53d7) then run 'gh workflow run curl-port-phase-7'; a lane cannot push or dispatch workflows
- 2026-11-17: Blocked -> Backlog. Unblocked: curl-port-phase-7 dispatched on factory/phase-1 at a5337bdf7 (BL-1339 and BL-1340 included), run 44044472038, succeeded; the linux-x64 job compared against the port (371 passed, 71 failed, 47 skipped). Read its artifacts.
- 2026-11-17: Backlog -> Doing.
