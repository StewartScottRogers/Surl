---
id: BL-512
title: Open AMQP 1.0 connections, sessions and links with their error conditions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-511]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-512 — Open AMQP 1.0 connections, sessions and links with their error conditions

## Goal

The AMQP 1.0 container answers `open`, `begin`, `attach`, `detach`, `end` and `close` with their state machines.
It maps channels and handles, resolves source and target addresses to nodes, keeps the idle timeout with empty
frames, and ends each misuse with the error condition the specification names.

## Context

- Decision: BL-509's ADR, decisions 3, 4 and 7.
- AMQP 1.0:
  - Part 2, section 2.4 (connections, the open/close states, and idle time-out: send an empty frame at half the
    peer's value);
  - section 2.5 (sessions, channel numbers, and `begin` with `remote-channel`);
  - section 2.6 (links, handles, attach and detach, and an attach for an unknown node answered with an attach
    with a null source or target followed by a detach carrying the error);
  - section 2.8.15 (`amqp:not-found`, `amqp:unauthorized-access`, `amqp:connection:framing-error`,
    `amqp:session:unattached-handle`, `amqp:invalid-field`).
  Part 3, section 3.5 (source and target).
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - open, begin and attach as sender and as receiver;
      - an attach to an unknown address refused as section 2.6.3 says;
      - a frame on an unknown channel ends the connection with the right condition;
      - a transfer on an unattached handle gets `amqp:session:unattached-handle`;
      - empty frames sent at half the peer's idle time-out;
      - a silent peer closed after the container's idle time-out;
      - close from the client answered with close.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
