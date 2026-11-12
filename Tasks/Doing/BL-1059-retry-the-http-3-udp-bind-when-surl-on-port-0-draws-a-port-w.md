---
id: BL-1059
title: Retry the HTTP/3 UDP bind when surl on port 0 draws a port Windows excludes for UDP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1059 — Retry the HTTP/3 UDP bind when surl on port 0 draws a port Windows excludes for UDP

## Goal

`surl --http3 https://127.0.0.1:0/` always starts on Windows, even when the TCP port it draws has a UDP port in a range Windows excludes.

## Context

- Found by BL-604, 2026-11-11: with `SURL_EXECUTABLE` naming a win-x64 publish, 3 to 11 of the HTTP/3 conformance tests per full run exit 45, `Could not bind https://127.0.0.1:55843/: Permission denied`. `netsh int ipv4 show excludedportrange protocol=udp` lists 55750-55849 and 57930-58029 on Stewart's machine, which hold exactly the failing ports; the ranges differ per machine and boot.
- The HTTP/3 server binds UDP on the same number as the TCP listener (ADR-0082). With port 0 it takes the TCP port first and cannot change it afterwards.
- Fix: when the UDP bind of a port-0 listener is refused, close the TCP listener and draw another port, a bounded number of times; an explicit port still fails at once. Start with `Application/Surl.Composition.UnitLibrary/ComposedServing.cs`; widen `touches` if the retry belongs in `Surl.Networking`.

## Acceptance criteria

- [ ] A unit test with a fake listener seam that refuses UDP on the first two ports shows surl starting on the third.
- [ ] An explicit port whose UDP bind is refused still exits 45.
- [ ] BL-604's two full conformance runs against a win-x64 publish have no HTTP/3 failure.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-12: Backlog -> Doing.
