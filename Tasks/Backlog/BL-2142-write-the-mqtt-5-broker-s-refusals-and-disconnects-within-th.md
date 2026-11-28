---
id: BL-2142
title: Write the MQTT 5 broker's refusals and DISCONNECTs within the one-second write deadline
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2142 — Write the MQTT 5 broker's refusals and DISCONNECTs within the one-second write deadline

## Goal

The `mqtt5` and `mqtt5s` broker gives up on a refusing `CONNACK` or a server `DISCONNECT`, those for a limit included, one second after it decides to send it, and then closes gracefully. This task changes nothing in `Documentation/Product/Requirements.md`; BL-2143 re-states NFR-017.

## Context

Found while working BL-2120 (2026-11-28). In `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerConnection.cs`, `DisconnectAsync` (packet too large `0x95`, quota `0x97`, receive maximum `0x93` and the rest) stops deliveries and writes under no deadline, and `RefuseConnectAsync` (`CONNACK` `0x95`, `0x97` and the rest) does the same, so a peer that stops reading holds the connection until the idle timeout. ADR-0006 section 5 gives a limit's refusal a one-second write deadline. ADR-0093's DISCONNECT table also says `0x8B` Server shutting down is written within that one second, but `ShutdownNoticeTimeout` is 2 s: make it one second too. The deadline runs on `context.TimeProvider` and covers stopping the deliveries and waiting for the write gate. When it passes, note it and close without aborting. `mqtt5+ws` shares this code; keep it working. A connection past a connection limit is closed with no bytes, as ADR-0006 section 5 says for framed protocols; leave it. Stand-in: a test with a stalled client sees the exchange end at the deadline, and a Will is still settled as before; nothing new to seed, reset or publish.

## Acceptance criteria

- [ ] A test with a connection whose write never completes shows a `DISCONNECT` `0x95` ends the exchange once one second has passed on a fake `TimeProvider`, and not before, without aborting the connection.
- [ ] A test shows the same for a refusing `CONNACK`.
- [ ] A test shows the `0x8B` shutdown notice is given up on after one second.
- [ ] A test shows the same bound when a delivery in progress never completes.
- [ ] The existing tests still send the same bytes, and `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests -warnaserror` is clean with the fast tests passing and no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
