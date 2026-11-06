---
id: BL-903
title: Refuse reserved WebSocket close codes in the --fault reply check
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-793]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-903 — Refuse reserved WebSocket close codes in the --fault reply check

## Goal

`surl --fault 'error;scheme=ws;reply=1004'` (or any code 1016 to 2999) is refused when the option is read,
with `key reply does not fit the ws error shape`, as the WebSocket server will not send it.

## Context

- `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs` `IsWebSocketClose` accepts 1000 to 4999 except 1005,
  1006 and 1015, as ADR-0124 decision 4's reply table words it.
- ADR-0134 decision 4 (BL-793): the WebSocket server sends only codes RFC 6455 section 7.4 allows on the wire -
  1000 to 1014 less 1004 to 1006, and 3000 to 4999 (`Surl.WebSocket`'s `WebSocketCloseCodes.IsAllowedOnTheWire`)
  - and a reason of at most 123 UTF-8 bytes; anything else is noted and not applied. `Surl.Cli` may not
  reference `Surl.WebSocket`, so the rule is written in `FaultReplyShapes` itself.
- Amend ADR-0124 decision 4's `ws`, `wss` row to the same wording.

## Acceptance criteria

- [ ] `CommandLineParserFaultTests` has named cases refusing `ws` replies `1004`, `1016`, `2999` and a reason over
      123 UTF-8 bytes, and accepting `1000`, `1014`, `3000` and `4999 bye`.
- [ ] ADR-0124 decision 4's `ws`, `wss` row says the same as ADR-0134 decision 4.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Filed by BL-793 (2026-11-05).

## Log

- 2026-11-05: Created.
