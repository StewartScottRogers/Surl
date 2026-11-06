---
id: BL-459
title: Establish WebTransport sessions and echo streams and datagrams over HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-458, BL-411, BL-748]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary/Surl.Http3.UnitLibrary.csproj, Protocols/Http/Surl.Http3.UnitLibrary/CLAUDE.md, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-459 — Establish WebTransport sessions and echo streams and datagrams over HTTP/3

## Goal

The WebTransport server registers `:protocol webtransport` with the HTTP/3 library and answers a valid extended
CONNECT with `200`. It then serves the session's bidirectional streams, unidirectional streams and datagrams as
BL-457's ADR decides, and ends the session with `WT_CLOSE_SESSION` or a drain.

## Context

- Decision: BL-457's ADR, decisions 1, 2, 3 and 6.
- The HTTP/3 library with extended CONNECT and datagrams: BL-411 (add its reference).
- WTH3: "Session Establishment", "WebTransport Features" (streams and datagrams), and "Session Termination".
  An unknown session ID on a stream or datagram is handled as WTH3 says (buffered for a while, or reset with
  `WT_BUFFERED_STREAM_REJECTED`).

## Acceptance criteria

- [x] Named tests, over the in-memory QUIC test connection, show:
      - a CONNECT to an accepted path answered `200`;
      - one to another path refused as the ADR says;
      - a bidirectional stream's bytes echoed;
      - a unidirectional stream answered;
      - a datagram echoed with its quarter stream ID;
      - `WT_CLOSE_SESSION` from the client closing every stream of the session;
      - shutdown sending `WT_DRAIN_SESSION` and then `GOAWAY`;
      - a stream naming an unknown session handled as WTH3 says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (ADR-0117 decisions 0 to 3 and 6): `WebTransportApplicationProtocol` (the public
`IQuicApplicationProtocol`), `WebTransportConnectHandler`, `WebTransportConnectJudgement`,
`WebTransportRequestStreamHandler`, `WebTransportSession` (the echo, closing and draining),
`WebTransportSessionDirectory` (streams naming no open session) and `WebTransportHttp3Codes`.
145 tests in `Surl.Extended.Protocol.WebTransport.UnitTests`, the library at 100% line and
branch coverage (Microsoft code coverage, cobertura), green five runs in a row.

The acceptance criteria's named tests:
- 200: `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectToTheAcceptedPath_IsAnswered200AndTheStreamStaysOpen`
- another path: `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectToAnotherPath_IsAnswered405WithAllowConnectAndFin`
- bidirectional echo: `WebTransportSessionTests.ServeStreamAsync_BidirectionalStream_EchoesItsBytesThenFin`
- unidirectional: `WebTransportSessionTests.ServeStreamAsync_UnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamOfTheSession`
- datagram: `WebTransportSessionTests.ServeAsync_HttpDatagram_IsEchoedWithItsQuarterStreamId`
- `WT_CLOSE_SESSION`: `WebTransportSessionTests.ServeAsync_ClientClosesTheSession_EndsEveryStreamOfTheSessionWithSessionGoneThenFin`
- shutdown: `WebTransportSessionTests.ServeAsync_Shutdown_SendsDrainSessionAndCloseSessionThenGoAway`
- unknown session: `WebTransportSessionDirectoryTests` (held then served, timed out, refused CONNECT, ordinary request, ended session, past 16).

Touches widened (no task in Doing named them): `Surl.Http3.UnitLibrary.csproj` gains
`InternalsVisibleTo` for the WebTransport tests, which compile in `Surl.Http3.UnitTests`' in-memory
HTTP/3 client by link (it uses `Http3FrameType` and `Http3FrameWriter`), as `Surl.Protocol.Http.UnitTests`
does; `Surl.Http3.UnitLibrary/CLAUDE.md` says so; and `Documentation/Conformance/Extended/WebTransport.md`,
whose BL-459 rows now name their tests.

Choices (defaults taken, within ADR-0117):
- `SETTINGS_WT_INITIAL_*` are not sent yet: offering flow control the server does not apply would be
  false. BL-460 sends them with flow control (its matrix row WTH3-5.5-1 already names it).
- The accepted path is a constructor argument: `ListenUrl` carries no path; BL-461 passes it.
- A client unidirectional stream the server has no stream to answer on (the client's `MAX_STREAMS`)
  is stopped `H3_REQUEST_REJECTED`: nothing of it was processed.
- A client reset is mirrored with `RESET_STREAM_AT` keeping every byte already echoed (and the header).
- Any capsule that breaks its layout resets the CONNECT stream `H3_MESSAGE_ERROR`; the flow-control
  capsules are ignored while flow control is off.

Gaps, because `Surl.Http3` does not offer a handler the means yet (filed as BL-894 for `Surl.Http3`,
then BL-895 for this server):
- unmet client SETTINGS reset the CONNECT stream `WT_REQUIREMENTS_NOT_MET` rather than closing the
  connection (`Http3ConnectionException` is internal), and the client's `max_datagram_frame_size` is
  not checked;
- `:scheme` is not in `Http3RequestHead`, so the `400` for a non-https scheme is not answered;
- `Surl.Http3` drops datagrams for a session whose CONNECT has not arrived, so none are held;
- at shutdown `Surl.Http3` cancels the handler and sends `GOAWAY` after it returns, so the session
  sends `WT_DRAIN_SESSION`, `WT_CLOSE_SESSION` code 0 and FIN together before `GOAWAY`, with no grace.
- A client `STOP_SENDING` that lands between the echo's check and its write could make that write
  throw; the window is one lock acquisition wide, and BL-895 can close it with the same seam.
## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The WebTransport server answers webtransport-h3 CONNECTs (200, 403, 405), echoes bidirectional and unidirectional streams, datagrams and DATAGRAM capsules, ends sessions on WT_CLOSE_SESSION, FIN or reset, drains on shutdown before GOAWAY, and holds or refuses streams naming no open session
