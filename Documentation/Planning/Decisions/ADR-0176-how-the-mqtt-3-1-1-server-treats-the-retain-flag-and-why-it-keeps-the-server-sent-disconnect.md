# ADR-0176 — How the MQTT 3.1.1 server treats the RETAIN flag, and why it keeps the server-sent DISCONNECT

- **Status:** Accepted
- **Date:** 2026-11-18
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-18, in
  BL-1376. No package, no threshold change, no download.
- **Supersedes in part:** [ADR-0014](ADR-0014-how-the-mqtt-server-answers.md) decision 1 (every publish kept
  whatever its `RETAIN` flag says). Keeps ADR-0014 decision 2's `DISCONNECT`.

## Context

The Surly cookbook bug report of 2026-11-18 (finding 1) found that `curl -d 22.0 mqtt://host/sensors/3`
(fixed header `0x30`, RETAIN 0) followed by `curl mqtt://host/sensors/3` receives the message. That is
ADR-0014 decision 1, carried out in `MqttPacketResponder.cs` (`Retain = true` on every `BusMessage`). MQTT
3.1.1 section 3.3.1.3 says otherwise: with RETAIN 0 the server must not store the message as retained and
must not remove or replace an existing retained message.

Evidence (ADR-0003: the specification and pinned upstream curl only, never the Curl port):

- Upstream curl cannot set RETAIN. `lib/mqtt.c` at tag `curl-8_21_0`, `mqtt_publish`, writes the fixed header
  as the constant `MQTT_MSG_PUBLISH` (`0x30`, line 588). ADR-0014's table measured `-d hi` as
  `30 05 00 01 74 68 69`.
- Finding 2 (the server sends `DISCONNECT` `E0 00` after the retained messages) is the same one-shot
  subscribe. ADR-0014 measured upstream curl 8.21.0 (win-x64 pin): a bare close after the `PUBLISH` is exit
  56, `Connection disconnected`; `DISCONNECT` is exit 0. The source agrees: `lib/mqtt.c` lines 858-864
  (`MQTT_FIRST`: zero bytes read is `failf(data, "Connection disconnected")`, `CURLE_RECV_ERROR`) and lines
  919-921 (`Got DISCONNECT`, done).

The retained messages live in the one `MessageBus` every messaging server shares (ADR-0100 decision 3), so
this decision also changes what a later subscriber on `mqtt5`, NATS, STOMP, AMQP or Kafka sees after an
`mqtt` publish. Nothing new is published on the event stream.

## Decision

1. **The server follows section 3.3.1.3.** A `PUBLISH` with RETAIN 1 is retained for its topic, replaces the
   topic's retained message, and with an empty payload removes it. A `PUBLISH` with RETAIN 0 is delivered
   to live bus subscribers (as today), is **not** stored as retained, and neither replaces nor removes an
   existing retained message. A topic matching a `--topic <filter>;retain` rule (ADR-0102 decision 3) is
   retained whatever the flag says, whichever protocol published it; that rule is the stand-in's way of
   saying "retain this" for a client that cannot. The bounds of ADR-0014 decision 7 (10000 topics, 100 MiB)
   still apply to what is retained.
2. **Why not keep the old behaviour.** It was chosen so a bare curl publish could be fetched back, but it
   breaks the specification for every other MQTT client and, through the shared bus, leaks MQTT's wrong
   retention into every other protocol. The need it served has a faithful, explicit answer (decision 3);
   a silent deviation does not.
3. **How each user leaves a retained message for `curl mqtt://host/<topic>`:**
   - a **curl user** starts `surl` with `--topic <filter>;retain` covering the topic (e.g.
     `--topic 'sensors/#;retain'`), then `curl -d 22.0 mqtt://host/sensors/3` is retained;
   - a **`Surl.Api` test** calls `SurlServer.Messages.PublishAsync(topic, payload, retain: true)` before the
     fetch, or any MQTT client that sets RETAIN 1;
   - a **`surl-mcp` agent** calls the `publish_message` tool with `retain` true.
4. **The server keeps sending `DISCONNECT` after the retained messages (ADR-0014 decision 2 stands).**
   MQTT 3.1.1 section 3.14 defines `DISCONNECT` only client to server, but upstream curl is the oracle
   (ADR-0003) and exits 56 on a bare close and 0 on `DISCONNECT`, per the measurements and `lib/mqtt.c`
   lines 858-864 and 919-921 above. `surl --aihelp mqtt` says why it is sent.

## Consequences

- Behaviour change: a plain `curl -d` publish to `mqtt` is no longer fetched back by a later
  `curl mqtt://` unless the topic is covered by `;retain`, the publisher sets RETAIN 1, or it was seeded
  through `Surl.Api` or `surl-mcp`. The Surly cookbook (outside this repository) needs `--topic ...;retain`
  in its MQTT recipes.
- A later subscriber on any bus protocol now sees only messages that were truly retained.

## Work left

- **BL-1377:** the server (`MqttPacketResponder.cs`, `MqttProtocolServer.cs` comments, the library's
  `CLAUDE.md`), unit tests for RETAIN 0 / 1 / empty-payload removal and the `;retain` rule, the
  `--aihelp mqtt` sentence (`AiHelpProse.cs`, and `AiHelpExamples` if it describes the old behaviour), and
  the fixture in `SurlServerCompositionTests.MessageBus.cs`.
- **BL-1378:** the MQTT `Integration` tests with pinned upstream curl, seeding with `--topic ...;retain`.

## Amendment 1 (2026-11-20, BL-1475): a retained seed ignores the `--topic` retain filter

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"). No package, no threshold
change, no download.

A retained seed - `SurlServer.Messages.PublishAsync(topic, payload, retain: true)`, `surl-mcp`'s
`publish_message` with `retain` true, and the control endpoint's publish - is kept on **any** topic, including
one outside every `--topic <filter>;retain` rule. Only a client publish consults that rule.

**Why.** `--topic ...;retain` exists because upstream curl cannot set RETAIN 1 (decision 3); it stands in for a
flag a curl client lacks. A seed states its RETAIN flag explicitly and is the test's own set-up of state (the
stand-in rule "Controllable"); refusing it would force a test to declare `--topic` just to seed. A seed that
the filter silently dropped would also be a surprise. No refusal error is introduced.

**Pinned by** `SurlServerTests.Messages_PublishAsyncRetained_OnATopicOutsideTheRetainFilter_IsStillKept` and
`StoreToolsTests.PublishMessage_RetainedOnATopicOutsideTheRetainFilter_IsStillKept`; stated in the `Surl.Api`
README, the `publish_message` description and `surl --aihelp mqtt`.
