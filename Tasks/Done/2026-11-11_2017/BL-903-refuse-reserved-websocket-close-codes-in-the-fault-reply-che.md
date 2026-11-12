---
id: BL-903
title: Refuse reserved WebSocket close codes in the --fault reply check
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-793]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md, Documentation/Planning/Decisions/ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
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

- [x] `CommandLineParserFaultTests` has named cases refusing `ws` replies `1004`, `1016`, `2999` and a reason over
      123 UTF-8 bytes, and accepting `1000`, `1014`, `3000` and `4999 bye`.
- [x] ADR-0124 decision 4's `ws`, `wss` row says the same as ADR-0134 decision 4.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Filed by BL-793 (2026-11-05).

- 2026-11-11 (lane 1): `FaultReplyShapes.IsWebSocketClose` now accepts only 1000-1003, 1007-1014 and 3000-4999,
  with an optional non-empty reason of at most 123 UTF-8 bytes (`Encoding.UTF8.GetByteCount`), the rule
  `WebSocketCloseCodes.IsAllowedOnTheWire` applies, written here since `Surl.Cli` may not reference `Surl.WebSocket`.
- Added both ADR files to `touches` (no task in Doing names them): ADR-0124's `ws`, `wss` row now repeats
  ADR-0134 decision 4, and ADR-0134 decision 4's "that ADR-0124's reply table lets through" became "let through
  until BL-903" so it stays true.
- Tests also cover 1005, 1015 (wss), 5000, 1003 and 1007 at the reserved range's edges, and a reason of exactly
  123 UTF-8 bytes (61 two-byte characters and one ASCII one).

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl --fault refuses ws/wss replies with reserved close codes (1004, 1016-2999) or a reason over 123 UTF-8 bytes
