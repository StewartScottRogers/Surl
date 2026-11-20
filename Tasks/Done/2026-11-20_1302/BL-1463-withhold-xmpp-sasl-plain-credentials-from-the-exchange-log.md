---
id: BL-1463
title: Withhold XMPP SASL PLAIN credentials from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1463 — Withhold XMPP SASL PLAIN credentials from the exchange log

## Goal

With the BL-1418 contract implemented, ADR-0182's replacement text is logged in place of the XMPP SASL `<auth mechanism="PLAIN">` base64 body on -v, --trace, --trace-ascii and --log-file.

## Context

- Filed by BL-1417 (ADR-0182): an extended server whose client bytes carry a secret in recoverable form. The server's own parser shows where the field sits; the redactor must work across chunk boundaries and keep the user name visible.
- Extended servers are judged by their protocol's specification (ADR-0083), not the Curl port.

## Acceptance criteria

- [x] A fast test in the server's .UnitTests shows the redacted bytes for a split-chunk exchange and that a server answer is unchanged.
- [x] The server's raw bytes still reach the server unchanged and the event stream follows ADR-0182.

## Notes

- The base64 holds authzid, user and password in one value, so the whole text is replaced, as ADR-0183 does for line protocols; the user name stays visible in the server's login notes and on the event stream. Only PLAIN is redacted (ADR-0182 decision 8). Decided by Claude under Stewart's delegation.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. XMPP SASL PLAIN auth text is logged as **** across chunk splits; raw bytes and answers unchanged
