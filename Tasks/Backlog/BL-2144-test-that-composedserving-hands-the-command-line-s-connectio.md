---
id: BL-2144
title: Test that ComposedServing hands the command line's connection limits to one serving engine
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2148]
touches: [Application/Surl.Composition.UnitTests, Documentation/Product/Requirements.md]
requirement: NFR-008
created: 2026-11-28
completed:
---
# BL-2144 — Test that ComposedServing hands the command line's connection limits to one serving engine

## Goal

A fast test fails if `ComposedServing` stops handing the command line's connection limits to the serving engine, or if two listeners stop sharing one connection count, and NFR-008 to NFR-011 say so.

## Context

Found while working BL-2120 (2026-11-28). `Application/Surl.Composition.UnitLibrary/ComposedServing.cs` passes `SurlServerComposition.ComposeConnectionLimits(commandLine)` to `new ServingEngine(...)`. The limits and the engine are each tested apart, but nothing fails if that argument is dropped or each listener gets its own engine. Test through the start, as `SurlServerCompositionTests.Start.cs` does: `FakeListenerFactory.ConnectionsFor(listenUrl)` hands each of two listeners its connections, and a fake `TimeProvider` drives the timeouts and `ConnectionLimits.DefaultRefusalSettleDelay`. Use values far from the defaults (1024, 100, 120 s, 3600 s) so a default would let the test fail. Tests only: no production code changes. Stand-in: this holds that an embedded instance obeys the limits a test configures.

## Acceptance criteria

- [ ] A test in `Application/Surl.Composition.UnitTests` starts two listen URLs with `--max-connections 1`, holds a connection on one and shows a connection on the other is refused.
- [ ] A test does the same with `--max-connections-per-address 1` for two connections from one remote address.
- [ ] A test shows a silent connection ended after a short `--idle-timeout`, and another shows one ended after a short `--max-time` with `--idle-timeout 0`.
- [ ] NFR-008 to NFR-011's statuses say `Enforced by` the `ci.yml` fast-test job and name the existing test classes, the new one included, with the file's `Last updated` date that of the change.
- [ ] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
