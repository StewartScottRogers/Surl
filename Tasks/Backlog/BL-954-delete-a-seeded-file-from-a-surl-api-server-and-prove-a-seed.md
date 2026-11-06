---
id: BL-954
title: Delete a seeded file from a Surl.Api server and prove a seeded queue message is consumed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-810]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-954 — Delete a seeded file from a Surl.Api server and prove a seeded queue message is consumed

## Goal

A .NET test deletes a file it seeded from a running `SurlServer` with `SurlServer.Files.DeleteAsync(path)`, and a
message seeded with `SurlServer.Messages.EnqueueAsync` is shown consumed by a queue client over a fake connection.

## Context

- BL-810 delivered `Mail`, `Files.PutAsync`, `Messages`, `Accounts`, `ResetAsync`, `SnapshotAsync` and
  `RestoreAsync` on `SurlServer` (`Application/Surl.Api`). Its planning surface (ADR-0124 decision 3) also names
  `Files.DeleteAsync(path)`, left out because `ComposedStores` (`Application/Surl.Composition.UnitLibrary`) has no
  file delete: add `ComposedStores.DeleteFileAsync` over `ContentStore`'s delete (publishing
  `StoreChangedEvent` with `Deleted`, never consulting the fault plan), then wrap it as `SurlServerFiles.DeleteAsync`
  throwing `SurlSeedRefusedException` for `Absent`, `NotEmpty` and `NotPermitted`.
- `EnqueueAsync`'s success is covered only by sharing `SurlServerMessages.ThrowIfRefusedAsync` with
  `PublishAsync`: no queue-declaring server (STOMP, AMQP 0-9-1, AMQP 1.0) is composed by `Surl.Composition` yet, so
  no queue exists in a `Surl.Api` test. Once one is composed (`AreExtensionsTurnedOn = true`), add a test: a STOMP
  client `SEND`s to `/queue/q1` (declaring it durable), the test enqueues, and a STOMP `SUBSCRIBE` receives both.
  If no queue server is composed when this runs, add its ID to `depends-on` instead.
- Update `Application/Surl.Api/README.md` ("Seed, reset, snapshot") and both projects' `CLAUDE.md`.

## Acceptance criteria

- [ ] Named test: a file put at `/a.txt` and deleted with `Files.DeleteAsync` is answered `404` over a fake HTTP
      connection; deleting it again throws `SurlSeedRefusedException` with `Refusal` `Absent`.
- [ ] Named test: a message seeded with `Messages.EnqueueAsync` into a queue a fake client declared is delivered to
      a fake subscriber of that queue.
- [ ] `dotnet build` is clean, the fast tests are green, and `Surl.Api` and `Surl.Composition.UnitLibrary` keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
