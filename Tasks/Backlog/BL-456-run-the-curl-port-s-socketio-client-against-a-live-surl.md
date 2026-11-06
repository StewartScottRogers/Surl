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
completed:
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

- [ ] The test class exists with the exchanges above, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/SocketIo.md`: every row is covered by a named test or marked `n/a` with a
      reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
