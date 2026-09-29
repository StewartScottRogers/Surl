---
id: BL-094
title: Persist MQTT retained messages through the content file-system seam and reload them at start
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-090, BL-091]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: FR-023
created: 2026-09-29
completed:
---
# BL-094 — Persist MQTT retained messages through the content file-system seam and reload them at start

## Goal

`MqttRetainedMessages` can be given a persistence that writes every change to a file in a
state folder through `IContentFileSystem` and loads that file at start, so retained
messages survive a restart; without one it stays in memory only, as today.

## Context

FR-023; ADR-0031 (BL-090) decision 6 gives the file name, byte format, when it is written
and what a malformed or unreadable file does at start. ADR-0014 decision 7 bounds the
store (`MaxTopics`, `MaxTotalPayloadBytes`); a loaded file is held to the same bounds.

- `Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessages.cs`: the dictionary-backed store,
  locked by `messagesLock`; `Retain` is the only mutator.
- Add a reference from `Surl.Protocol.Mqtt.UnitLibrary.csproj` to
  `Surl.Content.UnitLibrary` (allowed by ADR-0002 and the project's `CLAUDE.md`; the
  reference check in `Surl.Protocol.Abstractions.UnitTests` permits it). The MQTT library
  must not call `System.IO.File` or `Directory` itself: it reads and writes through
  `IContentFileSystem` (`OpenFileForAsyncRead`, `CreateFileForAsyncWrite`,
  `MoveFileReplacing`, `CreateDirectory` from BL-091), writing a temporary file then
  renaming it into place, as `ContentStore` does for uploads (BL-086).
- Suggested shape (name what it does): `MqttRetainedMessageFile` holding an
  `IContentFileSystem` and the state folder's full path, with `LoadAsync` and a save
  called under the store's lock (or a serialised writer) so two concurrent publishes
  never interleave writes; `MqttRetainedMessages` takes it as an optional constructor
  argument. `Retain` is synchronous today: if saving must be async, change the call site
  in `MqttPacketResponder`/`MqttProtocolServer` accordingly (async all the way; no
  `.Result` or `.Wait()`).
- Tests drive it over `Surl.Content`'s production `InMemoryContentFileSystem` (BL-091), so
  no disk and no `Integration` category is needed.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Mqtt.UnitTests` (e.g. `MqttRetainedMessageFileTests`) prove,
      by name: a retained message written by one store over an
      `InMemoryContentFileSystem` is loaded by a second store over the same file system; an
      empty payload removes the topic from the file too; a message refused by the bounds is
      not written; the state folder is created when missing; the file is written through a
      temporary name and renamed into place (no partial file at the final name after a
      failure mid-write); a malformed file at start behaves exactly as ADR-0031 decision 6
      says; a file over the bounds loads no more than the bounds allow.
- [ ] The byte format written is the one ADR-0031 specifies, pinned by a test that compares
      the exact bytes for two topics.
- [ ] A store without persistence behaves exactly as today: every existing
      `MqttRetainedMessagesTests` and `MqttProtocolServerTests` case passes unchanged.
- [ ] `rg -n "System.IO.File\b|File\.|Directory\." Surl.Protocol.Mqtt.UnitLibrary --glob "*.cs"`
      finds no disk call.
- [ ] `dotnet build Surl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean, the fast tests
      pass, the library keeps 100% line and branch coverage, and no test needs
      `TestCategory=Integration`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
