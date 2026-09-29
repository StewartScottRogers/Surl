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
completed: 2026-09-29
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

- [x] Tests replay the measured CONNECT with `-u` and prove the fake contract received the
      exact user name and password bytes, and that the server answers `CONNACK` 0 when
      accepted and ADR-0032's code (then closes) when refused.
- [x] Tests prove the no-credentials CONNECT, the password-over-`mqtt://` case (no
      `TlsSession`) and the same over an `mqtts` connection are answered as ADR-0032
      decision 5 says.
- [x] Tests prove a CONNECT whose will or credential fields are cut short is still
      `Malformed` (closed with no reply, ADR-0014), and a will is skipped, not kept.
- [x] The measured curl exit codes for `CONNACK` 4 and 5 are written in the fixture
      `README.md`s (they are what BL-118 asserts end to end).
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Mqtt.UnitLibrary
      -warnaserror` and `dotnet build Surl.Console -warnaserror` are clean; the fast tests
      pass; 100% line and branch coverage kept; no method exceeds complexity 10.

## Notes

- Plan: `MqttConnectJudge` now reads every field the connect flags announce - client
  identifier, will topic and message (skipped, `TrySkipWill`), user name, password
  (`MqttBodyReader.TryReadBinary`) - and returns an `MqttConnectJudgement` carrying the
  login. A well-formed CONNECT is `MqttConnectVerdict.LoginToCheck`; `MqttPacketResponder`
  passes a `PasswordLogin` (listen scheme, user name, password bytes, `connection.TlsSession`)
  to the `IAuthenticationPolicy` and maps `Accepted` -> CONNACK 0, `RefusedCredentials` ->
  CONNACK 4, `RefusedAnonymous`/`RefusedPlaintext` -> CONNACK 5, closing after 4 and 5.
- Decision (sensible default, no ADR needed): the server decides nothing itself - the
  plain-text and anonymous rules of ADR-0032 decision 5 live in the policy, which receives
  the TLS session, so the tests use a fake policy (`UnitTestRecordingAuthenticationPolicy`,
  with `RefuseWithoutTls` to stand in for the plain-text rule).
- Decision: a cut-short field wins over an empty-identifier refusal: every announced field is
  read before CONNACK 2 is chosen, so a CONNECT is judged malformed first (MQTT 3.1.1 4.8).
- `MqttProtocolServer(MqttRetainedMessages)` stays as an overload passing
  `AnonymousAuthenticationPolicy` (ADR-0032 section 6), so `Surl.Console` compiles unchanged;
  BL-117 composes the real policy.
- Measured (curl 8.21.0 win-x64): CONNACK 4 and CONNACK 5 both end curl with exit 8,
  `curl: (8) Expected 0000 but got 000n`. Fixtures: `connect-user-and-password`,
  `connack-bad-user-name-or-password`, `connack-not-authorized`.
- Gates: Surl.Protocol.Mqtt.UnitLibrary 100% line, 100% branch, worst CRAP 10, no member
  over complexity 10; 235 MQTT tests pass. `dotnet format --verify-no-changes` reports
  line-ending errors only in `Surl.Console/ServerTlsComposition.cs`, outside this task.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. MQTT CONNECT user name and password go to the authentication contract; CONNACK 0/4/5 per ADR-0032 decision 5, measured curl exit 8 for 4 and 5
