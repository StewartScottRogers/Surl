---
id: BL-1464
title: Withhold the MQTT 5 CONNECT password from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1464 — Withhold the MQTT 5 CONNECT password from the exchange log

## Goal

With the BL-1418 contract implemented, ADR-0182's replacement text is logged in place of the MQTT 5 CONNECT password and Authentication Data property on -v, --trace, --trace-ascii and --log-file.

## Context

- Filed by BL-1417 (ADR-0182): an extended server whose client bytes carry a secret in recoverable form. The server's own parser shows where the field sits; the redactor must work across chunk boundaries and keep the user name visible.
- Extended servers are judged by their protocol's specification (ADR-0083), not the Curl port.

## Acceptance criteria

- [x] A fast test in the server's .UnitTests shows the redacted bytes for a split-chunk exchange and that a server answer is unchanged.
- [x] The server's raw bytes still reach the server unchanged and the event stream follows ADR-0182.

## Notes

- Mqtt5ClientSecretBytesRedactor (CONNECT password + Authentication Data, AUTH Authentication Data); other packets pass unheld. Mqtt5BodyReader gained ReadBytes and a public ReadPropertyValue. mqtt5+ws is not redacted (WebSocket frames at the engine level; a redactor there needs frame unmasking) - left out of scope.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. MQTT 5 CONNECT password and Authentication Data are withheld from the exchange log
