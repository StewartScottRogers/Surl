---
id: BL-1639
title: Replace the coverage exclusions on UnixSocketConnectionListener that fast tests reach
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1638]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1639 — Replace the coverage exclusions on UnixSocketConnectionListener that fast tests reach

## Goal

`Foundation/Surl.Networking.UnitLibrary/UnixSocketConnectionListener.cs` carries `[ExcludeFromCodeCoverage]` only on a
member that no fast test can reach, each with a comment directly above it saying why, and the library still measures
100% line and 100% branch.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical results).
- The file has 18 exclusions (lines 34, 51, 58, 79, 108, 128, 139, 164, 173, 185, 194, 207, 210, 219, 232, 235, 248 in
  the audited tree). Each one's Justification reads "covered by UnixSocketConnectionListenerTests". But
  `Foundation/Surl.Networking.UnitTests/UnixSocketConnectionListenerTests.cs` has 15 `[TestMethod]`s and **no**
  `TestCategory("Integration")`, so those tests run in the fast set on Windows, Linux and macOS. An exclusion whose
  reason is "a fast test covers it" removes the member from the report without a reason it cannot be reached. That is how
  the uncovered retry path (`RetryPause`, line 274, fixed by BL-1638) stayed hidden.
- Contrast: `TcpConnectionListener`, `UdpDatagramListener` and `SocketListenerFactory` exclusions name tests that *are*
  tagged `Integration` (`TcpConnectionListenerTests.cs:9`, `SocketListenerFactoryTests.cs:9`). They are out of scope.

## Acceptance criteria

- [x] Every `[ExcludeFromCodeCoverage]` on a member of `UnixSocketConnectionListener` that a fast test executes on
      Windows is removed.
- [x] Each exclusion that remains (e.g. the `File.SetUnixFileMode` call off Windows, or a branch only an OS failure
      reaches) has a comment directly above it naming why no fast test on Windows can reach it, not which test covers it.
- [x] Any line or branch that the removal exposes is covered by a named, platform-neutral test in
      `Surl.Networking.UnitTests`, or is itself one of the justified exclusions above.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports `100 | 100`
      with 0 failing members.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Mostly tests (test-writer), plus the attribute removals. No behaviour change.
- Depends on BL-1638 because both edit `UnixSocketConnectionListener.cs`.
- Left excluded, each with a Windows-unreachable reason: `MakeOwnerOnly` (SetUnixFileMode off Windows only), `RemoteEndPointOf` (vanished-peer SocketException), new `AcceptOrDropClient` (macOS-only ArgumentException arm). Made `internal` and unit-tested: `CreatedFilePathOf`, `WriteTimeOf`, `ClassifyBindFailure`, `DeleteFileMadeByFailedStart`, `TryDelete`, `ReleaseSocket`, `BindListeningSocket` (takes the owner-only step as a parameter so a failing step is testable). No behaviour change.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Exclusions removed; Networking measures 100/100, fast tests green.
