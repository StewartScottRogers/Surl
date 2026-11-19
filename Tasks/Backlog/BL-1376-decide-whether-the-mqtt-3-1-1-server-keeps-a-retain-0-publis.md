---
id: BL-1376
title: Decide whether the MQTT 3.1.1 server keeps a RETAIN 0 publish as the topic's retained message
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-18
completed:
---
# BL-1376 — Decide whether the MQTT 3.1.1 server keeps a RETAIN 0 publish as the topic's retained message

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", decides whether the MQTT 3.1.1 server
(`mqtt`, `mqtts`) keeps a `PUBLISH` whose `RETAIN` flag is 0 as its topic's retained message, and says how a
curl user, a `Surl.Api` test and a `surl-mcp` agent leave a retained message for a later `curl mqtt://` fetch.

## Context

- From the Surly cookbook bug report of 2026-11-18 (finding 1), against commit `a0236e175`: after
  `surl --allow-anonymous mqtt://127.0.0.1:1883/`, `curl -d 22.0 mqtt://127.0.0.1:1883/sensors/3` (fixed header
  `0x30`, RETAIN 0) and then `curl mqtt://127.0.0.1:1883/sensors/3` receives `\x00\x09sensors/3` + `22.0`.
  Three cookbook writers found it independently. The report's client was partly the Curl port; per ADR-0003 the
  Curl port is no evidence here. The only evidence is the MQTT 3.1.1 specification and pinned upstream curl.
- Today's behaviour is deliberate: ADR-0014 decision 1 ("Every published message is kept ... whatever its
  `RETAIN` flag says"), carried out at `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/MqttPacketResponder.cs:267-271`
  (`Retain = true` on every `BusMessage`), described in `MqttProtocolServer.cs:12-16`, the library's `CLAUDE.md`,
  and `surl --aihelp mqtt` (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:232`).
- MQTT 3.1.1 section 3.3.1.3: with RETAIN 0 the server must not store the message as retained and must not
  remove or replace an existing retained message.
- Upstream curl cannot set RETAIN. In curl 8.21.0's `lib/mqtt.c` (tag `curl-8_21_0`,
  https://github.com/curl/curl/blob/curl-8_21_0/lib/mqtt.c) `mqtt_publish` writes the fixed header byte as the
  constant `MQTT_MSG_PUBLISH` (`0x30`, line 588), with no option to change it. ADR-0014's table also measured it:
  `-d hi` sends `30 05 00 01 74 68 69`.
- Ways to leave a retained message that already exist and do not depend on the RETAIN flag:
  `--topic <filter>;retain` (ADR-0102 decision 3; `MessageBus` makes a matching publish retained, "whichever
  protocol published it", `AiHelpProse.cs:247`, and the `messages` example in `AiHelpExamples.cs:78-84` already uses
  `--topic shop/#;retain`); `SurlServer.Messages.PublishAsync(topic, payload, retain: true)` in `Surl.Api`; and the
  `publish_message` tool's `retain` in `surl-mcp` (`Application/Surl.Mcp.UnitLibrary/PublishMessageTool.cs`).
- Finding 2 of the same report (the server sends `DISCONNECT` `E0 00` after the retained messages, which MQTT
  3.1.1 section 3.14 defines only from client to server) is part of this decision too, because it is the same
  one-shot subscribe. ADR-0014's table measured upstream curl 8.21.0 (win-x64 pin
  `0E773709...8778`): a bare close after the `PUBLISH` is exit 56, `Connection disconnected`; `DISCONNECT` is
  exit 0. The source agrees: `lib/mqtt.c` lines 858-864 (`MQTT_FIRST`: zero bytes read is
  `failf(data, "Connection disconnected")`, `CURLE_RECV_ERROR`) and lines 919-921 (`Got DISCONNECT`, done).
- Stand-in: the decision is about the one `MessageBus` every messaging server shares (ADR-0100 decision 3). It
  changes what a later subscriber on any bus protocol (`mqtt5`, NATS, STOMP, AMQP, Kafka) sees after an `mqtt`
  publish, so the ADR states the cross-protocol effect too. Nothing new is published on the event stream.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number), marked "Decided by Claude under
      Stewart's delegation", decides whether a RETAIN 0 `PUBLISH` is kept as retained, and whether it replaces or
      removes an existing retained message, citing MQTT 3.1.1 section 3.3.1.3 and `lib/mqtt.c` at `curl-8_21_0`
      line 588.
- [ ] The ADR says how each of a curl user, a `Surl.Api` test and a `surl-mcp` agent leaves a retained message
      that `curl mqtt://host/<topic>` then receives, naming `--topic <filter>;retain`, `Messages.PublishAsync(...,
      retain: true)` and `publish_message` with `retain` where they apply.
- [ ] The ADR keeps or changes ADR-0014 decision 2's server-sent `DISCONNECT`, citing ADR-0014's measured exit
      codes (56 for a bare close, 0 for `DISCONNECT`) and `lib/mqtt.c` lines 858-864 and 919-921.
- [ ] ADR-0014 carries a "Superseded in part" line pointing to the new ADR for each decision it changes, and
      `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR lists the work it leaves for BL-1377 (server, unit tests, `--aihelp mqtt` sentence, the fixture in
      `SurlServerCompositionTests.MessageBus.cs`) and for BL-1378 (the MQTT `Integration` tests), so neither needs
      a question.

## Notes

- Recommendation to weigh (the ADR decides): follow the specification - a RETAIN 0 publish is delivered to live
  bus subscribers but not retained, and neither replaces nor removes a retained message; a RETAIN 1 publish is
  retained, replaces, and an empty payload removes. Curl users seed with `--topic <filter>;retain`; tests with
  `Surl.Api` and `surl-mcp` seed with their `retain` flag. Keep the server-sent `DISCONNECT`: upstream curl is the
  oracle (ADR-0003) and exits 56 without it; `--aihelp mqtt` should say why it is sent.
- The Surly cookbook's MQTT recipes depend on today's behaviour and will need `--topic ...;retain` if it changes;
  that book is outside this repository.
- No code, no help text: a decision record only.

## Log

- 2026-11-18: Created.
