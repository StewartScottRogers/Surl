---
id: BL-678
title: Take over a connected MQTT 5 client's session with DISCONNECT 0x8E
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-499]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-678 — Take over a connected MQTT 5 client's session with DISCONNECT 0x8E

## Goal

When a `CONNECT` names a client identifier that is already connected, the MQTT 5 broker sends the older
connection `DISCONNECT` `0x8E` Session taken over and closes it, handling its Will as MQTT 5.0 3.1.2.5 and 3.1.4
say, as ADR-0093 decision 1 ("Session takeover") decides.

## Context

- ADR-0093 decision 1, "Session takeover"; MQTT 5.0 `[MQTT-3.1.4-3]` and the non-normative note after it on when
  the older connection's Will is sent (Will Delay 0; Session Expiry 0 or the new Clean Start 1).
- BL-496 left it out: `Mqtt5BrokerState.RegisterClient` simply records the newer connection for the identifier, and
  `UnregisterClient` leaves it registered when the older one ends; the older connection keeps running. The taken-over
  connection needs a way to be told (for example a cancellation the broker links into its packet reads) and to write
  `DISCONNECT` `0x8E` before it closes.
- Depends on BL-499, which keeps sessions, so the takeover can resume or discard the session as Clean Start says.

## Acceptance criteria

- [ ] Named tests in `Surl.Extended.Protocol.Mqtt5.UnitTests` show:
      - a second connection's `CONNECT` with a connected identifier makes the first write `DISCONNECT` `0x8E`
        (`E0 01 8E`) and end, while the second gets `CONNACK` `0x00`;
      - the first connection's Will published at once when its Will Delay Interval is 0;
      - the first connection's Will published when the new `CONNECT` has Clean Start 1, and not when it has Clean
        Start 0 and a Will Delay Interval that has not passed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
