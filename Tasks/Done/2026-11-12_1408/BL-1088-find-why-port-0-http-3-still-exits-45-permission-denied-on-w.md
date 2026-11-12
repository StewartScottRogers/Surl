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
completed: 2026-11-12
---
# BL-1088 — Find why port-0 HTTP/3 still exits 45 Permission denied on Windows after BL-1059's retry

## Goal

A published win-x64 surl started with an HTTP/3 listener on port 0 never exits 45 `Could not bind https://127.0.0.1:<port>/: Permission denied` under a full conformance run.

## Context

- BL-1059 (commit a08b151b) made `SharedPortListenerPair` retry the UDP bind on PermissionDenied for port 0. BL-604's 2026-11-12 runs of the full Conformance suite against a fresh win-x64 publish still hit it: 5 failures in `CurlPortBesideUpstreamCurlOverHttp3Tests` (ports 54533, 54724, 54738, 54753, 54830).
- Find which bind path still throws (the QUIC listener's own bind, or a path that skips the retry) and whether the excluded range (`netsh int ipv4 show excludedportrange protocol=udp`) covers those ports.

## Acceptance criteria

- [x] Notes name the bind path that escapes the retry, with evidence.
- [x] A unit test fails before the fix and passes after.
- [x] Fast tests stay green.

## Notes

- Escaping path: not a missed bind path. `SharedPortListenerPair`'s retry did run, but only 10 times. Windows hands out consecutive ephemeral TCP ports when a port-0 listener is released and rebound (measured: 52282, 52283, ... 52293), and this machine's UDP excluded ranges (`netsh int ipv4 show excludedportrange protocol=udp`) hold adjacent 100-port blocks (54408-54907, 52329-53028). All five failing ports (54533, 54724, 54738, 54753, 54830) sit inside 54408-54907, so 10 consecutive ports never leave the block.
- Fix: `MaxBindAttempts` 10 -> 2048. Test `StartAsync_PortZeroAndTheUdpPortRefusedAcrossAWholeExcludedBlock_StartsOnThePortAfterIt` refuses 700 in a row (fails at 10, passes at 2048). Decided by Claude under Stewart's delegation; too small for an ADR (a constant).
- Fast tests: Networking 463 passed, 0 failed; `dotnet build` clean.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Port-0 HTTP/3 pair retries 2048 times, crossing Windows adjacent excluded UDP blocks
