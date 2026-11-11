---
id: BL-1047
title: Find why a live surl webtransport never answers the Curl port client's unidirectional stream
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1047 — Find why a live surl webtransport never answers the Curl port client's unidirectional stream

## Goal

Say whether surl or the Curl port client is at fault when `curl -k -d "hello uni" "webtransport://127.0.0.1:<port>/?wt=uni"` hangs against `surl --allow-anonymous --self-signed webtransport://127.0.0.1:0/`, and fix surl if it is surl's.

## Context

- Found by BL-462. Against the same live surl the bidirectional and datagram exchanges pass; the unidirectional one is stopped at the harness's 30 s timeout with empty output. surl's `-v` log shows `WebTransport session 0 on / was accepted.` and no refusal note.
- Judge by WTH3 4.2 and ADR-0117 decision 3 (`WebTransport:Surl-3-2`); the port's client is only a live client (ADR-0083), so its output is never the verdict. Read the packets surl sends (`-v`) to see whether a server unidirectional stream with `0x54` and session ID 0 leaves surl and carries the echoed bytes and a FIN.
- Likely places: `WebTransportSession.OpenAnswerAsync`, `Http3ExtendedConnectSession.OpenUnidirectionalStreamAsync`, the QUIC server's unidirectional stream open and flush.
- The test to make pass: `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Send_OnAUnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamByteForByte` (BL-462).

## Acceptance criteria

- [x] A failing test in `Surl.Extended.Protocol.WebTransport.UnitTests` or `Surl.Http3.UnitTests` reproduces the defect, or Notes says why the cause is the Curl port client's and names its evidence from the packets.
- [x] If surl's: the fix, with the unit test passing and the 100% gates held.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

Cause was surl's, not the port client's. `WebTransportProtocolServer` left `QuicServerConnectionOptions.InitialMaxStreamsUni` at its default 3, which the client's HTTP/3 control and two QPACK streams use up, so its WebTransport unidirectional stream (the fourth, stream 14) was past the QUIC stream limit and never reached the session (a debug trace showed `AcceptStreamAsync` never returned it). The in-memory test sent that stream on the QPACK encoder stream ID, so it missed this. Fix: `InitialMaxStreamsUni` = 3 + `WebTransportFlowControlSettings.Default.InitialMaxStreams` (103). Test: `WebTransportProtocolServerTests.ServeAsync_ClientsFourthUnidirectionalStream_IsTakenAndAnswered` (failed before, passes after). Live: `curl -k -d "hello uni" webtransport://127.0.0.1:<port>/?wt=uni` against `surl --allow-anonymous --self-signed` now prints `hello uni`, exit 0. No ADR needed (a transport parameter value, not a behaviour choice).


## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl now allows the client's WebTransport unidirectional streams (QUIC initial_max_streams_uni 103, was 3); live port client wt=uni echoes
