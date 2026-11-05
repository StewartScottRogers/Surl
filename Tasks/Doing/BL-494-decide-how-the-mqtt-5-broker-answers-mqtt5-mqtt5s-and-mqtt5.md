---
id: BL-494
title: Decide how the MQTT 5 broker answers mqtt5, mqtt5s and mqtt5+ws
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Mqtt5.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-494 — Decide how the MQTT 5 broker answers mqtt5, mqtt5s and mqtt5+ws

## Goal

An ADR decides how `surl mqtt5://`, `mqtt5s://` and `mqtt5+ws://` answer as an MQTT Version 5.0 broker. It
covers the session and retained-message state, the properties honoured, flow control, topic aliases,
subscription options, shared subscriptions, enhanced authentication, reason codes, the WebSocket transport, TLS
and limits. It also decides how the broker stays apart from the original MQTT 3.1.1 server. A server-side
requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specification: OASIS MQTT Version 5.0 (7 March 2019):
  - its normative statements `[MQTT-x.y.z-n]`, which become the matrix's IDs;
  - chapter 4 (operational behaviour);
  - chapter 5 (security);
  - chapter 6 (WebSocket: binary frames, subprotocol `mqtt`).
- Which packets a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Mqtt5.UnitLibrary`
  and its matrices `Mqtt5.md`, `Mqtt5-PublishSubscribe.md` and `Mqtt5-Transports.md`. The port's
  `Mqtt5ReferenceBroker` is copyable code.
- The original MQTT 3.1.1 server, `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary` (ADR-0031), may not be
  referenced. Its code may be copied (it is Surl's own), with its MQTT 5 differences made explicit. Decide whether
  `mqtt5` retained messages are shared with `mqtt`'s `MqttRetainedMessages`. Planning assumption: they are not,
  because the isolation rule forbids the reference and the payload formats differ.
- WebSocket: BL-408's library. Enhanced authentication can use `Surl.Authentication`'s SCRAM exchanges
  (BL-416) through the SASL contracts.

Decide and record:

1. The CONNACK properties the broker sends: Receive Maximum, Maximum QoS, Retain Available, Maximum Packet Size,
   Topic Alias Maximum, Wildcard/Subscription Identifier/Shared Subscription Available, Server Keep Alive, and
   Assigned Client Identifier.
2. Authentication: username and password from the accounts file, required by default (CONNACK `0x86`/`0x87`),
   anonymous only under `--allow-anonymous`, and the enhanced-authentication methods offered (planning:
   `SCRAM-SHA-1` and `SCRAM-SHA-256`, `0x8C` for others).
3. Session state: Clean Start, Session Expiry Interval, messages queued for an offline session, and the limits.
4. QoS 2 and Receive Maximum both ways, topic aliases both ways, Message Expiry Interval, the Payload Format
   Indicator check, and user-property forwarding.
5. Subscription options (No Local, Retain As Published, Retain Handling), Subscription Identifiers, and
   `$share/` shared subscriptions.
6. The reason code and DISCONNECT the broker sends for each protocol error (chapter 4.13).
7. `mqtt5+ws`: the path, the subprotocol, and packets spanning frames. `mqtt5s` over TLS.
8. Each ADR-0006 limit, and the Maximum Packet Size.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 8, each citing the MQTT 5.0 section, and is
      listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Mqtt5.md` exists in the matrix format with every `[MQTT-…]` statement that
      binds a server, each "covered by" naming BL-495, BL-496, BL-497,
      BL-498, BL-499 or BL-500, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
