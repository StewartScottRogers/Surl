---
id: BL-1458
title: Withhold the NATS CONNECT pass and auth_token from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1458 — Withhold the NATS CONNECT pass and auth_token from the exchange log

## Goal

With the BL-1418 contract implemented, ADR-0182's replacement text is logged in place of the NATS CONNECT JSON `pass` and `auth_token` (and `user` stays) (`NatsConnectOptions.cs:34`) on -v, --trace, --trace-ascii and --log-file.

## Context

- Filed by BL-1417 (ADR-0182): an extended server whose client bytes carry a secret in recoverable form. The server's own parser shows where the field sits; the redactor must work across chunk boundaries and keep the user name visible.
- Extended servers are judged by their protocol's specification (ADR-0083), not the Curl port.

## Acceptance criteria

- [ ] A fast test in the server's .UnitTests shows the redacted bytes for a split-chunk exchange and that a server answer is unchanged.
- [ ] The server's raw bytes still reach the server unchanged and the event stream follows ADR-0182.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
