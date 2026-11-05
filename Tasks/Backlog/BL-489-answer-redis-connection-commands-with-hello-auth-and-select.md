---
id: BL-489
title: Answer Redis connection commands with HELLO, AUTH and SELECT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-488]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-489 — Answer Redis connection commands with HELLO, AUTH and SELECT

## Goal

The Redis server answers `HELLO` (switching between RESP2 and RESP3, with its `AUTH` and `SETNAME` arguments),
`AUTH` (one- and two-argument forms, checked against the accounts), `SELECT`, `PING`, `ECHO`, `QUIT`, `RESET` and
`CLIENT`. Before login it answers every other command with `-NOAUTH`, and it answers an unknown command with its
error.

## Context

- Decision: BL-487's ADR, decisions 1, 2, 4 and 5.
- Command pages: `HELLO` (the map reply's fields `server`, `version`, `proto`, `id`, `mode`, `role`, `modules`;
  `-NOPROTO` for an unsupported version), `AUTH` (`-WRONGPASS invalid username-password pair or user is disabled.`),
  `SELECT` (`-ERR DB index is out of range`), `PING` and `ECHO`, `QUIT`, `RESET`.
- Logins go through the `Surl.Protocol.Abstractions` contracts (`IAuthenticationPolicy` or the policy the ADR
  names).

## Acceptance criteria

- [ ] Named tests show:
      - a command before `AUTH` gets `-NOAUTH`;
      - `AUTH` with a good password gets `+OK`, and with a bad one `-WRONGPASS`;
      - `HELLO 3` answered with a RESP3 map, and later replies in RESP3;
      - `HELLO 4` gets `-NOPROTO`;
      - `HELLO 3 AUTH user pass` logs in;
      - `SELECT` of an out-of-range database refused;
      - `PING` and `PING msg` in both protocols;
      - `QUIT` answered `+OK` then the connection closed;
      - an unknown command gets the ADR's error text;
      - anonymous use allowed only under `--allow-anonymous`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
