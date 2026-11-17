---
id: BL-1347
title: Cover the LDAP account check that finds its timeout already passed in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Authentication.UnitTests, Foundation/Surl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1347 — Cover the LDAP account check that finds its timeout already passed in Surl.Authentication

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.9% line, 99.95% branch, 1 failing member.
- `LdapAccountSource.CheckWithinTimeoutAsync` (async) - `Foundation/Surl.Authentication.UnitLibrary/LdapAccountSource.cs:95`,
  86.67% line, 50% branch. The true side of `if (timeProvider.GetElapsedTime(startedAt) >= timeoutLength)` at line 107
  is never taken, so lines 108 to 111 (`timeout.Cancel(); linked.Token.ThrowIfCancellationRequested();`) never run:
  a check that waited for a slot until its timeout had already passed, before its own timer fired.

## Acceptance criteria

- [x] A named `Surl.Authentication.UnitTests` test holds every slot, advances an injected `TimeProvider` past the
      timeout without letting the timer callback cancel the wait first, frees a slot, and asserts the check is
      refused as timed out and never connects to the directory (lines 108 to 111 covered).
- [x] `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Added Surl.Authentication.UnitLibrary to touches (no other Doing task names it): line 111, the brace after ThrowIfCancellationRequested, was unreachable because timeout.Cancel() already cancels the linked token; replaced by an explicit throw of OperationCanceledException(linked.Token), same behaviour.
- Test: LdapAccountSourceTests.CheckThatGetsASlotAfterItsTimeoutPassed_IsRefusedAsTimedOut_WithoutConnecting with a timerless TimeProvider.
- Tests only (test-writer). Use a hand-written `TimeProvider` (no package); a timer that does not fire on its own is
  what lets the slot reach line 107 with the elapsed time past the timeout.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. LdapAccountSource timeout-already-passed path covered; Surl.Authentication.UnitLibrary at 100/100, 0 failing
