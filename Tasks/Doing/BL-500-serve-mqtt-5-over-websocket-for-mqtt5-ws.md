---
id: BL-500
title: Serve MQTT 5 over WebSocket for mqtt5+ws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-499, BL-408]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-500 — Serve MQTT 5 over WebSocket for mqtt5+ws

## Goal

The MQTT 5 broker accepts a WebSocket upgrade offering subprotocol `mqtt`, carries MQTT packets in binary frames
(a packet may span frames, and a frame may hold several packets), and refuses text frames, all as MQTT 5.0
chapter 6 says.

## Context

- Decision: BL-494's ADR, decision 7.
- The WebSocket handshake and frames: BL-408's library (add its reference). MQTT 5.0 chapter 6:
  `[MQTT-6.0.0-1]` (binary frames only; close on another frame type), `[MQTT-6.0.0-2]` (a packet may span frames,
  and frames are not aligned to packets), `[MQTT-6.0.0-3]` and `[MQTT-6.0.0-4]` (subprotocol `mqtt`).
- The broker's packet handling is reused unchanged: the WebSocket stream is presented as the byte stream the
  packet reader already reads.

## Acceptance criteria

- [ ] Named tests show:
      - an upgrade offering `mqtt` accepted with `Sec-WebSocket-Protocol: mqtt`;
      - one not offering it refused as the ADR says;
      - a CONNECT split across two binary frames answered;
      - two packets in one frame both handled;
      - a text frame closing the connection.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
