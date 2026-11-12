---
id: BL-1088
title: Find why port-0 HTTP/3 still exits 45 Permission denied on Windows after BL-1059's retry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1088 — Find why port-0 HTTP/3 still exits 45 Permission denied on Windows after BL-1059's retry

## Goal

A published win-x64 surl started with an HTTP/3 listener on port 0 never exits 45 `Could not bind https://127.0.0.1:<port>/: Permission denied` under a full conformance run.

## Context

- BL-1059 (commit a08b151b) made `SharedPortListenerPair` retry the UDP bind on PermissionDenied for port 0. BL-604's 2026-11-12 runs of the full Conformance suite against a fresh win-x64 publish still hit it: 5 failures in `CurlPortBesideUpstreamCurlOverHttp3Tests` (ports 54533, 54724, 54738, 54753, 54830).
- Find which bind path still throws (the QUIC listener's own bind, or a path that skips the retry) and whether the excluded range (`netsh int ipv4 show excludedportrange protocol=udp`) covers those ports.

## Acceptance criteria

- [ ] Notes name the bind path that escapes the retry, with evidence.
- [ ] A unit test fails before the fix and passes after.
- [ ] Fast tests stay green.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
