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
completed: 2026-11-05
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

- [x] Named tests show for each persisted kind (retained messages, durable queues, topic logs as the ADR lists
      them): written after a change, the exact bytes the ADR pins, loaded back equal, missing file loads empty,
      each malformed case named in the ADR raises the load exception with the file path and the ADR's reason,
      a failed write keeps the change in memory and reports it.
- [x] A test loads a `retained-messages` file in ADR-0031 decision 6's format and finds its messages on the bus.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

Decided by Claude under Stewart's delegation (2026-11-05). ADR-0100 3.7 says "BL-634 fixes the byte formats",
but `Documentation/Planning/Decisions` is in BL-516's `touches`, so the formats live in `MessageBusFiles`' and
`BusMessageCodec`'s remarks and the library's CLAUDE.md, are pinned by `MessageBusFilesTests` and
`BusMessageCodecTests`, and BL-698 (filed) adds them to the ADR, as BL-694 and BL-697 do for earlier tasks.

- **API.** `MessageBusFiles(fileSystem, <path>/.surl)` names every path; `MessageBus.LoadAsync(files)` loads once
  into an empty bus (else `InvalidOperationException`) and returns a `BusSaveResult`; `SaveChangesAsync` writes
  what changed and returns `Saved` or `StorageFailed` with the exception message (new
  `MessageBusOutcome.StorageFailed`, ADR-0100 3.6 names it). Explicit save after a change, as `MailboxStore` does:
  `Publish` stays synchronous and in memory, and a server awaits the save and notes a failure.
- **Message form.** Every `BusMessage` property kept so a loaded message equals the saved one: topic, flags byte
  (retain, UTF-8 payload, and which optional fields follow; 0x80 must be clear), QoS, publish time as UTC ticks,
  expiry, content type, response topic, correlation data, key, headers, payload. Every length is 4 bytes
  big-endian - one rule for every field.
- **Files.** `retained` (header `SURL-BUS-RETAINED-1\n`, ordinal topic order); `queues/index`
  (`SURL-BUS-QUEUES-1\n`, queues and filters in ordinal order, each message's file number and redelivered byte) with
  `queues/messages/<16 hex>` (`SURL-BUS-MESSAGE-1\n` + message); `logs/<16 hex>` (`SURL-BUS-LOG-1\n`, topic, start
  offset, records). The log file carries its topic and start offset, so no separate topic-number index is needed
  and an empty log (a retained topic with no records) persists too.
- **Logs** are appended to while they only grow, and rewritten whole through a temporary file when retention
  raised the start offset or a write to them failed, so a torn append is never trusted again.
- **Queues persisted:** durable and neither exclusive nor auto-delete (ADR-0100 3.7). Unacknowledged messages are
  saved in queue order and load as redelivered, as a consumer's end would have made them.
- **Bounds at load.** A file holding more than `MaxTopics`, `MaxLogBytes`, `MaxRetainedPayloadBytes`, `MaxQueues`,
  `MaxMessagesPerQueue` or `MaxQueuedBytes` allows is malformed (the MQTT retained file's own rule, ADR-0031 6).
  `MaxLogBytesPerTopic` is not checked at load: retention applies at the topic's next publish.
- **MQTT migration** reads the old file's format inside the bus (a horizontal library cannot reference
  `Surl.Protocol.Mqtt`), validating topics with `CanonicalTopicName` (MQTT's own names). A failed write of the
  migrated `retained` is returned from `LoadAsync`, not thrown: being unable to write is not "could not read".
- Unreferenced queue message files (left by a failed index write or a failed delete) are deleted at a later
  successful index write; unknown names in `logs` are ignored.
- Results: 393 tests in `Surl.MessageBus.UnitTests` (new: `BusMessageCodecTests`, `MessageBusFilesTests`,
  `MessageBusPersistenceTests`), library at 100% line and 100% branch coverage; solution build clean with
  `-warnaserror`; every fast test green.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.MessageBus loads and saves retained messages, durable queues and topic logs under .surl/bus, reads an old MQTT retained-messages file, and reports a failed write as StorageFailed
