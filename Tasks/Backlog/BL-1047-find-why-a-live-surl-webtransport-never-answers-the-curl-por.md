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
completed:
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

- [ ] A failing test in `Surl.Extended.Protocol.WebTransport.UnitTests` or `Surl.Http3.UnitTests` reproduces the defect, or Notes says why the cause is the Curl port client's and names its evidence from the packets.
- [ ] If surl's: the fix, with the unit test passing and the 100% gates held.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-11: Created.
