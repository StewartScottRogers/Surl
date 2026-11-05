---
id: BL-460
title: Apply WebTransport flow control, session limits and authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-460 — Apply WebTransport flow control, session limits and authentication

## Goal

The WebTransport server enforces the flow-control limits and the session limit BL-457's ADR set, sending and
honouring the flow-control capsules. It requires a login on the CONNECT unless `--allow-anonymous` is given.

## Context

- Decision: BL-457's ADR, decisions 4, 5, 7 and 8.
- WTH3 "Flow Control" (`WT_MAX_DATA`, `WT_MAX_STREAMS`, `WT_DATA_BLOCKED`, `WT_STREAMS_BLOCKED`) and
  `SETTINGS_WT_MAX_SESSIONS`.
- The login goes through the `Surl.Protocol.Abstractions` contracts.

## Acceptance criteria

- [ ] Named tests show:
      - a session over `SETTINGS_WT_MAX_SESSIONS` refused as the ADR says;
      - a client exceeding `WT_MAX_STREAMS` ending the session with the draft's error;
      - data past `WT_MAX_DATA` refused;
      - `WT_MAX_DATA` raised as the server reads;
      - a CONNECT without a login refused;
      - one with a good login accepted;
      - an anonymous CONNECT accepted only under `--allow-anonymous`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
