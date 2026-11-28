---
id: BL-2138
title: Decide the write deadline of the NATS and STOMP slow-consumer errors
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2138 — Decide the write deadline of the NATS and STOMP slow-consumer errors

## Goal

The decision records agree on how long the NATS and STOMP servers may take to write their slow-consumer error before closing.

## Context

Found while working BL-2120 (2026-11-28). ADR-0006 section 5 and NFR-017 give a refusal for a limit a write deadline of one second. ADR-0113 (decision 7 and the paragraph "The slow consumer's `-ERR` is bounded") gives `-ERR 'Slow Consumer'` 5 seconds, and `NatsServerOptions.SlowConsumerErrorTimeout` is 5 s; ADR-0118 decision 8 and `StompServerOptions.SlowConsumerErrorTimeout` (5 s) are STOMP's twin for `Resource limit exceeded`. Decide by the standing rules (root `CLAUDE.md`, "Decisions"): either the slow-consumer error is a named exception to ADR-0006 section 5 (a consumer that is slow by definition), or both servers use one second. BL-2140 (NATS) and BL-2141 (STOMP) apply the answer. Stand-in: a test that forces a slow consumer needs one stated time, on the injected `TimeProvider`, after which the connection closes.

## Acceptance criteria

- [ ] A dated amendment of ADR-0006, marked "Decided by Claude under Stewart's delegation", states the slow-consumer error's write deadline for `nats`, `natss`, `stomp` and `stomps`, with the alternative rejected and why.
- [ ] ADR-0113 and ADR-0118 state the same number, amended where it changed, and ADR-0006's header links the amendment.
- [ ] The amendment says in one sentence for each of the NATS and STOMP servers what its code must do, which is what BL-2140 and BL-2141 build.
- [ ] No file outside `Documentation/Planning/Decisions` and this task file changes.

## Notes

## Log

- 2026-11-28: Created.
