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
completed:
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

- [ ] Named tests show a message with a native form delivered with its bytes and format unchanged through live
      fan-out, a named queue (also after a reject with requeue) and the topic log.
- [ ] A named test shows the native form surviving a save and load under `.surl/bus`, and a bus file written
      without native forms still loading.
- [ ] A named test shows the native form's bytes counted against `MaxMessageBytes` and the queued-bytes bound.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-509 (ADR-0107 decision 6) for BL-513, which depends on it.

## Log

- 2026-11-05: Created.
