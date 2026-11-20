---
id: BL-1433
title: Withhold the MQTT CONNECT password from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1433 — Withhold the MQTT CONNECT password from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed mqtts://127.0.0.1:0/`, answering
`curl -k -u alice:s3cret -d hi mqtts://127.0.0.1:<port>/t`, never logs the CONNECT packet's password
bytes; they are written as BL-1417's ADR says.

## Context

- Bug report item: secrets in the `-v` log. An MQTT 3.1.1 CONNECT (section 3.1.3.5) ends with the
  password as two length bytes and the bytes, present when connect-flags bit 6 is set.
- `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/MqttProtocolServer.cs` is the server;
  `MqttBodyReader.cs:100` reads the length-prefixed password and `MqttConnectJudge.cs` judges it. The
  redactor parses the fixed header's remaining length (1 to 4 bytes) and the CONNECT variable header
  across chunk boundaries, then rewrites only the password field. Only the first packet of a
  connection is a CONNECT.
- `MqttUpstreamTestCaseServer.cs` keeps raw bytes (Surl.Conformance).

## Acceptance criteria

- [ ] `MqttProtocolServer` implements BL-1418's contract. Fast tests in
      `Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests` show a CONNECT with user name and password
      logged with the password in the ADR's form, also split across reads at every byte.
- [ ] A CONNECT with a will message and a password, and one without a password, are handled (named
      tests); a later PUBLISH whose payload holds the password's bytes is logged unchanged.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [ ] `dotnet build Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
