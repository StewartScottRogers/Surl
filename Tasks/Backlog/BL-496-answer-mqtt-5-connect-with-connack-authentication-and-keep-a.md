---
id: BL-496
title: Answer MQTT 5 CONNECT with CONNACK, authentication and keep alive
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-495]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-496 — Answer MQTT 5 CONNECT with CONNACK, authentication and keep alive

## Goal

The MQTT 5 broker answers CONNECT with CONNACK carrying the properties BL-494's ADR lists. It checks the
protocol name and level, the username and password, and the Will. It assigns a client identifier when the client
sends none, enforces the keep alive at one and a half times its value, and refuses with the right reason code.

## Context

- Decision: BL-494's ADR, decisions 1, 2 and 8.
- MQTT 5.0:
  - 3.1 (CONNECT; `[MQTT-3.1.0-1]` the first packet; `[MQTT-3.1.2-1]` and `-2` on the protocol name and level,
    `0x84` Unsupported Protocol Version);
  - 3.1.2.10 and 3.1.2.11 (Keep Alive and its properties);
  - 3.1.3 (payload, Will Properties, and the zero-length Client Identifier);
  - 3.2 (CONNACK and its reason codes, Assigned Client Identifier `[MQTT-3.2.2-16]`);
  - 4.13 (errors).
- Logins go through the `Surl.Protocol.Abstractions` contracts, as `MqttConnectJudge` does for 3.1.1.
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - a CONNECT with good credentials answered with CONNACK `0x00` and the ADR's properties;
      - a bad password gets `0x86`;
      - no credentials with anonymous off gets `0x87`;
      - protocol level 4 gets `0x84`;
      - a zero-length client ID gets an Assigned Client Identifier;
      - a second CONNECT on the connection closes it with `0x82`;
      - a packet before CONNECT closes the connection;
      - silence past 1.5 times the keep alive closes it with `0x8D`;
      - the Will published on an abnormal close after its delay.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
