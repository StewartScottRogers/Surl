---
id: BL-755
title: Serve whole AMQP 1.0 connections with their head timeout and connection limits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-512]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-755 — Serve whole AMQP 1.0 connections with their head timeout and connection limits

## Goal

One `IConnectionProtocolServer` in `Surl.Extended.Protocol.Amqp1.UnitLibrary` serves a whole `amqp1` connection:
`Amqp1SecurityNegotiation` (BL-511), then `Amqp1ContainerConnection` (BL-512) with the account it logged in, under
ADR-0107 decision 7's head timeout and `--max-time` / connection limits.

## Context

- BL-511 built the headers and SASL, BL-512 the connection state machine after them; nothing yet runs one after
  the other, so BL-514 has no server to compose.
- ADR-0107 decision 7: `--head-timeout` runs from accept to the client's `open` being read, the headers, a TLS
  handshake and SASL included, and closes with no bytes; `--max-time` and the connection limits close with `close`
  and `amqp:resource-limit-exceeded` once `open` has been exchanged, otherwise with no bytes.
- Rows `Amqp1:Surl-Limits-2` and `Amqp1:Surl-Limits-3` in `Documentation/Conformance/Extended/Amqp1.md`.
- The node directory (`IAmqp1NodeDirectory`) is given to the server by the composition; BL-513 backs it with the bus.

## Acceptance criteria

- [ ] Named tests show:
      - a SASL login followed by `open`, `begin` and `attach` served on one connection, the account reaching the
        node directory;
      - no `open` within `--head-timeout` (SASL included) closed with no bytes;
      - an exchange cancelled for `--max-time` after `open` closed with `close` and `amqp:resource-limit-exceeded`.
- [ ] Rows `Amqp1:Surl-Limits-2` and `Amqp1:Surl-Limits-3` name those tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-512 (dark factory lane 4, 2026-11-05).

## Log

- 2026-11-05: Created.
