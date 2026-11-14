---
id: BL-1101
title: Make the LDAP account source's seventeenth-check test hold at 16 connects on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-1101 — Make the LDAP account source's seventeenth-check test hold at 16 connects on Linux and macOS

## Goal

`LdapAccountSourceTests.SeventeenthCheck_WaitsForASlot_InsideItsOwnTimeout` passes on CI's
Windows, Linux and macOS jobs every run, and a seventeenth concurrent LDAP check never opens a
seventeenth directory connection.

## Context

CI run 43404096613 (PR #28, frozen branch `factory/merge-20261113-020606`): the test failed on
both "Build and test (ubuntu-latest)" and "Build and test (macos-latest)" with
`Assert.AreEqual(LdapAccountSource.MaxConcurrentChecks, directory.ConnectCount)`: expected 16,
actual 17. It passed on Windows. That holds the dark factory's merge to `master`.

- Test: `Foundation/Surl.Authentication.UnitTests/LdapAccountSourceTests.cs:538`. It starts
  `MaxConcurrentChecks + 1` checks against a hanging fake directory and asserts the connect
  count right away, before advancing the fake clock.
- Production: `LdapAccountSource` in `Foundation/Surl.Authentication.UnitLibrary`.
- Find out first which side is wrong. 17 connects means one of these: the slot limit lets a
  seventeenth check connect (a production bug, so fix the limit), or the count is read before
  every check has reached its slot wait (a test race, so make the test wait deterministically
  through the fake, not with a delay).

## Acceptance criteria

- [x] The task's Log names the cause: either the slot limit admits a seventeenth connect, or the test reads the count too early.
- [x] A seventeenth concurrent check never calls the directory's connect while 16 checks hold slots, shown by a test that fails without the fix.
- [x] `SeventeenthCheck_WaitsForASlot_InsideItsOwnTimeout` uses no `Task.Delay`, `Thread.Sleep` or real time. It waits on the fake directory or the fake `TimeProvider`.
- [x] The test passes 50 runs in a row on Windows: `dotnet test Foundation/Surl.Authentication.UnitTests --filter "Name~SeventeenthCheck"` looped 50 times.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green. Surl.Authentication.UnitLibrary keeps 100% line and branch coverage.

## Notes

Filed by an interactive session on 2026-11-13 after PR #28's Linux and macOS jobs failed. Lanes
test only on Windows, where this passes, so reason about the race rather than rely on
reproducing it.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Cause: neither a too-early read nor a slot limit that admits a seventeenth (the semaphore holds 16). The failing assert is the one after the clock advance: the fake clock fires the 16 timers one after another, a timed-out check frees its slot, and the waiting seventeenth check is handed it before its own timer has fired, so it connects (17). Fix: `LdapAccountSource` notes the start with `GetTimestamp` and, once it holds a slot, cancels and throws if the timeout length has already elapsed on the injected clock. `ManualTimeProvider` (Authentication tests) now also moves `GetTimestamp` with `Advance`. The existing test, which already uses no delay or real time, now holds. 50 looped runs passed on Windows, fast suite green.
- 2026-11-13: Doing -> Done. LDAP source: a check past its timeout never connects after taking a freed slot; the seventeenth-check test holds
