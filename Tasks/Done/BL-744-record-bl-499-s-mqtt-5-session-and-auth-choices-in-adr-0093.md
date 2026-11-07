---
id: BL-744
title: Record BL-499's MQTT 5 session and AUTH choices in ADR-0093
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-499]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-744 — Record BL-499's MQTT 5 session and AUTH choices in ADR-0093

## Goal

ADR-0093 states the five choices BL-499 made inside its decisions 2 and 3, so the ADR says what
the MQTT 5 broker does.

## Context

- BL-499 could not edit `Documentation/Planning/Decisions`: BL-435 held it in `Doing` at the
  time. BL-499's Notes list the choices, and
  `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/CLAUDE.md` describes them.
- The choices, each decided by Claude under Stewart's delegation:
  1. A kept session is resumed only for the account that left it: Clean Start 0 with another
     account's login ends the old session and starts a new one (Session Present 0).
  2. A session another connection still holds is never resumed; until BL-679 adds the takeover,
     the new connection starts a fresh session and the old one ends with its connection.
  3. The waiting-message bounds (1000 messages, 16 MiB of payload) apply connected or not, and
     the kept-session bound (1000) counts sessions with no connection; a new session with an
     expiry past it is CONNACK `0x97`.
  4. Absent Authentication Data on a SCRAM CONNECT or AUTH `0x19` is an empty client-first,
     which SCRAM refuses (`0x87`); SCRAM's server-final, issued by the policy as a second
     challenge, is answered with the empty response and carried in the CONNACK or AUTH `0x00`.
  5. Re-authentication that authenticates another account than the CONNECT's is DISCONNECT
     `0x87`.

## Acceptance criteria

- [x] ADR-0093 has an "Amended" note dated when it lands, naming BL-499, and decisions 2 and 3
      state choices 1 to 5 above.
- [x] The ADR index row for 0093 in `Documentation/Planning/Decisions/README.md` mentions the
      amendment.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0093 decisions 2 and 3 state BL-499's five choices (choice 2 as BL-679's takeover now does it), with an Amended note and the index row updated
