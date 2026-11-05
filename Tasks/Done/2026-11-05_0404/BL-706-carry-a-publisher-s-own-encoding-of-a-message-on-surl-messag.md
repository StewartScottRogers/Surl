---
id: BL-706
title: Carry a publisher's own encoding of a message on Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-664]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-706 — Carry a publisher's own encoding of a message on Surl.MessageBus

## Goal

A `BusMessage` can carry its publisher's own encoding of the message, tagged with the format it is in, beside the
protocol-neutral fields. A server reading a message in its own format gets those bytes back unchanged, from fan-out,
a queue, the log and after a restart; every other server ignores them.

## Context

- Decision: ADR-0107 decision 6 ("The native form"). The AMQP 1.0 container (BL-513) stores each message it
  receives with its sections from `message-annotations` to the end, byte for byte, in format `amqp1`, and its
  header's five fields, so that an `amqp1` receiver gets the sections the sender wrote (AMQP 1.0 Part 3 section
  3.2: the bare message is immutable).
- Shape (planning names, the implementer may refine them in the ADR's spirit): `BusMessage.NativeForm`, a
  `BusNativeForm(string Format, ReadOnlyMemory<byte> Bytes)` or null; `Format` is a lower-case scheme family name.
  Its bytes count toward the bus's queued-bytes and log-bytes bounds (ADR-0100 decision 3.6) and toward
  `MaxMessageBytes` together with the payload.
- Persistence: `BusMessageCodec` writes and reads it in `.surl/bus` (ADR-0100 decision 3.7). A file written before
  this change still loads, its messages with no native form.
- Depends on BL-664 because it changes the same library (declared topics and queues); the two must not run at once.

## Acceptance criteria

- [x] Named tests show a message with a native form delivered with its bytes and format unchanged through live
      fan-out, a named queue (also after a reject with requeue) and the topic log.
- [x] A named test shows the native form surviving a save and load under `.surl/bus`, and a bus file written
      without native forms still loading.
- [x] A named test shows the native form's bytes counted against `MaxMessageBytes` and the queued-bytes bound.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-509 (ADR-0107 decision 6) for BL-513, which depends on it.
- Shape kept as planned: `BusMessage.NativeForm` is a `BusNativeForm(string Format, ReadOnlyMemory<byte> Bytes)` or
  null. `MessageBus.Publish`'s copy copies its bytes, so a publisher reusing its buffer cannot change a held message.
- Counting: an internal `BusMessage.PayloadAndNativeFormBytes` is what `MaxMessageBytes` (both `Publish` and
  `PublishToQueue`), `MaxQueuedBytes` (enqueue, settle, expiry drop, consumer end, queue delete, load) and
  `LogRecordBytes` count. Choice: `MaxRetainedPayloadBytes` stays payload only - its name says payload, and the task
  names only the message, queued and log bounds.
- Codec choice: flag bit `0x80`, which was "never set" and rejected, now means a native form follows the key (format as
  a 4-byte-length UTF-8 string, then the bytes). Files written before BL-706 never set it, so they load unchanged and
  with no native form; no file header version bump was needed. The malformed row for `0x80` now pins a native form
  that runs into the end of the message.
- Tests: `MessageBusNativeFormTests` (fan-out, queue with reject-and-requeue, topic log, save and load across queue,
  log and retained slot, an old log file loading with none, `MaxMessageBytes` on publish and queue send, the queued-
  bytes bound freed on acknowledge, `LogRecordBytes`), and `BusMessageCodecTests`' every-property bytes now include a
  native form.
- Measure-CodeQuality: 100% line and branch. The codec's `Flags`, `WriteOptionalFields` and `ReadOptionalFields` were
  each split in two to stay under complexity 10. Five older members still over it (`NatsSubjectNames`,
  `KafkaTopicNames`, `MqttTopicNames`, `MessageBusFiles.DecodeQueueIndexEntry`, `MessageBus.RequireWithinBounds`) are
  covered by BL-695, whose goal is no failing member in this library.
- ADR-0100's byte formats are BL-703's to write; it should add flag `0x80` and the native form.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. BusMessage carries a publisher's native form unchanged through fan-out, queues, logs and .surl/bus, counted against the byte bounds
