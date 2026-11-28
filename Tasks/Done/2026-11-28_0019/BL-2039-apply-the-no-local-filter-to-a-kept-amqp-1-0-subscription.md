---
id: BL-2039
title: Apply the no-local filter to a kept AMQP 1.0 subscription
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2038]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2039 — Apply the no-local filter to a kept AMQP 1.0 subscription

## Goal

An unshared kept AMQP 1.0 subscriber (`never`, `session-end` or `connection-close`) that is echoed the no-local
filter does not receive what AMQP 1.0 connections with its `container-id` publish, and a shared subscription is
not echoed the filter (ADR-0146 Amendment 3, BL-2037).

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; prove it with a failing test first.
- The entry is echoed (`Amqp1SourceFilters.NoLocalPredicate`, `Amqp1Terminus.Source`) but
  `Amqp1ContainerConnection.Subscriptions.cs`, `MakeQueueAndConsume`, never passes it to the bus, against
  ADR-0146 decision 2.1. Follow ADR-0146 Amendment 3: set `BusConnection.PublisherName` to `amqp1:` and the
  `container-id` once the client's `open` is read, pass the same text as `excludedPublisherName` when declaring
  an unshared kept subscription with no-local (both added by BL-2038), and leave no-local out of a shared
  subscription's applied filters, echo and description.
- A lookup attach (no source) is answered the stored filters (`LookKeptSubscriptionUp`), so they must keep
  matching what is applied.
- Existing live-link test:
  `Amqp1ContainerConnectionTests.ServeAsync_TopicLinkWithNoLocal_WithholdsTheConnectionsOwnPublishesOnly`.
  Matrix row `Amqp1:Messaging-3.5.8-3` in `Documentation/Conformance/Extended/Amqp1.md`.
- Stand-in: the shared message bus. No new event or fault.

## Acceptance criteria

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: an unshared kept subscription with no-local does not receive a publish from a connection with its `container-id`, made while attached or detached, and does receive one from another `container-id`; the test failed before the change (or Notes say why this is no defect).
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: a kept subscription without the filter receives its own connection's publish, and a shared one asking no-local is not echoed it and receives its own connection's publish.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: a later connection with the same `container-id`, link name, address and filters resumes the subscription, still with no-local; the same attach without no-local replaces it.
- [x] The library's `CLAUDE.md` no longer says no-local is not applied to a kept subscription and states the rule.
- [x] Row `Amqp1:Messaging-3.5.8-3` cites the new tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-27: Confirmed as a defect. The new tests in `Amqp1ContainerConnectionTests.NoLocal.cs` were run before the
  change: 10 cases failed (a kept subscription took its own publishes; a shared one was echoed the filter).
  The two controls (no filter; replaced by an attach without no-local) passed before and after.
- Change: `open` sets `BusConnection.PublisherName` to `amqp1:<container-id>`; an unshared kept subscription with
  no-local declares its queue excluding it; `Amqp1SourceFilters.Read` takes `appliesNoLocal` (renamed from
  `isTopic`), false for a shared kept subscription. One rule covers the attach, its echo, its description and a
  lookup, so a lookup of a shared subscription an earlier build stored with no-local is not echoed it.
- Decisions follow ADR-0146 Amendment 3; no new ADR. Extended server: no upstream curl or Curl port run.
- Verified: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` on both projects; fast
  tests 30565 passed, 0 failed, 3 skipped (AMQP 1.0: 878); `Measure-CodeQuality.ps1 -Library
  Surl.Extended.Protocol.Amqp1.UnitLibrary` 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. An unshared kept AMQP 1.0 subscription with no-local takes nothing its container-id publishes; a shared one is not echoed the filter.
