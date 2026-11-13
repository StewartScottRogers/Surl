---
id: BL-1096
title: Add unit tests for the 17 Mqtt5 matrix rows no test covers
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Documentation/Conformance/Extended/Mqtt5.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-1096 — Add unit tests for the 17 Mqtt5 matrix rows no test covers

## Goal

Every row of `Documentation/Conformance/Extended/Mqtt5.md` whose Covered-by cell is `UNCOVERED` gets a named test in `Surl.Extended.Protocol.Mqtt5.UnitTests`, written from the MQTT 5.0 specification, and the cell names that test; and the one row whose text disagrees with the tests (`MQTT5-2.2.2.2-1`) is settled by the specification.

## Context

The MQTT 5 server is an extended server (ADR-0083): it answers a scheme the Curl port adds and upstream curl does not have. It is judged by the MQTT 5.0 specification (OASIS, https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html). Tests replay bytes written from that specification. The Curl port is never a source of expected bytes or evidence, and no upstream curl measurement applies. Tests are fast: none carries `TestCategory=Integration`.

Projects: `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary` and its `.UnitTests` twin. Look at how existing tests in the UnitTests project drive the broker (injected listener seam, `TimeProvider`) and follow that style. Change production code only when a new test exposes a real bug, and say so in the Log.

The Covered-by cell format is whatever the already covered rows in `Mqtt5.md` use (test class and method name); follow it. This task edits only the `Covered by` cells of the listed rows, plus the `MQTT5-2.2.2.2-1` row if the decision below changes its text.

Overlap: BL-502 (Doing, Curl port mqtt5 client against a live surl) also touches `Mqtt5.md`. The board will not run the two at once; if BL-502 has changed the file, rebase before editing the cells.

The 17 UNCOVERED rows (row text in `Mqtt5.md`):
- Publish, will, alias: `Mqtt5:MQTT-3.1.2-14` (Will Retain 0 is published non-retained), `MQTT-3.3.1-8` (RETAIN 0 is not stored and changes no retained message), `MQTT-3.3.2-7` (Topic Alias mappings do not carry between connections), `MQTT-3.3.2-14` (a wildcard in Response Topic is a Protocol Error, 0x82).
- Flow control and expiry: `MQTT-3.3.4-10` (no non-PUBLISH packet is delayed by Receive Maximum), `MQTT-4.3.2-5` (QoS 1 Packet Identifier is reusable after PUBACK), `MQTT-4.3.3-7` (expiry is not applied once the QoS 2 PUBLISH is sent), `MQTT-4.3.3-13` (the QoS 2 sequence continues despite expiry), `MQTT-4.9.0-3` (other packets are answered while the send quota is zero).
- Unsubscribe, topics, shutdown: `MQTT-3.10.4-3` (started QoS 1 and 2 deliveries complete after UNSUBSCRIBE), `MQTT-4.7.3-3` (a topic is at most 65535 bytes), `MQTT-4.7.3-4` (no normalisation or substitution in matching), `MQTT5-3.14.2.1-2` (DISCONNECT 0x8B Server shutting down when the broker stops).
- Shared subscriptions: `MQTT-4.8.2-3` (delivery respects the chosen member's granted QoS), `MQTT-4.8.2-4` (a QoS 2 shared delivery cut by a lost connection is completed on that member's reconnect), `MQTT-4.8.2-5` (if that member's session ends first, no other member receives the message), `MQTT-4.8.2-6` (PUBACK or PUBREC of 0x80 or above discards the message, sent to no other member).

The disagreement: row `Mqtt5:MQTT5-2.2.2.2-1` says a property not allowed in its packet is a Protocol Error `0x82`, but the existing tests assert Malformed Packet `0x81`. Read MQTT 5.0 section 2.2.2.2 and the property tables (section 2.2.2.2 and 3.x) and decide which is right. Then align the code (and its tests) or the row to the specification, and record which and why in Notes. If the choice is a design call the specification leaves open, decide by the standing rules and record an ADR marked "Decided by Claude under Stewart's delegation".

Stand-in note: this task adds tests only, so the stand-in surfaces (shared store, event stream, seeding, fault injection) are unchanged and need no new work. Where a test asserts what a client sees, it is the same observation a stand-in test would make.

## Acceptance criteria

- [ ] Publish, will, alias group: a named test exists for each of `MQTT-3.1.2-14`, `MQTT-3.3.1-8`, `MQTT-3.3.2-7` and `MQTT-3.3.2-14` (the last asserts Protocol Error `0x82`), it passes, and each row's Covered-by cell names it.
- [ ] Flow control and expiry group: a named test exists for each of `MQTT-3.3.4-10`, `MQTT-4.3.2-5`, `MQTT-4.3.3-7`, `MQTT-4.3.3-13` and `MQTT-4.9.0-3`, it passes, and each cell names it.
- [ ] Unsubscribe, topics, shutdown group: a named test exists for each of `MQTT-3.10.4-3`, `MQTT-4.7.3-3`, `MQTT-4.7.3-4` and `MQTT5-3.14.2.1-2` (asserts DISCONNECT `0x8B`), it passes, and each cell names it.
- [ ] Shared subscription group: a named test exists for each of `MQTT-4.8.2-3`, `MQTT-4.8.2-4`, `MQTT-4.8.2-5` and `MQTT-4.8.2-6`, it passes, and each cell names it.
- [ ] `MQTT5-2.2.2.2-1` is settled by the specification: the row text and the tests agree on one reason code (`0x82` or `0x81`), and Notes says which section of MQTT 5.0 decides it.
- [ ] `Documentation/Conformance/Extended/Mqtt5.md` contains no `UNCOVERED`, and `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests -warnaserror` is clean and `dotnet test Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests --filter "TestCategory!=Integration"` is green, with the library still at 100% line and branch coverage.

## Notes

Sizing: 17 tests plus one decision. If a lane finds it running long, file the shared subscription group as a follow-up task rather than widen this one.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
