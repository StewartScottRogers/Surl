---
id: BL-1640
title: Cover the cancelled delay hold and the aborted peer watch in Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1640 — Cover the cancelled delay hold and the aborted peer watch in Surl.Protocol.Abstractions

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Abstractions.UnitLibrary` reports 100% line and 100% branch with no
failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Abstractions
  99.91% line, 99.49% branch, 2 failing members. Both are in the ADR-0141 amendment 2/3 delay-hold code.
- `async CommandStageFaults.WaitForDelayOrPeerAsync`,
  `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CommandStageFaults.cs:122`, 92.86% branch, Cx 5. Line 150, the end
  of the `try { await hold; } finally { await holdEnded.CancelAsync(); }` block (lines 143-150), is 3 of 4: `hold` is
  never seen to *throw*. That happens when the exchange's `CancellationToken` is cancelled during the hold, with the
  watch not ended by the peer leaving.
- `async InMemoryConnection.WatchForPeerCloseAsync(int, CancellationToken)`, `InMemoryConnection.cs:183`, 93.33% line.
  Line 201 is uncovered: on a connection built with `peerHalfClosesWhenExhausted: false`,
  `WaitForCancellationOrAbortAsync` (line 200) never returns normally in a test. It always ends by cancellation. An
  `Abort()` of the connection while the watch waits should resume at line 201 and return `PeerClosed`. If it does not,
  the test will show what it does.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Abstractions.UnitTests` test applies a Delay rule on a watched `InMemoryConnection` that
      stays open (`peerHalfClosesWhenExhausted: false`), cancels the exchange's token during the hold, and asserts the
      apply ends with `OperationCanceledException` (or what the method's contract says) and the idle clock restarted.
- [ ] A named test starts `WatchForPeerCloseAsync` on an `InMemoryConnection(…, peerHalfClosesWhenExhausted: false)`,
      calls `Abort()`, and asserts the outcome it returns.
- [ ] Both tests use the injected `FakeTimeProvider`-style clock, with no real sleeps, and pass 20 runs in a row
      (`dotnet test --filter "FullyQualifiedName~<name>"`).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Abstractions.UnitLibrary` reports
      `100 | 100` with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). Only the `.UnitTests` project changes, so this runs beside protocol tasks.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
