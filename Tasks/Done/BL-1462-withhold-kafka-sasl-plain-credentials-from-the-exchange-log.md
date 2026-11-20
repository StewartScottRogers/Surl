---
id: BL-1462
title: Withhold Kafka SASL PLAIN credentials from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1462 — Withhold Kafka SASL PLAIN credentials from the exchange log

## Goal

With the BL-1418 contract implemented, ADR-0182's replacement text is logged in place of the Kafka `SaslAuthenticate` request bytes of SASL PLAIN on -v, --trace, --trace-ascii and --log-file.

## Context

- Filed by BL-1417 (ADR-0182): an extended server whose client bytes carry a secret in recoverable form. The server's own parser shows where the field sits; the redactor must work across chunk boundaries and keep the user name visible.
- Extended servers are judged by their protocol's specification (ADR-0083), not the Curl port.

## Acceptance criteria

- [x] A fast test in the server's .UnitTests shows the redacted bytes for a split-chunk exchange and that a server answer is unchanged.
- [x] The server's raw bytes still reach the server unchanged and the event stream follows ADR-0182.

## Notes

- Decided under delegation: `KafkaSaslPlainBytesRedactor` keeps each frame's original size prefix and BYTES length (as the HTTP/2 redactor keeps frame lengths, ADR-0182 amendment 3) and replaces only the password with `****`, leaving authzid and authcid visible. Only PLAIN is masked; SCRAM stays as received (decision 8). A v0 handshake's raw token is masked the same way. No new ADR needed: it applies decision 6.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Kafka SASL PLAIN password logged as **** on -v, --trace and --log-file; user name kept, SCRAM and other requests as received
