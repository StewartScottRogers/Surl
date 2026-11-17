---
id: BL-1336
title: Make the http/2 gateway answer stream 1 after an h2c upgrade so case 2407 passes on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1337]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-17
---
# BL-1336 — Make the http/2 gateway answer stream 1 after an h2c upgrade so case 2407 passes on Linux

## Goal

Case 2407 (`lib2405`, `curl_multi_waitfds` over HTTP/2 via the `http/2` gateway half) passes on `linux-x64`.

## Context

BL-1335 read the `test2407` block of `upstream-test-case-timeout-output.linux-x64.txt` from [CI run 44009482486](https://github.com/StewartScottRogers/Surl/actions/runs/44009482486). The client opens two connections to the gateway's plaintext port, each sends `GET /2407` with `Upgrade: h2c`, each gets `101 Switching Protocols`, curl logs `Received 101, Switching to HTTP/2` and `Copied HTTP/2 data in stream buffer ... len=21` (the server's SETTINGS), and then nothing: stdout is 0 bytes and the runner stops it after 60 s. So the response to stream 1 never reaches curl on the upgraded connections. Start at `Http2GatewayCleartextUpgrade.Takeover` -> `Http2ConnectionServer.ServeUpgradedAsync` -> `Http2GatewayStreamForwarder`: find why HEADERS/DATA for stream 1 are not written when two upgrades run side by side (case 2400 passes with one connection). The case is tagged `flaky` upstream, so confirm it is deterministic here by reproducing in a unit test with two concurrent upgrades before changing code.

## Acceptance criteria

- [x] A unit test in `Surl.Protocol.Http.UnitTests` reproduces the cause (a backend reply with bare-LF lines on a connection left open; it would never complete before the fix): `Test2407_BackendReplyWithBareLineFeedsAndConnectionLeftOpen_StillAnswersStream1` and the LF rows of `NghttpxGatewayReplyFramingReaderTests`. (Revised: the cause was not the two concurrent upgrades; see Notes.)
- [x] After the fix the upgraded connection receives HEADERS and DATA on stream 1 (that test).
- [x] The Linux CI confirmation and the pass-rate document moved to BL-1345, which needs a CI run that includes this commit.

## Notes

2026-11-17: Read the Linux trace of run 44059981299. Curl exchanged SETTINGS and acks on both upgraded connections, then got no stream 1 reply; the gateway log shows a single backend exchange (3). Cause: case 2407's `<data>` has no `crlf="headers"`, so the reply head is bare-LF, and the case's `http` half keeps the connection open (no `swsclose`). `NghttpxGatewayReplyFramingReader` and `NghttpxGatewayResponseTranslator` only recognised `CRLF CRLF`, so the gateway never saw a head end, never reached `Whole`, and waited for a close that never came. nghttpx's parser accepts a bare LF. Fix: `NghttpxGatewayReplyHead.TryRead` finds the head end on LF or CRLF lines; both classes use it. Decision (small, within the delegated scope): leading blank lines before the status line are skipped. The "fails before the fix" check was not run by reverting the code (not permitted in this session); the LF rows fail on the old CRLF-only search by construction.

2026-11-16: Read `Http2GatewayCleartextUpgrade`, `Http2ConnectionServer.ServeUpgradedAsync`, `Http2GatewayStreamForwarder`, `NghttpxGatewayBackend` and `Http2ConnectionPreface`: no state is shared between connections and nothing there can stall stream 1. Added `Test2407_TwoUpgradedConnectionsServedAtOnce_EachGetsHeadersAndDataOnStream1`; it PASSES, so the failure is not reproducible with in-memory connections (first criterion not met: no test fails before a fix). The CI timeout output has no HTTP/2 trace, so real-socket timing (curl's preface, the case's `http` half) is unobserved. Filed BL-1337 to capture curl's HTTP/2 trace and the gateway's exchange log on Linux; this task waits on it. The regression test stays.

2026-11-16: BL-1337 landed on this lane but is not yet in a CI run, so the Linux HTTP/2 trace and exchange log do not exist yet. Nothing more to learn locally. Resume after a Linux CI run on master includes BL-1337 and its upstream-test-case-timeout-output.linux-x64.txt has the test2407 trace.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Cause not reproducible in memory; waits on BL-1337 (curl HTTP/2 trace and gateway log in the timeout output)
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Deferred. Needs the Linux CI timeout output from a run that includes BL-1337; not observable locally
- 2026-11-17: Deferred -> Backlog. BL-1337's trace is on master: read case 2407's curl HTTP/2 trace and gateway log from PR #103's 'Upstream test cases (ubuntu-latest)' run 44059981299 job 133959035772 (gh run view 44059981299 --job 133959035772 --log), or its upstream-test-case-results-linux-x64 artifact.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. The http/2 gateway accepts bare-LF backend reply heads, so case 2407's stream 1 is answered; Linux CI confirmation is BL-1345
