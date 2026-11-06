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
  the session limit of WTH3 5.1 and 5.2 (draft -16 has no `SETTINGS_WT_MAX_SESSIONS`; ADR-0117 decision 5).
- The login goes through the `Surl.Protocol.Abstractions` contracts.

## Acceptance criteria

- [ ] Named tests show:
      - a session over ADR-0117 decision 5's limit (1 without flow control, 16 with) reset `H3_REQUEST_REJECTED`;
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
- 2026-11-05: Backlog -> Doing.
