---
id: BL-456
title: Run the Curl port's socketio client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-455, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/SocketIo.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-456 — Run the Curl port's socketio client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl socketio://` and `socketios://`: join a
namespace, emit an event and receive its echo, and a refused `auth`. Each assertion comes from the Engine.IO and
Socket.IO specifications, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help socketio` and its cases in `Curl.Conformance.Extended.UnitTests/SocketIo/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the exchanges above, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/SocketIo.md`: every row is covered by a named test or marked `n/a` with a
      reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- Added `PinnedCurlPortClientTalksToSurlOverSocketIoTests` (4 Integration tests, all passing on Windows with the pinned client): main-namespace echo, socketios echo (`--self-signed`, client `-k`), refused login without `--allow-anonymous`, and a refused unserved namespace.
- Decision: listen URLs cannot carry a path (exit 3), so the CLI serves only `/`; the "named namespace" case is therefore the refusal (`SocketIo:Surl-4-1`). Clients never get a non-zero exit asserted beyond `!= 0`; the port's output is not an expected value (ADR-0003).
- `SocketIo.md` gains a "Live client" section mapping rows to the live tests. No port defect observed.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Pinned Curl port socketio client echoes events, joins over TLS and is refused on bad login/namespace against a live surl (4 Integration tests)
