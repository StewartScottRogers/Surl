---
id: BL-115
title: Verify MQTT CONNECT user names and passwords through the authentication contract
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-109]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: FR-020
created: 2026-09-29
completed:
---
# BL-115 — Verify MQTT CONNECT user names and passwords through the authentication contract

## Goal

The MQTT server reads the user name and password of every `CONNECT` and asks BL-109's
authentication contract whether to accept it, answering `CONNACK` with the return code
ADR-0032 decision 5 names when it does not - including a CONNECT with no credentials and a
password sent over `mqtt://` rather than `mqtts://`.

## Context

FR-020 and ADR-0032's MQTT row; ADR-0032 (BL-100) decision 5; MQTT 3.1.1 sections 3.1.3.4
and 3.1.3.5 (user name, password), 3.2.2.3 (return codes 4 "bad user name or password" and
5 "not authorized"). BL-109 added the contract. This supersedes ADR-0014's "a will, user name
and password ... are accepted and not read" (ADR-0032 records it).

- `Surl.Protocol.Mqtt.UnitLibrary/MqttConnectJudge.cs` validates the connect flags and the
  client identifier and stops; it must go on to read the will topic and message (skipped),
  then the user name and password fields the flags announce, each cut-short field staying
  `Malformed`. `MqttConnectVerdict.cs` gains the refusal verdicts; `MqttPacketResponder.cs`
  / `MqttPacketEncoder.cs` write `CONNACK`; after a refused `CONNACK` the server closes
  (MQTT 3.1.1 section 3.2.2.3).
- `MqttProtocolServer(MqttRetainedMessages)` gains the contract (keep `Surl.Console`
  compiling as BL-114 does; composition is BL-117). `connection.TlsSession` tells `mqtts`.
- Measure with `Record-CurlExchange.ps1 -Raw` (see its `.PARAMETER Raw`) and the pinned
  reference build (curl 8.21.0): `-sS -u tester:secret mqtt://127.0.0.1:P/t` and without
  `-u`, recording the CONNECT bytes, and curl's exit code and stderr for a `CONNACK` with
  return code 4 and with 5 (`-RawReply` canned bytes). Save them as fixtures under
  `Surl.Protocol.Mqtt.UnitTests/Fixtures/` beside the existing ones.

## Acceptance criteria

- [ ] Tests replay the measured CONNECT with `-u` and prove the fake contract received the
      exact user name and password bytes, and that the server answers `CONNACK` 0 when
      accepted and ADR-0032's code (then closes) when refused.
- [ ] Tests prove the no-credentials CONNECT, the password-over-`mqtt://` case (no
      `TlsSession`) and the same over an `mqtts` connection are answered as ADR-0032
      decision 5 says.
- [ ] Tests prove a CONNECT whose will or credential fields are cut short is still
      `Malformed` (closed with no reply, ADR-0014), and a will is skipped, not kept.
- [ ] The measured curl exit codes for `CONNACK` 4 and 5 are written in the fixture
      `README.md`s (they are what BL-118 asserts end to end).
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Mqtt.UnitLibrary
      -warnaserror` and `dotnet build Surl.Console -warnaserror` are clean; the fast tests
      pass; 100% line and branch coverage kept; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
