---
id: BL-634
title: Persist the Surl.MessageBus under the data directory's .surl/bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-631, BL-633]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-634 — Persist the Surl.MessageBus under the data directory's .surl/bus

## Goal

With `--directory <path>`, `Surl.MessageBus` keeps its retained messages, durable queues and topic logs under
`<path>/.surl/bus/` and loads them at start, reading an existing MQTT 3.1.1 `retained-messages` file as BL-622's
ADR says; without a data directory nothing touches the disk.

## Context

- Decision: BL-622's ADR, decision 3's persistence paragraph: file names, byte formats, when each is written,
  and how `<path>/.surl/mqtt/retained-messages` (ADR-0031 decision 6, written by
  `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessageFile.cs`) is read. The ADR pins the byte
  formats; this task's tests pin them byte for byte.
- Rules to follow, as `Surl.MailStore` does (ADR-0050 decision 7): only through `IContentFileSystem`
  (`Foundation/Surl.Content.UnitLibrary`), writes through a `.<name>-<guid>` file then `MoveFileReplacing`,
  loaded once at start, a missing file is empty, malformed is a load exception carrying the full file path and
  reason (which `Surl.Composition` turns into `CouldNotReadFile` (37) and `surl: (37) Could not read <file path>:
  <reason>`), a failed write while serving leaves the change in memory and returns the exception message in the
  outcome for the server to note, nothing ends.
- Tests use `InMemoryContentFileSystem`; no test touches the disk.

## Acceptance criteria

- [ ] Named tests show for each persisted kind (retained messages, durable queues, topic logs as the ADR lists
      them): written after a change, the exact bytes the ADR pins, loaded back equal, missing file loads empty,
      each malformed case named in the ADR raises the load exception with the file path and the ADR's reason,
      a failed write keeps the change in memory and reports it.
- [ ] A test loads a `retained-messages` file in ADR-0031 decision 6's format and finds its messages on the bus.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
