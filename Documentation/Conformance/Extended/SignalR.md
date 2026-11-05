# SignalR requirement matrix

The server-side requirements `Surl.Extended.Protocol.SignalR` answers, for the `signalr` and
`signalrs` schemes (ADR-0083 decision 9). Written from ASP.NET Core SignalR's
`TransportProtocols.md` (cited as TP) and `HubProtocol.md` (cited as HUB) at dotnet/aspnetcore
`main`, commit `6aacb16a27a677d16278a9814ed04f871020854b`, read 2026-11-05, and RFC 6455 for the
WebSocket transport: the specifications
[ADR-0119](../../Planning/Decisions/ADR-0119-how-the-signalr-server-answers-signalr-and-signalrs.md)
decides by, never the Curl port's client matrix. Each row binds the server; "Covered by" names the
task that proves it, until that task replaces its ID with the named test, or says why the row needs
no test here.

IDs are `SignalR:TP-<section>-<n>` and `SignalR:HUB-<section>-<n>` for the specifications, and
`SignalR:Surl-<decision>-<n>` for a rule ADR-0119 adds where the specifications leave the server a
choice. Client-only requirements (following a redirect, choosing a transport, ignoring messages after
a CancelInvocation, reconnecting on `allowReconnect`) are not rows. Requirements of the transports,
stateful reconnect and MessagePack that later tasks add are rows marked "n/a" naming that task, and
the task turns each into a row of its own when it lands.

## Negotiate (TP "POST [endpoint-base]/negotiate")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:TP-Negotiate-1 | MUST | `POST [endpoint-base]/negotiate` is answered `200`, `Content-Type: application/json`, with a JSON object holding `negotiateVersion`, `connectionId` and `availableTransports`. | `SignalRRequestAnswererTests.AnswerAsync_NegotiateVersionOne_AnswersAFreshIdAndTokenAndTheWebSocketsTransport` |
| SignalR:TP-Negotiate-2 | MUST | A `negotiateVersion` query of 1 or more is answered with version 1 and a `connectionToken` beside the `connectionId`. | `SignalRRequestAnswererTests.AnswerAsync_NegotiateVersionOne_AnswersAFreshIdAndTokenAndTheWebSocketsTransport`, `AnswerAsync_NegotiateVersionAboveOne_AnswersVersionOne` |
| SignalR:TP-Negotiate-3 | MUST | A request without `negotiateVersion`, or with `0`, is answered with version 0 and no `connectionToken`. | `SignalRRequestAnswererTests.AnswerAsync_NegotiateVersionZero_AnswersAnIdWithoutAToken` |
| SignalR:TP-Negotiate-4 | SHOULD | The `connectionToken` is secret: 16 random bytes, independent of the `connectionId`; each negotiate gives a fresh pair (ADR-0119 decision 2). | `SignalRHubConnectionsTests.Negotiate_VersionOne_GivesAnIdAndAnIndependentTokenEach22Base64UrlCharacters`, `Negotiate_Twice_GivesFreshIds`; `SignalRRequestAnswererTests.AnswerAsync_NegotiateTwice_AnswersFreshIdsEachTime` |
| SignalR:TP-Negotiate-5 | MUST | `availableTransports` lists each transport served, with its `transferFormats`: WebSockets with `Text` and `Binary` (ADR-0119 decision 2). | `SignalRRequestAnswererTests.AnswerAsync_NegotiateVersionOne_AnswersAFreshIdAndTokenAndTheWebSocketsTransport` |
| SignalR:TP-Negotiate-6 | MUST | The negotiate `error` form ends a connection attempt; it is sent only when the hub is full, as `{"error":"The server has too many connections."}` (ADR-0119 decisions 2 and 7). | `SignalRRequestAnswererTests.AnswerAsync_HubFull_AnswersTheNegotiateErrorForm`; `SignalRHubConnectionsTests.Negotiate_HubFull_IsRefusedUntilOneExpires` |
| SignalR:Surl-2-1 | MUST | Another method on `[endpoint-base]/negotiate` is `405` with `Allow: POST`; a `negotiateVersion` that is not a non-negative integer is `400` (ADR-0119 decision 2). | `SignalRRequestAnswererTests.AnswerAsync_NegotiateWithAnotherMethod_IsMethodNotAllowed`, `AnswerAsync_NegotiateVersionNotANonNegativeInteger_IsBadRequest` |
| SignalR:Surl-1-1 | MUST | `[endpoint-base]` is the listen URL's path without a trailing `/`; any path other than it and its `/negotiate` is `404`, judged after authentication (ADR-0119 decision 1). | `SignalRHubPathTests.FromListenPath_Path_GivesTheEndpointBaseAndNegotiatePath`; `SignalRRequestAnswererTests.AnswerAsync_PathTheHubDoesNotServe_IsNotFound`, `AnswerAsync_UnknownPathWithoutAToken_IsUnauthorizedNotNotFound` |
| SignalR:TP-Negotiate-7 | MAY | A redirect response (`url`, `accessToken`). | n/a: the hub is the endpoint a redirect would name, so the server never redirects (ADR-0119 decision 2) |
| SignalR:TP-Negotiate-8 | MAY | `useAck` / `useStatefulReconnect` answered with `"useStatefulReconnect":true`. | n/a: stateful reconnect is BL-755's (ADR-0119 decision 6); until then the response never offers it |

## The WebSocket transport (TP "WebSockets"; RFC 6455)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:TP-WebSockets-1 | MUST | A WebSocket upgrade of `[endpoint-base]` with the `id` of a negotiated connection is answered `101` and attached to that connection. | `SignalRRequestAnswererTests.AnswerAsync_UpgradeNamingANegotiatedConnection_Switches` |
| SignalR:TP-WebSockets-2 | MUST | An upgrade without `id` establishes a new connection. | `SignalRRequestAnswererTests.AnswerAsync_UpgradeByAccessTokenWithoutAnId_SwitchesToANewConnection`, `SignalRHubSessionTests.RunAsync_Upgrade_IsAnswered101WithTheAcceptKey` |
| SignalR:TP-WebSockets-3 | MUST | An upgrade with an `id` no connection has (unknown, expired, ended, or a version 1 connection's `connectionId`) is `404`. | `SignalRRequestAnswererTests.AnswerAsync_UpgradeWithAConnectionTokenNotFromNegotiate_IsNotFound`, `AnswerAsync_UpgradeWithAVersionOneConnectionsId_IsNotFound`, `SignalRHubConnectionsTests.TakeUp_AfterEnd_IsNotFound`, `TakeUp_AfterTheUpgradeWindow_IsNotFound` |
| SignalR:TP-WebSockets-4 | MUST | A second WebSocket for a connection that already has one is `409`. | `SignalRRequestAnswererTests.AnswerAsync_UpgradeOfAConnectionAlreadyTakenUp_IsConflict`, `SignalRHubConnectionsTests.TakeUp_NegotiatedTokenBySameCaller_HandsOverTheConnectionOnce` |
| SignalR:Surl-2-2 | MUST | A negotiated connection not upgraded within `--head-timeout` is forgotten, and its token is then `404` (ADR-0119 decisions 2 and 7). | `SignalRHubConnectionsTests.TakeUp_AfterTheUpgradeWindow_IsNotFound` |
| SignalR:RFC6455-4.2-1 | MUST | The upgrade request is checked and refused as `Surl.WebSocket` does, with no subprotocol chosen (ADR-0071, ADR-0084 decision 2). | n/a: `Surl.WebSocket` owns the checks and is tested there (BL-408) |
| SignalR:Surl-2-3 | MUST | Until BL-757, a `GET [endpoint-base]` that is not an upgrade gets `Surl.WebSocket`'s `426`, and `POST` or `DELETE` on it `405` with `Allow: GET` (ADR-0119 decision 2). | `SignalRRequestAnswererTests.AnswerAsync_GetOnTheEndpointBaseWithoutAnUpgrade_IsUpgradeRequired`, `AnswerAsync_OtherMethodOnTheEndpointBase_IsMethodNotAllowed` |
| SignalR:TP-Errors-1 | MUST | Errors while serving end the WebSocket with a non-`1000` close code; frames over `--max-message` with `1009` (ADR-0119 decisions 3 and 7). | `SignalRHubSessionTests.RunAsync_FrameRfc6455MakesInvalid_IsClosedWithItsCode`, `RunAsync_FramePastMaxMessage_IsClosedWithMessageTooBig`, `RunAsync_FragmentedMessagePastMaxMessage_IsClosedWithMessageTooBig` |
| SignalR:TP-ServerSentEvents-1 | MUST | The Server-Sent Events transport: `text/event-stream`, the mandatory `id`, `400` without it, `404` for an unknown one. | n/a: BL-757 adds the transport (ADR-0119 decision 2) |
| SignalR:TP-LongPolling-1 | MUST | The Long Polling transport: the mandatory `id`, `204` to end a superseded poll, `200` with no content on a server timeout, `DELETE` to end the connection. | n/a: BL-757 adds the transport (ADR-0119 decision 2) |
| SignalR:TP-HttpPost-1 | MUST | The HTTP Post half-transport: the mandatory `id`, `200` once the payload is processed, `409` for a second outstanding post, `404` for an ended connection. | n/a: BL-757 adds the transport (ADR-0119 decision 2) |
| SignalR:Surl-0-1 | MUST | `signalrs` is the same server inside TLS from the first byte; without `--cert` or `--self-signed` the listener fails with `CertificateProblem` (58). | n/a: the listener seam wraps TLS before the server reads a byte (ADR-0010); BL-450 registers `signalrs` and proves it |

## The handshake (HUB "Overview")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:HUB-Handshake-1 | MUST | The handshake request `{"protocol":"json","version":1}` is answered `{}` then `0x1E`. | `SignalRHubSessionTests.RunAsync_Handshake_IsAnsweredWithAnEmptyObject` |
| SignalR:HUB-Handshake-2 | MUST | A protocol the server does not speak, or a version other than 1, is answered `{"error":"Requested protocol is not available."}` then `0x1E`, and the connection is closed. | `SignalRHubSessionTests.RunAsync_HandshakeRefused_AnswersTheHandshakeErrorThenClosesNormally` |
| SignalR:HUB-Handshake-3 | MUST | A first record that is not a handshake request is answered `{"error":"The handshake request is invalid."}` then `0x1E`, and the connection is closed. | `SignalRHubSessionTests.RunAsync_HandshakeRefused_AnswersTheHandshakeErrorThenClosesNormally` |
| SignalR:HUB-Handshake-4 | MUST | Records that follow the handshake request in the same WebSocket message are read as messages. | `SignalRHubSessionTests.RunAsync_HandshakeFollowedByARecordInTheSameMessage_AnswersBoth` |
| SignalR:Surl-3-1 | MUST | No handshake request within `--head-timeout` closes the WebSocket `1008` with no handshake response (ADR-0119 decision 3). | `SignalRHubSessionTests.RunAsync_NoHandshakeWithinTheHeadTimeout_ClosesWithPolicyViolation` |
| SignalR:HUB-MessagePack-1 | MUST | The MessagePack encoding's handshake and messages. | n/a: BL-756 adds MessagePack (ADR-0119 decision 3); until then it is refused as HUB-Handshake-2 says |

## JSON records and messages (HUB "JSON Encoding", "Message Headers", "Errors")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:HUB-Json-1 | MUST | Every record the server writes is one JSON object ended by `0x1E`, in its own text message. | `HubMessageJsonReaderTests.ReadThenWrite_HubProtocolExample_WritesTheExampleWithoutWhitespace`; one record a text message: `SignalRHubSessionTests.RunAsync_StreamInvocation_SendsEachItemAHundredMillisecondsApartThenACompletion` |
| SignalR:HUB-Json-2 | MUST | Records are cut at `0x1E` whatever the messages: several in one message are read separately, one split across messages is reassembled; text and binary messages are both read. | `HubRecordReaderTests.TryAppend_TwoMessagesInOneBuffer_ReadsThemSeparately`, `TryAppend_MessageSplitAcrossTwoBuffers_ReassemblesIt`; text and binary messages: `SignalRHubSessionTests.RunAsync_WebSocketPongAndFragments_AreReadWithoutAnAnswer` (binary), `RunAsync_RecordSpanningMessagesPastMaxMessage_IsClosedWithTheErrorCloseAndMessageTooBig` (text) |
| SignalR:HUB-Json-3 | MUST | Each message type 1 to 7 is read and written with HubProtocol.md's fields, as its JSON examples show. | `HubMessageJsonReaderTests.ReadThenWrite_HubProtocolExample_WritesTheExampleWithoutWhitespace`, `TryRead_ProtocolError_IsRefused` |
| SignalR:HUB-Headers-1 | MUST | A message carrying `headers` is processed and its headers disregarded. | `HubMessageJsonReaderTests.ReadThenWrite_HubProtocolExample_WritesTheExampleWithoutWhitespace` (the header example) |
| SignalR:Surl-3-2 | MAY | Members the server does not know are ignored, not treated as protocol errors (ADR-0119 decision 3). | `HubMessageJsonReaderTests.TryRead_UnknownMembers_AreIgnored` |
| SignalR:HUB-Errors-1 | MAY | A protocol error (a record that is not an object, a missing or wrongly typed required member, an unknown `type`, an `invocationId` already in progress, a StreamItem or Completion for no upload stream, a Completion with both `result` and `error`) ends the connection with `{"type":7,"error":"Connection closed with an error."}` and WebSocket Close `1002`. | `SignalRHubSessionTests.RunAsync_RecordTheServerDoesNotRead_IsAProtocolError`, `RunAsync_InvocationIdAlreadyInProgress_IsAProtocolError` |
| SignalR:HUB-Ack-1 | MUST | Ack (8) and Sequence (9) messages and their sequencing. | n/a: BL-755 adds stateful reconnect (ADR-0119 decision 6); until then types 8 and 9 are protocol errors |

## Invocations and streams (HUB "Invocations" to "Completion and results")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:HUB-Invocation-1 | MUST | An Invocation with an `invocationId` is answered with one Completion carrying that `invocationId`: `Echo`'s argument as `result` (ADR-0119 decision 1). | `SignalRHubSessionTests.RunAsync_EchoInvocation_CompletesWithItsArgumentByteForByte` |
| SignalR:HUB-Invocation-2 | MUST | A void method's Completion has neither `result` nor `error` (`Broadcast`). | BL-768 |
| SignalR:HUB-Invocation-3 | MUST NOT | An Invocation without an `invocationId` gets no response at all, not even an error. | `SignalRHubSessionTests.RunAsync_NonBlockingInvocation_GetsNoCompletion` |
| SignalR:Surl-1-2 | MUST | `Broadcast` sends every connection on the hub that finished its handshake, the caller included, a non-blocking Invocation of `Broadcast` with the same arguments (ADR-0119 decision 1). | BL-768 |
| SignalR:HUB-Stream-1 | MUST | A StreamInvocation of `Count` is answered with one StreamItem per result, then a Completion; the items paced 100 ms apart on the `TimeProvider`. | `SignalRHubSessionTests.RunAsync_StreamInvocation_SendsEachItemAHundredMillisecondsApartThenACompletion`, `RunAsync_StreamOfNoItems_CompletesAtOnce` |
| SignalR:HUB-Stream-2 | MUST | A StreamInvocation of a single-result method, or an Invocation of a streaming one, is completed with an `error`. | `SignalRHubSessionTests.RunAsync_CallTheHubCannotServe_CompletesWithItsError` |
| SignalR:HUB-Cancel-1 | MUST | A CancelInvocation stops the stream it names and that invocation's Completion is sent. | `SignalRHubSessionTests.RunAsync_CancelInvocation_StopsTheStreamAndCompletesIt` |
| SignalR:HUB-Cancel-2 | SHOULD | A CancelInvocation naming no invocation in progress is ignored. | `SignalRHubSessionTests.RunAsync_CancelInvocationNamingNothingInProgress_IsIgnored` |
| SignalR:HUB-Upload-1 | MUST | An invocation of `Collect` with one `streamIds` entry reads the client's StreamItems for that stream until its Completion, then completes with the items. | BL-768 |
| SignalR:HUB-Errors-2 | MUST | A method that fails completes its invocation with `error` and the fixed text `An unexpected error occurred invoking 'Fail' on the server.`; streamed items already sent stand. | `SignalRHubSessionTests.RunAsync_FailingMethod_CompletesWithError` |
| SignalR:Surl-1-3 | MUST | An unknown `target`, wrong arguments and too many invocations in progress are each completed with their fixed `error` text (ADR-0119 decision 1). | `SignalRHubSessionTests.RunAsync_CallTheHubCannotServe_CompletesWithItsError`, `RunAsync_MoreStreamsThanAllowed_CompletesTheNextWithTooManyInvocations` |

## Keep-alive, timeout and close (HUB "Ping", "Close Message Encoding")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:HUB-Ping-1 | SHOULD | The server sends a Ping `{"type":6}` whenever it has sent nothing for 15 s. | `SignalRHubSessionTests.RunAsync_ServerSendsNothingForAKeepAliveInterval_SendsAPingEachInterval` |
| SignalR:HUB-Ping-2 | MUST | A client Ping needs no answer and gets none. | `SignalRHubSessionTests.RunAsync_ServerSendsNothingForAKeepAliveInterval_SendsAPingEachInterval`, `RunAsync_CancelInvocationNamingNothingInProgress_IsIgnored` |
| SignalR:HUB-Ping-3 | SHOULD | A client silent for 30 s gets a Close `{"type":7,"error":"Server timeout elapsed without receiving a message from the client."}` and WebSocket Close `1000`. | `SignalRHubSessionTests.RunAsync_ClientQuietPastTheTimeout_IsClosedWithTheTimeoutClose` |
| SignalR:HUB-Close-1 | MUST | A client Close ends the connection: every invocation stopped, WebSocket Close `1000`. | `SignalRHubSessionTests.RunAsync_ClientHubClose_ClosesNormallyWithNothingMore` |
| SignalR:Surl-7-1 | MUST | Shutdown sends each connection a Close `{"type":7}` then WebSocket Close `1001`, within the grace (ADR-0119 decision 7, ADR-0059). | BL-768 |

## Authentication and limits (ADR-0119 decisions 5 and 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SignalR:Surl-5-1 | MUST | Negotiate without credentials is `401` with the session's challenges and no body, unless `--allow-anonymous`. | `SignalRRequestAnswererTests.AnswerAsync_NegotiateWithoutAToken_IsUnauthorizedWithTheSessionsChallenge`, `AnswerAsync_NegotiateAnonymouslyWhenAllowed_RecordsTheAnonymousCaller` |
| SignalR:Surl-5-2 | MUST | A bearer token in `Authorization`, or in an `access_token` query when there is no `Authorization`, is judged as an empty-name account's password; a wrong one is `401` or `403`. | `SignalRRequestAnswererTests.AnswerAsync_AccessTokenQueryParameter_IsJudgedAsABearerAuthorization`, `AnswerAsync_AccessTokenBesideAnAuthorizationField_IsIgnored`, `AnswerAsync_NegotiateWithAForbiddenToken_IsForbidden` |
| SignalR:Surl-5-3 | MUST | The upgrade is judged too, and a connection negotiated by one caller is upgraded only by that caller; another is `403`. | `SignalRRequestAnswererTests.AnswerAsync_UpgradeByACallerOtherThanTheNegotiator_IsForbidden`, `SignalRHubConnectionsTests.TakeUp_ByAnotherAccount_IsRefusedAndLeavesTheConnectionWaiting` |
| SignalR:Surl-5-4 | MUST | Credentials that fail are never served as anonymous under `--allow-anonymous`. | `SignalRRequestAnswererTests.AnswerAsync_WrongTokenWhenAnonymousIsAllowed_IsUnauthorizedNotServedAnonymously` |
| SignalR:Surl-7-2 | MUST | A hub record over `--max-message` is refused before the rest is read: Close `{"type":7,"error":"Connection closed with an error."}`, WebSocket Close `1009`. | `HubRecordReaderTests.TryAppend_MessageOverTheLimit_IsRefusedBeforeItsEnd`; the Close and WebSocket `1009`: `SignalRHubSessionTests.RunAsync_RecordSpanningMessagesPastMaxMessage_IsClosedWithTheErrorCloseAndMessageTooBig` |
| SignalR:Surl-7-3 | MUST | An `invocationId` or stream ID over 128 characters is a protocol error. | `HubMessageJsonReaderTests.TryRead_InvocationIdPastTheLimit_IsRefused`, `TryRead_StreamIdPastTheLimit_IsRefused`; the Close: `SignalRHubSessionTests.RunAsync_RecordTheServerDoesNotRead_IsAProtocolError` |
| SignalR:Surl-7-4 | MUST | 1024 connections a hub, negotiated or upgraded; the next negotiate gets the `error` form, the next upgrade without `id` `503`. | `SignalRHubConnectionsTests.TakeUp_WithoutAnId_MakesANewConnectionThatNeverExpiresUntilTheHubIsFull`, `SignalRRequestAnswererTests.AnswerAsync_UpgradeWithoutAnIdOnAFullHub_IsServiceUnavailable` |
| SignalR:Surl-7-5 | MUST | A connection with more than 1048576 bytes unsent is closed with the protocol-error Close and WebSocket Close `1008`. | BL-768 |
| SignalR:Surl-7-6 | MUST | A `Collect` upload over `--max-filesize` is completed with `The upload stream is too large.` | BL-768 |
