---
id: BL-1057
title: Find and fix the intermittent failure in Surl.Extended.Protocol.Nats.UnitTests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-12
---
# BL-1057 — Find and fix the intermittent failure in Surl.Extended.Protocol.Nats.UnitTests

## Goal

Every test in `Surl.Extended.Protocol.Nats.UnitTests` passes every time on Windows, Linux and macOS.

## Context

- On 2026-11-11, while working BL-1034, dark factory lane 2 ran the fast suite twice in a row with no
  NATS change. The second run reported `Surl.Extended.Protocol.Nats.UnitTests`: Failed 1, Passed 180.
  The failing test's name was not captured. The failure did not come back in 30 more runs of that
  project alone, so it depends on timing and shows up under load, when the whole suite runs in parallel.
- Start by running the fast suite (or the NATS project, with other work loading the machine) in a loop
  and capture the failing test name and its message. Then look for a test that reads state once after
  awaiting a different event. BL-1034 had that shape: it awaited RESET_STREAM, then read STOP_SENDING,
  which arrives as a separate frame. Make the test wait on the event it asserts, never on time
  (root `CLAUDE.md`: no `Thread.Sleep`, inject `TimeProvider`).

## Acceptance criteria

- [x] The failing test and its cause are named in Notes, with the evidence (the captured failure and the race traced in the code).
- [x] That test passes 200 times in a row locally in a loop, and the fix waits on an event rather than on time.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the NATS library keeps 100% line and branch coverage.

## Notes

- Failing test: `NatsServerConnectionTests.ServeAsync_ClientPong_ClearsTheUnansweredCount` (the stale-ping test
  had the same exposure). Reproduced 1 in 72 runs by looping the project in 6 parallel jobs beside 2x-CPU busy loops
  (60 runs without the busy loops all passed). Captured: `Assert.StartsWith("INFO {", written)` failed with actual `""`
  at `AfterInfo` called from the wait condition in `AdvanceToNextWriteAsync` (`NatsServerConnectionTests.cs:321`).
- Cause: `ConnectAsync` returned once `CONNECT accepted` was logged and the ping timer was pending. INFO is written by
  the outbox's own writer task, so it may still be unwritten then; `AfterInfo` asserts the text starts with `INFO {`,
  so a wait condition polling it failed outright instead of waiting. The test read state once after awaiting a
  different event (the same shape as BL-1034).
- Fix (tests only): `ConnectAsync` also waits for INFO to be written, and the two polling conditions use the
  non-asserting `Answers` helper. No time-based wait added. After the fix: 200 runs (8 jobs x 25) under the same load, 0 failures.
- Library untouched; coverage unaffected.

## Log

- 2026-11-11: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Racy INFO assertion in NATS ping tests fixed; 200 loaded runs green
