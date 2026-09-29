---
id: BL-077
title: Record how the TELNET server answers in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-035]
touches: [Documentation/Planning/Decisions/ADR-0016-how-the-telnet-server-answers.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-077 — Record how the TELNET server answers in an ADR

## Goal

An accepted ADR, marked "Decided by Claude under Stewart's delegation", records the
decisions BL-035 made for `TelnetProtocolServer`, and the Decisions index lists it.

## Context

- BL-035 decided the TELNET server's behaviour and stated it in the XML doc of
  `Surl.Protocol.Telnet.UnitLibrary/TelnetProtocolServer.cs`, with the measurements in
  `Surl.Protocol.Telnet.UnitTests/Fixtures/README.md` and the reasons under BL-035's
  `Notes`. It could not write the ADR itself: BL-027 held
  `Documentation/Planning/Decisions` in `Doing` at the time.
- The ADR follows the form of ADR-0011 (DICT) and ADR-0014 (MQTT): Context with the
  bytes measured from pinned upstream curl 8.21.0, Decision as numbered points, and
  Consequences. Take the number the index gives next; if another lane took it first,
  renumber this one.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-00NN-how-the-telnet-server-answers.md` exists,
      is Accepted, is marked "Decided by Claude under Stewart's delegation", and states: the
      server speaks first (curl 8.21.0 negotiates only after the server does and never
      closes on the end of its standard input); the opening offers and requests; which
      options are agreed and refused and the RFC 1143 no-reply rule; `SEND` for
      TERMINAL-TYPE, X-DISPLAY-LOCATION and NEW-ENVIRON and one log note per answer (and
      why unasked subnegotiations are not reported); the line echo, `quit`, `bye`, and
      waiting for unanswered `SEND`s; the line and subnegotiation limits.
- [ ] Every statement in it is true of `TelnetProtocolServer` as the code stands.
- [ ] The Decisions `README.md` index lists it.

## Notes

## Log

- 2026-09-28: Created.
