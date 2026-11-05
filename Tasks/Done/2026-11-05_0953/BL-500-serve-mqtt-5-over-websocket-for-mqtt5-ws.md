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
completed: 2026-11-05
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

- [x] Named tests show:
      - an upgrade offering `mqtt` accepted with `Sec-WebSocket-Protocol: mqtt`;
      - one not offering it refused as the ADR says;
      - a CONNECT split across two binary frames answered;
      - two packets in one frame both handled;
      - a text frame closing the connection.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Plan: `Mqtt5WebSocketServer` (scheme `mqtt5+ws`) wraps a `Mqtt5Broker`: reads the upgrade with
  `HttpConnectionReader`, checks it, answers `101` with `Sec-WebSocket-Protocol: mqtt`, then hands
  the broker `Mqtt5WebSocketConnection`, an `IConnection` that reads binary/continuation payloads
  as one byte stream and writes each broker write as one unmasked binary frame. The broker is
  unchanged. References added: `Surl.HttpMessage` and `Surl.WebSocket` (ADR-0093 decision 0
  allows both; `ProtocolIsolationTests` passes).
- Choices ADR-0093 decision 7 leaves open, taken by RFC 6455's defaults:
  - an unmasked client frame, an `RSV` bit, a continuation with nothing to continue, a binary
    frame inside a fragmented message, and any frame `WebSocketFrameReader` finds invalid close
    `1002` (RFC 6455 sections 5.1-5.4, 7.4.1); a frame over `--max-message` closes `1009`
    (Message Too Big) rather than an MQTT `DISCONNECT 0x95`, because the frame is refused from
    its header before any packet byte is known;
  - a client close is answered with a close echoing its status code (RFC 6455 5.5.1 "typically
    echos"), or an empty close for an empty one;
  - after any close is sent, reading ends (the broker sees a network loss, so the Will is
    published) and the broker's later writes are dropped, as 5.5.1 allows no data frame after a
    close; when the broker returns, close `1000` follows unless a close was already sent, then
    a half-close and the one-second lingering close `WebSocketLingeringClose` gives;
  - an upgrade without exactly one `Host` is `400`, and a head not read is answered as the
    WebSocket server answers it (`431`, `505`, `408`, `400`, or no bytes), as ADR-0071 does;
  - the path is compared without its query, so `/mqtt?x=1` is served;
  - the head timeout runs from accept to CONNACK: the broker is given what the upgrade left of
    it (decision 8's table).
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Mqtt5.UnitLibrary` reports
  100% line and 100% branch. The three members over its own complexity count
  (`Mqtt5InboundPublishes.ResolveAlias`, `Mqtt5BusMessages.PropertiesOf`,
  `Mqtt5InboundPublishes.Judge`) are BL-497's, untouched, and were reported in BL-499; every
  method this task added is at 10 or below and the CA1502 build gate passes.
- Tests: `Mqtt5WebSocketServerTests` and `Mqtt5WebSocketConnectionTests`; the Mqtt5 test
  project now runs 363 tests, all green, and the whole fast suite is green.
- Not done here, by design: registering `mqtt5+ws` in `Surl.Composition` and its `--aihelp`
  facts are BL-501's.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The MQTT 5 broker serves mqtt5+ws: upgrade with subprotocol mqtt, packets across binary frames, text frames closed 1003
