---
id: BL-340
title: Amend ADR-0071 decision 6 to allow close codes 1012 to 1014 as the code does
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-340 — Amend ADR-0071 decision 6 to allow close codes 1012 to 1014 as the code does

## Goal

ADR-0071 decision 6 states the close codes surl accepts from a client exactly as
`WebSocketCloseCodes.IsAllowedOnTheWire` accepts them.

## Context

- Found by BL-305. ADR-0071's decision 6 table (row for `CLOSE` with "any other code") lists
  "1012 to 2999 unregistered" as answered 1002.
- `Surl.Protocol.Ws.UnitLibrary/WebSocketCloseCodes.cs` allows 1000-1003, 1007-1014 and
  3000-4999: BL-288 took 1012-1014 from the IANA WebSocket Close Code Number Registry, which
  assigned them from the range RFC 6455 section 7.4.2 reserves. So surl echoes 1012-1014.
- Decision (Claude, BL-305): amend the ADR, not the code - the IANA registry is the protocol's
  current specification, and the Product overview and Glossary (BL-305) already state the
  code's set.

## Acceptance criteria

- [ ] ADR-0071 carries an amendment, marked "Decided by Claude under Stewart's delegation",
      saying a client `CLOSE` with 1012, 1013 or 1014 is echoed and 1015 to 2999 is answered 1002,
      and why (the IANA registry).
- [ ] The decision 6 table no longer says 1012 to 1014 are answered 1002.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
