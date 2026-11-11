---
id: BL-931
title: Make CoapFlowObserversTests.ServeAsync_DeleteFromAnotherFlow_Notifies404WithoutObserveAndRemovesTheObserver deterministic
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-931 — Make CoapFlowObserversTests.ServeAsync_DeleteFromAnotherFlow_Notifies404WithoutObserveAndRemovesTheObserver deterministic

## Goal

`CoapFlowObserversTests.ServeAsync_DeleteFromAnotherFlow_Notifies404WithoutObserveAndRemovesTheObserver` passes on every run, under full-solution parallel load included.

## Context

- Seen by BL-599 on 2026-11-06 (lane 2, commit 19d126c9): during `Measure-CodeQuality.ps1`'s full
  `dotnet test` run it failed once at `CoapFlowObserversTests.cs:217`,
  `Assert.AreEqual(0, harness.Observations.Count)` - expected 0, actual 1 - and passed when
  `Surl.Extended.Protocol.Coap.UnitTests` was rerun alone and in the next full run.
- An observation counted before the assertion suggests the test reads `Observations` before the
  server has finished acting on the DELETE (or a notification races the observer's removal). Find
  the race; wait on the event the server raises, not on time, and use the injected `TimeProvider`.
- A flaky fast test can turn CI red on one platform and stop the dark factory's merge to `master`.

## Acceptance criteria

- [ ] The test asserts only after the server's handling of the DELETE has completed, through an awaited signal, not a delay.
- [ ] The test passes 200 times in a row: `dotnet test Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests --filter "FullyQualifiedName~DeleteFromAnotherFlow"` in a loop.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
