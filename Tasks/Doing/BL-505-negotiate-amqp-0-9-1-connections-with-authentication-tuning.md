---
id: BL-505
title: Negotiate AMQP 0-9-1 connections with authentication, tuning and heartbeats
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-504]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-505 — Negotiate AMQP 0-9-1 connections with authentication, tuning and heartbeats

## Goal

The AMQP 0-9-1 broker runs the connection class: `start`/`start-ok` with the accounts check, `tune`/`tune-ok`,
`open`/`open-ok` for its virtual hosts, and `close`/`close-ok` both ways. It sends heartbeats and closes a silent
peer.

## Context

- Decision: BL-503's ADR, decisions 2, 3, 4 and 7.
- AMQP 0-9-1, section 1.4.2 (the connection class methods), section 4.2.7 (heartbeats: a peer silent for two
  intervals is dead), and the XML's reply codes (`403` ACCESS_REFUSED, `530` NOT_ALLOWED for an unknown virtual
  host, `501`, `503` COMMAND_INVALID, `504` CHANNEL_ERROR, `505` UNEXPECTED_FRAME). A failed login ends as the
  ADR decides: `connection.close` with `403`, or closing the socket. Cite the specification text the ADR relies on.
- Logins go through the `Surl.Protocol.Abstractions` contracts (`PLAIN` response `\0user\0pass`; the `AMQPLAIN`
  table with `LOGIN` and `PASSWORD`).
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - a full connection opened with `PLAIN`, and with `AMQPLAIN`;
      - a bad password closed as the ADR says;
      - an unknown virtual host gets `530`;
      - `tune-ok` above the server's maximums refused;
      - heartbeats sent at the negotiated interval;
      - a peer silent for two intervals closed;
      - a method on an unopened channel gets `504`;
      - `close` from the client answered with `close-ok`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
