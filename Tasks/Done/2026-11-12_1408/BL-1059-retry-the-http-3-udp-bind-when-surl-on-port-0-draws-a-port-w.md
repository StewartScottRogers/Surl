---
id: BL-1059
title: Retry the HTTP/3 UDP bind when surl on port 0 draws a port Windows excludes for UDP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-12
---
# BL-1059 — Retry the HTTP/3 UDP bind when surl on port 0 draws a port Windows excludes for UDP

## Goal

`surl --http3 https://127.0.0.1:0/` always starts on Windows, even when the TCP port it draws has a UDP port in a range Windows excludes.

## Context

- Found by BL-604, 2026-11-11: with `SURL_EXECUTABLE` naming a win-x64 publish, 3 to 11 of the HTTP/3 conformance tests per full run exit 45, `Could not bind https://127.0.0.1:55843/: Permission denied`. `netsh int ipv4 show excludedportrange protocol=udp` lists 55750-55849 and 57930-58029 on Stewart's machine, which hold exactly the failing ports; the ranges differ per machine and boot.
- The HTTP/3 server binds UDP on the same number as the TCP listener (ADR-0082). With port 0 it takes the TCP port first and cannot change it afterwards.
- Fix: when the UDP bind of a port-0 listener is refused, close the TCP listener and draw another port, a bounded number of times; an explicit port still fails at once. Start with `Application/Surl.Composition.UnitLibrary/ComposedServing.cs`; widen `touches` if the retry belongs in `Surl.Networking`.

## Acceptance criteria

- [x] A unit test with a fake listener seam that refuses UDP on the first two ports shows surl starting on the third.
- [x] An explicit port whose UDP bind is refused still exits 45.

## Notes

- Third criterion (BL-604 full conformance runs on a win-x64 publish) moved to a follow-up task; not runnable in this lane.

- Retry already lived in Foundation/Surl.Networking SharedPortListenerPair (AddressInUse only, 10 attempts), not in ComposedServing. Widened touches there (no Doing task overlaps) and made PermissionDenied (Windows excluded UDP ranges) retry for port 0 too. Explicit ports still fail at once (45). The reporter in Composition just wraps the factory, so no change there.

## Log

- 2026-11-11: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Port-0 HTTP/3 start retries a UDP bind Windows refuses (permission denied) like a taken port; explicit port still exits 45
