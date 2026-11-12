---
id: BL-818
title: Write the injected-fault and parallel-instances stand-in recipes as executable tests against Surl.Api servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-808, BL-796]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-818 — Write the injected-fault and parallel-instances stand-in recipes as executable tests against Surl.Api servers

## Goal

Two recipe test classes show how a test makes the stand-in fail on purpose and checks its client's error
handling, and how several independent stand-ins run side by side in one test process without seeing each other.

## Context

- Same recipe rules as BL-817: short, commented, `[TestCategory("Integration")]` in
  `Conformance/Surl.Conformance.UnitTests`, pinned upstream curl 8.21.0 as the client (Inconclusive without the
  pin), never the Curl port, no sleeps or polling.
- `StandInFaultRecipeTests`: (1) a `RefuseLogin` fault for `alice` on an HTTP server - curl with `-u alice:...`
  gets 401 and the right exit code with `-f`; (2) a `ProtocolError` fault `451 4.3.0` on SMTP `RCPT TO` for one
  recipient (BL-796) - curl's exit code; (3) a `DropConnection` fault with `times=1` on HTTP - the first curl
  fails, a retry succeeds; each also asserts the `FaultAppliedEvent` through BL-807's query. Measure each exit code
  with the pinned build in this task (`Record-CurlExchange.ps1` or the test itself), state it in the test with the
  curl version, and never assume one.
- `StandInParallelInstancesRecipeTests`: two `SurlServer`s on `http://127.0.0.1:0`, each with different in-memory
  `Files`, started and used concurrently; curl fetches from each and gets that server's file; each server's
  `FindExchanges` holds only its own exchanges; a fault added to one does not affect the other. This complements
  `ParallelSurlProcessesTests` (processes) with in-process instances, which is what the "no static mutable state"
  principle protects; if it fails, the cause belongs to a BL-786 finding.
- Stand-in: fault-injectable and embeddable; uses the fault plan and event stream.

## Acceptance criteria

- [ ] Both classes exist with the tests named in Context, each passing on Windows with the pinned reference build,
      platform-neutral, each exit code asserted as measured with its curl version stated.
- [ ] No test uses `Thread.Sleep`, `Task.Delay` or a polling loop.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-12: Backlog -> Doing.
