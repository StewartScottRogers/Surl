# WebTransport requirement matrix

The server-side requirements `Surl.Extended.Protocol.WebTransport` answers, for the `webtransport`
scheme (ADR-0083 decision 9). Written from WebTransport over HTTP/3, draft-ietf-webtrans-http3-16
(cited as WTH3), RFC 9220, RFC 9297 and RFC 9221, read 2026-11-05, the specifications
[ADR-0117](../../Planning/Decisions/ADR-0117-how-the-webtransport-server-answers-webtransport.md)
decides by, never from the Curl port's client matrix. Each row binds the server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test here.

IDs are `WebTransport:WTH3-<section>-<n>` for the draft, `WebTransport:RFC<number>-<section>-<n>`
for the RFCs, and `WebTransport:Surl-<decision>-<n>` for a rule ADR-0117 adds where the
specifications leave the server a choice. Client-only requirements (waiting for the server's
`SETTINGS`, not following redirects, closing with `WT_ALPN_ERROR`) and intermediary requirements
(WTH3 5.6.1, 4.4's pass-through) are not rows.

## Connection (WTH3 3.1, 4, 7.1)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| WebTransport:WTH3-3.1-1 | MUST | The server's `SETTINGS` carry `SETTINGS_WT_ENABLED` (`0x2c7cf000`) = 1. | `WebTransportApplicationProtocolTests.ServeAsync_Connection_SendsSettingsWithExtendedConnectDatagramsWtEnabledAndTheInitialFlowControlLimits` |
| WebTransport:WTH3-3.1-2 | MUST | The server's `SETTINGS` carry `SETTINGS_ENABLE_CONNECT_PROTOCOL` = 1 and `SETTINGS_H3_DATAGRAM` = 1. | `WebTransportApplicationProtocolTests.ServeAsync_Connection_SendsSettingsWithExtendedConnectDatagramsWtEnabledAndTheInitialFlowControlLimits` |
| WebTransport:WTH3-3.1-3 | MUST | The server sends the `max_datagram_frame_size` transport parameter above 0 (65535). | BL-461 (the composing server sets `WebTransportApplicationProtocol.MaxDatagramFrameSize`, 65535) |
| WebTransport:WTH3-3.1-4 | MUST | The server sends an empty `reset_stream_at` transport parameter. | BL-461 (the composing server sets `ResetStreamAt`) |
| WebTransport:WTH3-3.1-5 | MUST NOT | The server judges no WebTransport CONNECT before the client's `SETTINGS` arrive; it waits for them within `--head-timeout`, then resets the CONNECT `H3_REQUEST_INCOMPLETE` (ADR-0117 decisions 0 and 8). | `WebTransportConnectHandlerTests.ServeSessionAsync_ClientSettingsNeverArrive_ResetsTheConnectWithRequestIncompleteAfterTheHeadTimeout`, `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectBeforeTheClientSettings_IsAnsweredOnceTheyArrive` |
| WebTransport:WTH3-3.1-6 | MUST | A client asking for WebTransport without `SETTINGS_H3_DATAGRAM` = 1, `SETTINGS_WT_ENABLED` = 1 or a non-zero `max_datagram_frame_size` has its connection closed `WT_REQUIREMENTS_NOT_MET` (ADR-0117 decision 0). | `WebTransportConnectHandlerTests.ServeSessionAsync_ClientSettingsLackARequirement_ClosesTheConnectionWithRequirementsNotMet` (the two settings; `max_datagram_frame_size` is not checked by the handler, which sees only the HTTP/3 SETTINGS) |
| WebTransport:WTH3-4-1 | MUST | A stream header or datagram whose session ID is not a client-initiated bidirectional stream ID closes the connection `H3_ID_ERROR`. | `Surl.Http3` (BL-411): `Http3ExtendedConnectTests` |
| WebTransport:WTH3-4.3-1 | MUST | Signal `0x41` is read only as the first bytes of a client bidirectional stream; elsewhere it is `H3_FRAME_ERROR`. | n/a: `Surl.Http3` reads the signal and answers a misplaced one before the WebTransport server sees the stream (BL-411, ADR-0086 decision 3) |
| WebTransport:WTH3-3.2-1 | MUST NOT | Accepting 0-RTT, the server does not reduce the `SETTINGS_WT_INITIAL_*` it sent before. | n/a: the values are constants (ADR-0117 decision 0), and `Surl.Quic` accepts no 0-RTT (ADR-0082) |

## Session establishment (WTH3 3.2, 3.3; RFC 9220)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| WebTransport:WTH3-3.2-2 | MUST | An extended CONNECT with `:protocol webtransport-h3`, `:scheme https`, `:authority` and `:path` on an accepted path is answered `:status 200` and establishes a session. | `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectToTheAcceptedPath_IsAnswered200AndTheStreamStaysOpen`, `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectBelowTheAcceptedPathFromTheOwnOrigin_IsAnswered200` |
| WebTransport:RFC9220-3-1 | MUST | An extended CONNECT missing `:protocol`, `:scheme`, `:authority` or `:path` is malformed: stream error `H3_MESSAGE_ERROR`. | n/a: `Surl.Http3` answers it (BL-411, ADR-0086 decision 3) |
| WebTransport:WTH3-3.2-3 | MUST | A WebTransport CONNECT whose `:scheme` is not `https` is refused `:status 400` (ADR-0117 decision 1). | `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectWithAnHttpScheme_IsAnswered400AndFin`, `WebTransportConnectJudgementTests.IsSecureScheme_Scheme_IsAcceptedOnlyWhenItIsHttps` |
| WebTransport:WTH3-3.2-4 | SHOULD | A CONNECT to a path outside the listen URL's path is answered `:status 405`, `allow: CONNECT`, judged after authentication. | `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectToAnotherPath_IsAnswered405WithAllowConnectAndFin`, `WebTransportConnectJudgementTests.IsAcceptedPath_PathAgainstTheListenPath_IsAcceptedAtOrBelowItAtASlash`; after authentication: `WebTransportConnectAuthenticationTests.RefuseAsync_ConnectWithoutALoginToAnUnservedPath_IsAnswered401BeforeThePathIsJudged` |
| WebTransport:Surl-1-1 | MUST | A request that is not an extended CONNECT is answered `:status 405`, `allow: CONNECT` (ADR-0117 decision 1). | `WebTransportRequestStreamHandlerTests.ServeRequestStreamAsync_Get_IsAnswered405WithAllowConnectAndFin` |
| WebTransport:WTH3-3.2-5 | MUST | A present `origin` is verified: the server's own origin is accepted, `null` and any other refused `:status 403`; an absent `origin` is accepted. | `WebTransportConnectJudgementTests.IsOwnOrigin_OneOrigin_IsAcceptedOnlyWhenItIsHttpsAndTheAuthority`, `WebTransportConnectJudgementTests.IsOwnOrigin_NoOrigin_IsAccepted`, `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectFromAnotherOrigin_IsAnswered403AndFin` |
| WebTransport:WTH3-3.2-6 | MUST NOT | The server processes no capsule on the CONNECT stream before it has sent the `200`. | `WebTransportConnectHandler.ServeSessionAsync` writes the `200` before `WebTransportSession` reads a capsule; `WebTransportSessionTests.ServeAsync_DatagramCapsule_IsEchoedAsADatagramCapsule` |
| WebTransport:WTH3-3.3-1 | MAY | The server sends no `WT-Protocol`, accepts a CONNECT with or without `WT-Available-Protocols`, and ignores a malformed one (ADR-0117 decision 2). | `WebTransportConnectHandlerTests.ServeSessionAsync_ConnectToTheAcceptedPath_IsAnswered200AndTheStreamStaysOpen` (the `200` carries no other field) |

## Streams and datagrams (WTH3 4.2 to 4.6; RFC 9297; RFC 9221)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| WebTransport:WTH3-4.2-1 | MUST | A unidirectional stream header is type `0x54` then the session ID as a QUIC variable-length integer; the server writes the same header on its own unidirectional streams. | `WebTransportStreamHeaderTests.Read_UnidirectionalHeader_ReadsTheSessionIdOfEachLength`, `WebTransportStreamHeaderTests.Encode_Header_WritesKindThenSessionIdInShortestEncoding` (codec); `WebTransportSessionTests.ServeStreamAsync_UnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamOfTheSession` (streams) |
| WebTransport:WTH3-4.3-2 | MUST | A bidirectional stream header is signal `0x41` then the session ID. | `WebTransportStreamHeaderTests.Read_BidirectionalHeader_ReadsTheSessionIdOfEachLength`, `WebTransportStreamHeaderTests.Read_StreamStartingWithNeitherSignal_IsMalformed` |
| WebTransport:Surl-3-1 | MUST | Each client bidirectional stream's bytes are echoed on it in order, and its FIN answered with FIN (ADR-0117 decision 3). | `WebTransportSessionTests.ServeStreamAsync_BidirectionalStream_EchoesItsBytesThenFin` |
| WebTransport:Surl-3-2 | MUST | Each client unidirectional stream is answered by one server unidirectional stream carrying the same bytes, finished when the client's finishes. | `WebTransportSessionTests.ServeStreamAsync_UnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamOfTheSession`, `WebTransportSessionTests.ServeStreamAsync_ClientAllowsNoServerStream_StopsTheClientStreamWithRequestRejected` |
| WebTransport:WTH3-4.5-1 | MUST | A datagram's payload follows the quarter stream ID of the session's CONNECT stream; each is echoed with the same quarter stream ID and payload. | `WebTransportSessionTests.ServeAsync_HttpDatagram_IsEchoedWithItsQuarterStreamId` |
| WebTransport:RFC9297-3.5-1 | MUST | A `DATAGRAM` capsule on the CONNECT stream is processed as a datagram; the server echoes it as a `DATAGRAM` capsule. | `WebTransportSessionTests.ServeAsync_DatagramCapsule_IsEchoedAsADatagramCapsule` |
| WebTransport:RFC9221-5-1 | MAY | A datagram the client's `max_datagram_frame_size` cannot carry is dropped, never sent. | `WebTransportSessionTests.ServeAsync_DatagramLargerThanTheClientTakes_DropsTheEcho`, `WebTransportSessionTests.ServeAsync_ClientTakesNoDatagrams_DropsTheEcho` |
| WebTransport:WTH3-4.4-1 | MUST | The server resets a WebTransport data stream with `RESET_STREAM_AT`, Reliable Size at least the header it wrote. | `WebTransportSessionTests.ServeAsync_ClientClosesTheSession_EndsEveryStreamOfTheSessionWithSessionGoneThenFin` (`ResetAtAsync` with the header length; `RESET_STREAM` when `reset_stream_at` is not agreed) |
| WebTransport:WTH3-4.4-2 | MUST | Application error codes are mapped into and out of the `WT_APPLICATION_ERROR` range by section 4.4's formula, skipping `0x1f * N + 0x21`. | `WebTransportApplicationErrorCodeTests.ToHttp3ErrorCode_ApplicationCode_IsTheDraftsFormula`, `WebTransportApplicationErrorCodeTests.TryFromHttp3ErrorCode_CodeOutsideTheRangeOrReserved_ReturnsFalse` |
| WebTransport:Surl-3-3 | MUST | A client reset or `STOP_SENDING` on an echoed stream is mirrored on the server's half with the same code (ADR-0117 decision 3). | `WebTransportSessionTests.ServeStreamAsync_ClientResetsItsHalf_ResetsTheEchoWithTheSameCode`, `WebTransportSessionTests.ServeStreamAsync_ClientStopsTheEchoThenSendsMore_StopsTheClientHalfWithTheSameCode`, `WebTransportSessionTests.ServeStreamAsync_ClientStopsTheEchoThenFinishes_StopsTheClientHalfInsteadOfFin` |
| WebTransport:WTH3-4.6-1 | SHOULD | Streams and datagrams for a session not yet established are held until it is, then served. | `WebTransportSessionDirectoryTests.ServeStreamAsync_StreamBeforeItsConnect_IsHeldAndEchoedOnceTheSessionIsAccepted` (streams); `WebTransportSessionTests.ServeAsync_DatagramBeforeItsConnect_IsEchoedOnceTheSessionIsAccepted` (datagrams) |
| WebTransport:WTH3-4.6-2 | MUST | Held streams are capped at 16 and held datagrams at 64 a connection, each for at most `--head-timeout`; a stream past the cap, timed out or for a refused session is reset `WT_BUFFERED_STREAM_REJECTED`, and such a datagram dropped. | `WebTransportSessionDirectoryTests.ServeStreamAsync_SeventeenthStreamWaiting_IsResetAtOnce`, `WebTransportSessionDirectoryTests.ServeStreamAsync_UnidirectionalStreamWhoseSessionNeverComes_IsStoppedWithBufferedStreamRejectedAfterTheHeadTimeout`, `WebTransportSessionDirectoryTests.ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected` (streams); `Http3ExtendedConnectTests.ServeAsync_DatagramPastTheSixtyFourHeld_IsDropped`, `Http3ExtendedConnectTests.ServeAsync_HeldDatagramPastTheHeadTimeout_IsDroppedAndTheSessionGetsOnlyLaterOnes` (datagrams, held by `Surl.Http3`) |
| WebTransport:Surl-3-4 | MUST | A stream naming a request stream that is not a session is reset `WT_BUFFERED_STREAM_REJECTED` at once; one naming an ended session `WT_SESSION_GONE` (ADR-0117 decision 3). | `WebTransportSessionDirectoryTests.ServeStreamAsync_StreamNamingAnOrdinaryRequest_IsResetWithBufferedStreamRejected`, `WebTransportSessionDirectoryTests.ServeStreamAsync_StreamNamingAnEndedSession_IsResetWithSessionGone` |

## Flow control and the session limit (WTH3 5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| WebTransport:WTH3-5.5-1 | MUST | The server's `SETTINGS` carry `SETTINGS_WT_INITIAL_MAX_STREAMS_UNI` (`0x2b64`) = 100, `SETTINGS_WT_INITIAL_MAX_STREAMS_BIDI` (`0x2b65`) = 100 and `SETTINGS_WT_INITIAL_MAX_DATA` (`0x2b61`) = 1048576. | `WebTransportApplicationProtocolTests.ServeAsync_Connection_SendsSettingsWithExtendedConnectDatagramsWtEnabledAndTheInitialFlowControlLimits` |
| WebTransport:WTH3-5.1-1 | MUST | Flow control is on only when the client also sends a non-zero `SETTINGS_WT_INITIAL_*`; with it off, every flow-control capsule is ignored. | `WebTransportFlowControlSettingsTests.IsOnFor_ClientSettings_IsOnWhenAnyInitialLimitIsNonZero`, `WebTransportSessionTests.ServeAsync_FlowControlCapsulesWithFlowControlOff_AreIgnored` |
| WebTransport:WTH3-5.1-2 | MUST | With flow control off, a CONNECT that would make a second session on the connection is reset `H3_REQUEST_REJECTED`. | `WebTransportConnectHandlerTests.ServeSessionAsync_SecondSessionWithoutFlowControl_IsResetRequestRejected` |
| WebTransport:WTH3-5.2-1 | SHOULD | With flow control on, a CONNECT that would make a 17th concurrent session is reset `H3_REQUEST_REJECTED` (ADR-0117 decision 5). | `WebTransportConnectHandlerTests.ServeSessionAsync_SeventeenthSessionWithFlowControl_IsResetRequestRejected` |
| WebTransport:WTH3-5.6-1 | MUST | The capsules `WT_MAX_DATA` (`0x190b4d3d`), `WT_MAX_STREAMS` (`0x190b4d3f`, `0x190b4d40`), `WT_DATA_BLOCKED` (`0x190b4d41`) and `WT_STREAMS_BLOCKED` (`0x190b4d43`, `0x190b4d44`) are each a type, a length and one variable-length integer. | `WebTransportCapsuleCodecTests.Write_LimitCapsule_IsTypeLengthAndOneVariableLengthInteger`, `WebTransportCapsuleCodecTests.Read_ValueBreakingItsLayout_IsMalformedNotIncomplete` |
| WebTransport:WTH3-5.6-2 | SHOULD | The server raises `WT_MAX_DATA` and `WT_MAX_STREAMS` as it consumes data and streams close, each value above the last, without waiting for a `*_BLOCKED`, and never `WT_MAX_DATA` past `--max-filesize`. | `WebTransportSessionTests.ServeStreamAsync_HalfTheDataWindowRead_RaisesWtMaxDataToTheBytesReadPlusTheWindow`, `WebTransportSessionTests.ServeStreamAsync_HalfTheDataWindowReadNear_MaxFilesize_RaisesWtMaxDataNoFurtherThanIt`, `WebTransportSessionTests.ServeStreamAsync_ClientStreamFinished_RaisesWtMaxStreamsByTheStreamClosed`, `WebTransportReceiveLimitsTests` |
| WebTransport:WTH3-5.6-3 | MUST | A client stream past `WT_MAX_STREAMS`, or stream payload past `WT_MAX_DATA`, ends the session `WT_FLOW_CONTROL_ERROR`. | `WebTransportSessionTests.ServeStreamAsync_ClientStreamPastWtMaxStreams_EndsTheSessionWithFlowControlError`, `WebTransportSessionTests.ServeStreamAsync_PayloadPastWtMaxData_EndsTheSessionWithFlowControlError` |
| WebTransport:WTH3-5.6-4 | MUST | A client `WT_MAX_DATA` or `WT_MAX_STREAMS` that does not increase the last value, or a `WT_MAX_STREAMS` above 2^60, ends the session `WT_FLOW_CONTROL_ERROR`. | `WebTransportSessionTests.ServeAsync_ClientLimitThatDoesNotRaiseItsLast_EndsTheSessionWithFlowControlError`, `WebTransportFlowControlTests` |
| WebTransport:WTH3-5.6-5 | MUST | The server honours the client's limits for what it sends, stops reading the stream it would echo while blocked, and sends `WT_DATA_BLOCKED` or `WT_STREAMS_BLOCKED` once per limit value. | `WebTransportSessionTests.ServeStreamAsync_ClientsWtMaxDataReached_EchoesUpToItSendsWtDataBlockedAndResumesWhenRaised`, `WebTransportSessionTests.ServeStreamAsync_ClientsUnidirectionalWtMaxStreamsReached_SendsWtStreamsBlockedAndAnswersWhenRaised`, `WebTransportSendCreditTests` |
| WebTransport:WTH3-5.4-1 | MUST | `WT_MAX_STREAM_DATA` or `WT_STREAM_DATA_BLOCKED` ends the session `H3_MESSAGE_ERROR` (ADR-0117 decision 4). | `WebTransportSessionTests.ServeAsync_CapsuleHttp3Prohibits_ResetsTheConnectStreamWithMessageError` |

## Session termination and draining (WTH3 4.7, 6)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| WebTransport:WTH3-6-1 | MUST | `WT_CLOSE_SESSION` is type `0x2843`, a 32-bit code and a UTF-8 message of at most 1024 bytes; a longer message is refused by the codec. | `WebTransportCapsuleCodecTests.Read_CloseSessionWith1024ByteMessage_IsAccepted`, `WebTransportCapsuleCodecTests.Read_CloseSessionWith1025ByteMessage_IsMalformed`, `WebTransportCapsuleCodecTests.Constructor_CloseSessionWith1025ByteMessage_IsRefused` |
| WebTransport:WTH3-6-2 | MUST | On the client's `WT_CLOSE_SESSION`, FIN, or reset of the CONNECT stream, the server resets its halves and stops the client's halves of every session stream `WT_SESSION_GONE`, drops the session's datagrams, and closes its side of the CONNECT stream with FIN and no capsule. | `WebTransportSessionTests.ServeAsync_ClientClosesTheSession_EndsEveryStreamOfTheSessionWithSessionGoneThenFin`, `WebTransportSessionTests.ServeAsync_ClientFinishesTheConnectStream_EndsTheSessionWithFin`, `WebTransportSessionTests.ServeAsync_ClientResetsTheConnectStream_ResetsTheServerHalfWithSessionGone` |
| WebTransport:WTH3-6-3 | MUST | A received close message over 1024 bytes or not UTF-8, or any byte after `WT_CLOSE_SESSION`, resets the CONNECT stream `H3_MESSAGE_ERROR`. | `WebTransportSessionTests.ServeAsync_MalformedCloseSession_ResetsTheConnectStreamWithMessageError`, `WebTransportSessionTests.ServeAsync_CapsuleAfterCloseSession_ResetsTheConnectStreamWithMessageError`, `WebTransportCapsuleCodecTests` (the 1024-byte and UTF-8 rules) |
| WebTransport:WTH3-6-4 | MUST | The server's own `WT_CLOSE_SESSION` (code 0 `server shutting down`, code 1 `session data limit reached`) is followed at once by FIN; it then opens no stream and sends no datagram for the session. | `WebTransportSessionTests.ServeAsync_Shutdown_SendsDrainSessionAndCloseSessionThenGoAway` (code 0); `WebTransportSessionTests.ServeStreamAsync_PayloadReachesMaxFilesize_ClosesTheSessionWithCode1ThenFin`, `WebTransportSessionTests.ServeAsync_DatagramsReachMaxFilesize_CloseTheSessionWithCode1` (code 1) |
| WebTransport:WTH3-4.7-1 | MAY | On the client's `WT_DRAIN_SESSION` (`0x78ae`, empty) the server keeps serving the session. | `WebTransportSessionTests.ServeAsync_ClientDrainsAndSendsAnUnknownCapsule_KeepsServingTheSession` |
| WebTransport:WTH3-4.7-2 | MUST | On shutdown or `--max-time` the server sends `WT_DRAIN_SESSION` on every session, then `GOAWAY`, serves sessions through the grace, then closes those left with `WT_CLOSE_SESSION` code 0. | `WebTransportSessionTests.ServeAsync_Shutdown_SendsDrainSessionThenGoAwayAndCloseSessionAfterTheGrace` (drain, `GOAWAY`, then close), `WebTransportSessionTests.ServeAsync_ShutdownGrace_KeepsEchoingUntilTheCloseSession` (serving through the grace) |
| WebTransport:WTH3-6-5 | SHOULD | Before `CONNECTION_CLOSE` the server waits, within the shutdown grace, for the client to close each CONNECT stream. | `WebTransportSessionTests.ServeAsync_ClientFinishesTheConnectStreamWithinTheShutdownGrace_ClosesTheConnectionAtOnce` (the connection stays open through the grace and closes once the client FINs its CONNECT stream), `WebTransportSessionTests.ServeAsync_ShutdownGrace_KeepsEchoingUntilTheCloseSession` (else at the grace) |

## Authentication and limits (ADR-0117 decisions 7 and 8)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| WebTransport:Surl-7-1 | MUST | A CONNECT without credentials is refused `:status 401` with the session's challenges unless `--allow-anonymous` is given. | `WebTransportConnectAuthenticationTests.RefuseAsync_ConnectWithoutALogin_IsAnswered401WithTheRequestBoundChallengesAndFin`, `WebTransportConnectAuthenticationTests.RefuseAsync_AnonymousConnectUnderAllowAnonymous_IsAccepted` |
| WebTransport:Surl-7-2 | MUST | A CONNECT with a good Basic or Bearer `authorization` from the one account set is accepted; a bad one is refused `401`, never served as anonymous, after the refusal delay. | `WebTransportConnectAuthenticationTests.RefuseAsync_ConnectWithTheAccountsLogin_IsAccepted`, `WebTransportConnectAuthenticationTests.RefuseAsync_NegotiateCredentialsUnderAllowAnonymous_AreAnswered401NotServedAsAnonymous` |
| WebTransport:Surl-7-3 | MUST | A `Forbidden` verdict is `:status 403`; an NTLM or Negotiate `authorization` is `401` with the request-bound challenges. | `WebTransportConnectAuthenticationTests.RefuseAsync_ConnectWithAWrongLogin_IsAnswered403`, `WebTransportConnectAuthenticationTests.RefuseAsync_NtlmCredentials_AreNotJudgedAndAreAnswered401WithTheRequestBoundChallenges` |
| WebTransport:Surl-8-1 | MUST | A session whose received payload reaches `--max-filesize` is closed `WT_CLOSE_SESSION` code 1, `session data limit reached`. | `WebTransportSessionTests.ServeStreamAsync_PayloadReachesMaxFilesize_ClosesTheSessionWithCode1ThenFin`, `WebTransportSessionTests.ServeAsync_DatagramsReachMaxFilesize_CloseTheSessionWithCode1` |
| WebTransport:Surl-8-2 | MUST | A capsule whose declared length exceeds `--max-message` resets the CONNECT stream `H3_EXCESSIVE_LOAD` before its value is read. | `WebTransportSessionTests.ServeAsync_CapsulePastTheMessageLimit_EndsTheSessionOnTheResetSurlHttp3Sent` |
| WebTransport:Surl-8-3 | MUST | Echo waiting to be written is capped at 65536 bytes a stream (reading pauses) and 64 datagrams a session (the newest dropped). | the stream half: `WebTransportSession.EchoChunkLength`; the datagram half: `WebTransportDatagramEchoQueue.Capacity`, tested by `WebTransportDatagramEchoQueueTests` |
| WebTransport:Surl-8-4 | MUST | Connection limits, the handshake and header timeouts, `--idle-timeout` and `--max-time` are `Surl.Http3`'s and `Surl.Quic`'s (ADR-0082 decision 7). | n/a: those libraries enforce them and their own tests prove it (BL-383, BL-410); the WebTransport server adds nothing |

## Live client (BL-462; ADR-0083 decisions 1 to 3)

`PinnedCurlPortClientTalksToSurlOverWebTransportTests` (`Integration`, in `Surl.Conformance.UnitTests`) runs the
Curl port client `CurlPortBuilds.json` pins against a live `surl webtransport://` started with `--self-signed`
(the client skips verification with `-k`). Each assertion is the rule's own, never what the client prints beyond
the bytes the server was required to echo. The tests are inconclusive where no pinned client is installed. A
server with no account set lets every CONNECT in, so the accepted exchanges pass `--allow-anonymous` and the
refusal passes `--user`. The port's client exits 0 on a refused CONNECT (a defect in the port, reported under
ADR-0083, not in surl), so the refusal test asserts only that nothing is echoed.

| Row | Live test |
| --- | --- |
| WebTransport:WTH3-3.2-2, WebTransport:Surl-3-1 | `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Send_OnABidirectionalStream_IsEchoedOnItByteForByte` |
| WebTransport:WTH3-4.2-1, WebTransport:Surl-3-2 | `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Send_OnAUnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamByteForByte` |
| WebTransport:WTH3-4.5-1 | `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Send_AsADatagram_IsEchoedAsADatagramWithTheSamePayload` |
| WebTransport:Surl-7-1 | `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Connect_WithoutCredentials_EchoesNothing` |

Every other row is proved by the unit tests its "Covered by" column names, or is marked `n/a` with its reason.
