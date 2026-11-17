---
id: BL-1336
title: Make the http/2 gateway answer stream 1 after an h2c upgrade so case 2407 passes on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-1336 — Make the http/2 gateway answer stream 1 after an h2c upgrade so case 2407 passes on Linux

## Goal

Case 2407 (`lib2405`, `curl_multi_waitfds` over HTTP/2 via the `http/2` gateway half) passes on `linux-x64`.

## Context

BL-1335 read the `test2407` block of `upstream-test-case-timeout-output.linux-x64.txt` from [CI run 44009482486](https://github.com/StewartScottRogers/Surl/actions/runs/44009482486). The client opens two connections to the gateway's plaintext port, each sends `GET /2407` with `Upgrade: h2c`, each gets `101 Switching Protocols`, curl logs `Received 101, Switching to HTTP/2` and `Copied HTTP/2 data in stream buffer ... len=21` (the server's SETTINGS), and then nothing: stdout is 0 bytes and the runner stops it after 60 s. So the response to stream 1 never reaches curl on the upgraded connections. Start at `Http2GatewayCleartextUpgrade.Takeover` -> `Http2ConnectionServer.ServeUpgradedAsync` -> `Http2GatewayStreamForwarder`: find why HEADERS/DATA for stream 1 are not written when two upgrades run side by side (case 2400 passes with one connection). The case is tagged `flaky` upstream, so confirm it is deterministic here by reproducing in a unit test with two concurrent upgrades before changing code.

## Acceptance criteria

- [ ] A unit test in `Surl.Protocol.Http.UnitTests` drives two concurrent `Upgrade: h2c` connections to the gateway and fails before the fix.
- [ ] After the fix both connections receive HEADERS and DATA on stream 1.
- [ ] The next Linux CI run lists `test2407` in `upstream-test-cases-passing.linux-x64.txt`, and `Upstream-Test-Case-Pass-Rate.md` says so.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
