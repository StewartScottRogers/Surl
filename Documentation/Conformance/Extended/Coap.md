# Coap requirement matrix

The server-side requirements `Surl.Extended.Protocol.Coap` answers, for the `coap` and `coaps`
schemes (ADR-0083 decision 9). Written from RFC 7252, RFC 7959, RFC 7641 and RFC 6690, the
specifications
[ADR-0097](../../Planning/Decisions/ADR-0097-how-the-coap-server-answers-coap-and-coaps.md)
decides by, never from the Curl port's client matrices. Each row binds a server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

A row's ID is `RFC<number>-<section>-<n>`, numbered in the order the statements appear in the
section. A statement that binds only a client is left out, except where the server must detect its
breach; such a row says what the server does. Where the RFC states a rule a server follows without
a keyword, the Level says "(unkeyworded)". Rows from ADR-0083 and ADR-0087 cover the command line.

## Message format (RFC 7252 section 3)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-3-1 | MUST | Every message the server sends has Version 1. | `CoapAppendixAExampleTests.Write_AppendixAExample_WritesItsBytes` |
| Coap:RFC7252-3-2 | MUST | A message with any other version is silently ignored, whatever its type. | `CoapMessageReaderTests.Read_AnotherVersion_IsNotCoap`, `CoapProtocolServerTests.ServeAsync_AckRstAndNonCoapDatagrams_AreIgnored` |
| Coap:RFC7252-3-3 | MUST NOT, MUST | Token Length 9 to 15 is never sent, and a received one is a message format error. | `CoapMessageReaderTests.Read_TokenLength9To15_IsAFormatError`, `CoapMessageTests.Constructor_TokenOf9Bytes_Throws` |
| Coap:RFC7252-3-4 | MUST | A payload marker followed by a zero-length payload is a message format error; the server never writes a marker without a payload. | `CoapMessageReaderTests.Read_PayloadMarkerWithNoPayload_IsAFormatError`, `CoapMessageWriterTests.Write_NoPayload_WritesNoPayloadMarker` |
| Coap:RFC7252-3.1-1 | MUST | Options appear in order of their option numbers, delta-encoded from the previous one (from 0 for the first); the 13 and 14 nibbles take one and two extension bytes. | `CoapMessageReaderTests.Read_OptionDelta_ReadsTheNumberFromTheNibbleAndItsExtension`, `Read_OptionLength_ReadsThatManyValueBytes`, `Read_SeveralOptions_AddsEachDeltaToThePreviousNumber`, `CoapMessageWriterTests.Write_OptionDelta_UsesTheNibbleAndItsExtension`, `Write_OptionLength_UsesTheNibbleAndItsExtension`, `Write_OptionsGivenOutOfOrder_WritesThemInOrderOfNumberKeepingRepeatsInOrder` |
| Coap:RFC7252-3.1-2 | MUST | An Option Delta nibble of 15 in a byte that is not the payload marker 0xFF is a message format error. | `CoapMessageReaderTests.Read_OptionDelta15_IsAFormatError` |
| Coap:RFC7252-3.1-3 | MUST | An Option Length nibble of 15 is a message format error. | `CoapMessageReaderTests.Read_OptionLength15_IsAFormatError` |
| Coap:RFC7252-3.2-1 | SHOULD | The server writes each uint option value in the fewest bytes (0 as an empty value). | `CoapOptionTests.FromUInt_WritesTheFewestBytes` |
| Coap:RFC7252-3.2-2 | MUST | A received uint option value with leading zero bytes is read. | `CoapOptionTests.ReadUInt_ReadsLeadingZeroBytes` |

## The message layer (RFC 7252 section 4)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-4.1-1 | MUST | An Empty message (code 0.00) has Token Length 0 and no byte after the Message ID; a received one that has either is a message format error. The server's Empty ACK and RST have neither. | `CoapMessageReaderTests.Read_EmptyMessageWithAToken_IsAFormatError`, `Read_EmptyMessageWithBytesAfterItsMessageId_IsAFormatError`, `CoapMessageTests.EmptyReset_IsAnEmptyRstWithTheMessageId` |
| Coap:RFC7252-4.2-1 | MUST | A CON is acknowledged, or rejected with an RST when the server lacks context: an Empty CON (CoAP ping), a code of class 1, 6 or 7, a response code, or a message format error. | `CoapProtocolServerTests.ServeAsync_MalformedCon_IsAnsweredWithRstOfTheSameMessageId`, `ServeAsync_EmptyCon_IsAnsweredWithRst`, `ServeAsync_ConWithAResponseOrReservedCode_IsAnsweredWithRst`, `ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag` |
| Coap:RFC7252-4.2-2 | MUST | An ACK echoes the CON's Message ID and carries a response or is Empty. | `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag`, `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain` |
| Coap:RFC7252-4.2-3 | MUST | An RST echoes the Message ID of the message it rejects and is Empty. | `CoapProtocolServerTests.ServeAsync_MalformedCon_IsAnsweredWithRstOfTheSameMessageId` |
| Coap:RFC7252-4.2-4 | (unkeyworded) | An ACK carrying a request or a code of a reserved class, and an RST that is not Empty, are silently ignored. | `CoapFlowSessionTests.ServeAsync_AckWithARequestOrReservedCodeOrAnotherIdAndRstNotEmpty_AreIgnored` |
| Coap:RFC7252-4.2-5 | MUST | For each CON the server sends it keeps a timeout and a retransmission counter: the first timeout random between ACK_TIMEOUT and ACK_TIMEOUT × ACK_RANDOM_FACTOR, doubled on each retransmission, up to MAX_RETRANSMIT; a matching ACK or RST ends it, and the last timeout gives the message up. | `CoapFlowSessionTests.ServeAsync_UnacknowledgedSeparateResponse_IsRetransmittedWithDoublingTimeoutsThenGivenUp`, `ServeAsync_MatchingAckOrEmptyRst_EndsTheRetransmission`, `CryptographicCoapRandomSourceTests.InitialTimeout_LiesBetweenAckTimeoutAndItsRandomFactor` |
| Coap:RFC7252-4.2-6 | MUST | Retransmissions are at least ACK_TIMEOUT apart and all fall within MAX_TRANSMIT_SPAN. | `CoapFlowSessionTests.ServeAsync_UnacknowledgedSeparateResponse_IsRetransmittedWithDoublingTimeoutsThenGivenUp` (2, 4, 8 and 16 seconds apart, 30 seconds in all) |
| Coap:RFC7252-4.2-7 | MUST | The server keeps the state to answer a retransmitted CON request with its ACK even after its own CON response was acknowledged. | `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain`, `CoapProtocolServerTests.ServeAsync_DuplicateCon_IsAnsweredWithTheSameResponseAndNotServedTwice`, `CoapDuplicateCacheTests.TryRecall_BeforeExpiry_ReturnsTheAnswerAndAfterIt_Nothing`, BL-1037 (a duplicate arriving after the separate response's ACK) |
| Coap:RFC7252-4.2-8 | SHOULD, MAY | ICMP errors are checked against the original datagram or ignored. | n/a: the `IDatagramFlow` seam reports no ICMP error (ADR-0004 section 3), so every one is ignored, as the section allows |
| Coap:RFC7252-4.3-1 | MUST NOT | A NON the server sends carries a request or a response and is never Empty. | `CoapProtocolServerTests.ServeAsync_NonGet_IsAnsweredWithANon205`, `ServeAsync_NonRequests_AreAnsweredWithNonsNumberedFromTheDrawnMessageId` |
| Coap:RFC7252-4.3-2 | MUST NOT | A NON is never acknowledged. | `CoapProtocolServerTests.ServeAsync_NonGet_IsAnsweredWithANon205`, `ServeAsync_NonRequests_AreAnsweredWithNonsNumberedFromTheDrawnMessageId` |
| Coap:RFC7252-4.3-3 | MUST, MAY | A NON the server lacks context for (Empty, a reserved class, a format error, an unrecognised critical option) is rejected and silently ignored; the server sends no RST for it (ADR-0097 decision 2). | `CoapProtocolServerTests.ServeAsync_MalformedNon_IsIgnored`, `ServeAsync_NonWithAResponseCode_IsIgnored` |
| Coap:RFC7252-4.4-1 | MUST | An ACK or RST echoes the Message ID of the message it answers. | `CoapProtocolServerTests.ServeAsync_MalformedCon_IsAnsweredWithRstOfTheSameMessageId`, `ServeAsync_MissingResource_Gets404WithItsToken`, `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain` |
| Coap:RFC7252-4.4-2 | MUST NOT | A Message ID the server originates is not reused with the same endpoint within EXCHANGE_LIFETIME. | `CoapProtocolServerTests.ServeAsync_NonRequests_AreAnsweredWithNonsNumberedFromTheDrawnMessageId` (each Message ID the flow originates is the next of 65536) |
| Coap:RFC7252-4.4-3 | MUST | A client's ACK or RST matches a server message only when its Message ID and source endpoint are that message's Message ID and destination endpoint (the flow). | `CoapFlowSessionTests.ServeAsync_MatchingAckOrEmptyRst_EndsTheRetransmission`, `ServeAsync_AckWithARequestOrReservedCodeOrAnotherIdAndRstNotEmpty_AreIgnored`, `CoapProtocolServerTests.ServeAsync_AckRstAndNonCoapDatagrams_AreIgnored` |
| Coap:RFC7252-4.5-1 | SHOULD | Each duplicate of a CON within EXCHANGE_LIFETIME gets the same ACK or RST, and its request is processed once. | `CoapProtocolServerTests.ServeAsync_DuplicateCon_IsAnsweredWithTheSameResponseAndNotServedTwice`, `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain`, `ServeAsync_NonDuplicate_IsIgnoredAndADuplicateCon_AfterExchangeLifetime_IsServedAgain`, `CoapDuplicateCacheTests.TryRecall_BeforeExpiry_ReturnsTheAnswerAndAfterIt_Nothing`, `Remember_AnExpiredId_ReplacesIt`, `Remember_PastTheCapacity_ForgetsTheOldest` |
| Coap:RFC7252-4.5-2 | SHOULD | A duplicate NON within NON_LIFETIME is silently ignored and its request processed once. | `CoapFlowSessionTests.ServeAsync_NonDuplicate_IsIgnoredAndADuplicateCon_AfterExchangeLifetime_IsServedAgain` |
| Coap:RFC7252-4.6-1 | SHOULD | A message fits one IP packet; with no known path MTU the server keeps messages near 1152 bytes and payloads at 1024 bytes. | BL-473 |
| Coap:RFC7252-4.7-1 | SHOULD | The server rate-limits its transmissions: one outstanding CON per flow, at most 8 requests in progress per flow (ADR-0097 decisions 2 and 7). | `CoapFlowSessionTests.ServeAsync_SecondSeparateResponse_WaitsUntilTheFirstIsAcknowledged`, BL-751 (the 8 requests in progress) |
| Coap:RFC7252-4.8.1-1 | MUST NOT | ACK_TIMEOUT is not lowered and NSTART not raised without congestion-control measurements; the server uses section 4.8's defaults. | `CoapFlowSessionTests.ServeAsync_UnacknowledgedSeparateResponse_IsRetransmittedWithDoublingTimeoutsThenGivenUp` (ACK_TIMEOUT 2 seconds), `ServeAsync_SecondSeparateResponse_WaitsUntilTheFirstIsAcknowledged` (NSTART 1) |
| Coap:RFC7252-4.8.1-2 | MUST NOT, SHOULD | ACK_RANDOM_FACTOR is not below 1.0 and differs from it (1.5). | `CryptographicCoapRandomSourceTests.InitialTimeout_LiesBetweenAckTimeoutAndItsRandomFactor` |
| Coap:RFC7252-4.8.1-3 | RECOMMENDED | Transmission parameters are consistent; the server uses section 4.8's defaults, as constants. | `CoapFlowSessionTests.ServeAsync_UnacknowledgedSeparateResponse_IsRetransmittedWithDoublingTimeoutsThenGivenUp` (ACK_TIMEOUT, MAX_RETRANSMIT), `ServeAsync_NonDuplicate_IsIgnoredAndADuplicateCon_AfterExchangeLifetime_IsServedAgain` (NON_LIFETIME), `CryptographicCoapRandomSourceTests.InitialTimeout_LiesBetweenAckTimeoutAndItsRandomFactor` (ACK_RANDOM_FACTOR) |

## Requests, responses, options and response codes (RFC 7252 section 5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-5.1-1 | MUST NOT | `GET` takes no action on a resource other than retrieval. | BL-1037 |
| Coap:RFC7252-5.1-2 | MUST | `GET`, `PUT` and `DELETE` are idempotent. | `CoapProtocolServerTests.ServeAsync_PutUnderUploads_Gets201ThenChanged204`, `CoapResourceAnswererTests.AnswerAsync_DeleteOfAMissingOrRefusedPath_Is202AndOfADirectory_Is405`, BL-1037 (`GET`) |
| Coap:RFC7252-5.2.1-1 | (unkeyworded) | A response ready in time is piggybacked in the ACK (ADR-0097 decision 2: within 1 second). | `CoapFlowSessionTests.ServeAsync_AnswerReadyBeforeTheSecond_IsPiggybacked` |
| Coap:RFC7252-5.2.2-1 | MUST NOT | Once an Empty ACK was sent for a request, the response never goes in an ACK; a retransmitted request gets another Empty ACK and the response stays separate. | `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain` |
| Coap:RFC7252-5.2.2-2 | MUST | The server stops retransmitting a CON response on a matching ACK, ignoring any code or payload in it, or on a matching RST. | `CoapFlowSessionTests.ServeAsync_MatchingAckOrEmptyRst_EndsTheRetransmission` |
| Coap:RFC7252-5.2.3-1 | SHOULD | A NON request is answered with a NON response. | `CoapProtocolServerTests.ServeAsync_NonGet_IsAnsweredWithANon205`, `ServeAsync_NonRequests_AreAnsweredWithNonsNumberedFromTheDrawnMessageId` |
| Coap:RFC7252-5.3.1-1 | MUST | Every response to a request, separate responses and notifications included, echoes the request's token unmodified. | `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag`, `ServeAsync_MissingResource_Gets404WithItsToken`, `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain`, `CoapFlowObserversTests.ServeAsync_PutFromAnotherFlow_NotifiesTheObserverWithAHigherNumberAndTheNewState` |
| Coap:RFC7252-5.3.1-2 | MUST | A token of 0 to 8 bytes is opaque: the server makes no assumption about its content. | `CoapProtocolServerTests.ServeAsync_MissingResource_Gets404WithItsToken`, `CoapMessageReaderTests.Read_CopiesTheBytes_SoTheDatagramMayBeReused` |
| Coap:RFC7252-5.3.2-1 | MUST | A response leaves from the endpoint the request was sent to (the flow's listen port, never a new one). | `CoapProtocolServerTests` (every `ServeAsync_*` test checks the flow never left the listen port) |
| Coap:RFC7252-5.3.2-2 | MUST | A piggybacked response's ACK has the CON's Message ID and the request's token; a separate response has the request's token. | `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag`, `CoapFlowSessionTests.ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain` |
| Coap:RFC7252-5.4-1 | MUST NOT, MUST | An option not defined for a method or response code is never sent, and in a request is treated as unrecognised (Observe or ETag on anything but `GET`, Block1 on `GET` or `DELETE`). | `CoapRequestOptionsTests.Read_UnrecognisedCriticalOption_IsNamed`, `Read_UnrecognisedElectiveOption_IsIgnored`, `Read_Block1OnAPut_IsRecognised` |
| Coap:RFC7252-5.4.1-1 | MUST | An unrecognised elective option is silently ignored. | `CoapRequestOptionsTests.Read_UnrecognisedElectiveOption_IsIgnored` |
| Coap:RFC7252-5.4.1-2 | MUST, SHOULD | An unrecognised critical option in a CON request is answered `4.02 Bad Option`, with a diagnostic payload naming it. | `CoapProtocolServerTests.ServeAsync_UnknownCriticalOption_Gets402NamingIt`, `CoapRequestOptionsTests.Read_UnrecognisedCriticalOption_IsNamed` |
| Coap:RFC7252-5.4.1-3 | MUST | An unrecognised critical option in a NON rejects the message, which is silently ignored. | `CoapProtocolServerTests.ServeAsync_NonWithAnUnknownCriticalOption_IsIgnored` |
| Coap:RFC7252-5.4.1-4 | MUST | An unrecognised critical option in a CON response or an ACK rejects it. | n/a: the server receives no responses; a client's ACK to a server CON is Empty (section 5.2.2) and is matched by Message ID alone |
| Coap:RFC7252-5.4.3-1 | MUST | An option whose length is outside its defined range is treated as unrecognised. | `CoapRequestOptionsTests.Read_UnrecognisedCriticalOption_IsNamed` (an empty Uri-Host, a one-byte If-None-Match), `Read_UnrecognisedElectiveOption_IsIgnored` (a five-byte Size2) |
| Coap:RFC7252-5.4.4-1 | SHOULD NOT, MUST | An option at its default value is not sent; an absent option means its default. | `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag` (Max-Age 60 is sent although it is the default, on purpose: ADR-0097 decision 1 sends it explicitly for RFC 7641), BL-1037 (an absent option read as its default) |
| Coap:RFC7252-5.4.5-1 | MUST NOT | A non-repeatable option is sent at most once. | `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag`, `CoapResourceAnswererTests.AnswerAsync_Get_Is205WithETagMaxAgeContentFormatAndTheBytes` |
| Coap:RFC7252-5.4.5-2 | MUST | Each supernumerary occurrence of a non-repeatable option is treated as unrecognised. | `CoapRequestOptionsTests.Read_SecondUriHost_IsAnUnrecognisedCriticalRepeat` |
| Coap:RFC7252-5.5-1 | MUST NOT, MUST | No payload is sent where the code defines none (`2.03`, `2.31`, the Empty messages); a payload on a request whose method defines none (`GET`, `DELETE`) is ignored. | `CoapResourceAnswererTests.AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload`, `CoapMessageTests.Constructor_EmptyMessageWithAPayload_Throws`, BL-473, BL-1037 (a request payload on `GET` or `DELETE`) |
| Coap:RFC7252-5.5.1-1 | SHOULD | A payload is sniffed only when no Content-Format is given. | n/a: the server never interprets a request payload; it stores the bytes as sent (ADR-0097 decision 1) |
| Coap:RFC7252-5.5.2-1 | MUST | A diagnostic payload is UTF-8 in Net-Unicode form, fixed text from the server's table (ADR-0006 section 3). | `CoapProtocolServerTests.ServeAsync_UnknownCriticalOption_Gets402NamingIt` |
| Coap:RFC7252-5.5.2-2 | SHOULD | An error response's payload is empty when the code says everything. | `CoapProtocolServerTests.ServeAsync_MissingResource_Gets404WithItsToken`, `CoapResourceAnswererTests.AnswerAsync_StoreFailure_Is500WithoutPayload` |
| Coap:RFC7252-5.6-1 | MUST NOT | A success code the endpoint does not recognise is not cached; a stored response is used only when fresh and matching. | n/a: the server keeps no response cache; its duplicate answers are the message layer's (section 4.5) |
| Coap:RFC7252-5.6.1-1 | MUST | An origin server that wishes to prevent caching sends Max-Age 0. | n/a: the server sends Max-Age 60 on every cacheable response and never prevents caching (ADR-0097 decision 1) |
| Coap:RFC7252-5.7.1-1 | MUST | A proxy answers `5.04` for a timed-out request and `5.02` for a response it cannot process. | n/a: the server is an origin server, not a proxy |
| Coap:RFC7252-5.7.1-2 | MUST NOT | A proxy's Max-Age from cache does not extend the origin's. | n/a: not a proxy |
| Coap:RFC7252-5.7.1-3 | MUST | A proxy processes every option, answers `4.02` for unrecognised unsafe options and `5.02` for unrecognised unsafe response options, and forwards Safe-to-Forward ones. | n/a: not a proxy |
| Coap:RFC7252-5.7.2-1 | MUST | A proxy request the endpoint is unwilling to forward is answered `5.05 Proxying Not Supported`; one whose authority names this endpoint is served as local. | `CoapResourceAnswererTests.AnswerAsync_Proxying_Is505`, BL-750 (served as local) |
| Coap:RFC7252-5.7.2-2 | MUST | A forwarding proxy splits the request URI into Uri-* options and consumes Proxy-Uri or Proxy-Scheme. | n/a: not a proxy |
| Coap:RFC7252-5.8-1 | MUST | A request with an unrecognised or unsupported method code (FETCH, PATCH and iPATCH included) is answered `4.05 Method Not Allowed`, piggybacked. | `CoapProtocolServerTests.ServeAsync_UnsupportedMethod_Gets405`, `CoapResourceAnswererTests.AnswerAsync_AnyOtherMethod_Is405` |
| Coap:RFC7252-5.8.1-1 | SHOULD | A successful `GET` is `2.05 Content`, or `2.03 Valid` when a request ETag is current. | `CoapResourceAnswererTests.AnswerAsync_Get_Is205WithETagMaxAgeContentFormatAndTheBytes`, `AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload` |
| Coap:RFC7252-5.8.2-1 | SHOULD | A `POST` that creates a resource is `2.01 Created` with its Location-Path; one that does not is `2.04 Changed`. | `CoapResourceAnswererTests.AnswerAsync_PostToADirectory_CreatesARandomlyNamedFileAnd201sWithItsLocationPath`, `AnswerAsync_PostToAFile_AppendsAnd204s` |
| Coap:RFC7252-5.8.3-1 | SHOULD, MAY | A `PUT` to an existing resource is `2.04 Changed`; to an absent one creates it, `2.01 Created`; one that cannot be done gets an error code. | `CoapProtocolServerTests.ServeAsync_PutUnderUploads_Gets201ThenChanged204`, `CoapResourceAnswererTests.AnswerAsync_PutIntoAMissingDirectoryOrOntoADirectory_Is404`, `AnswerAsync_PutOverTheLimit_Is413WithSize1` |
| Coap:RFC7252-5.8.4-1 | SHOULD | A `DELETE` is `2.02 Deleted` on success and when the resource did not exist. | `CoapProtocolServerTests.ServeAsync_Delete_Gets202`, `CoapResourceAnswererTests.AnswerAsync_DeleteOfAMissingOrRefusedPath_Is202AndOfADirectory_Is405` |
| Coap:RFC7252-5.9.1.3-1 | MUST | `2.03 Valid` carries an ETag and no payload. | `CoapResourceAnswererTests.AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload` |
| Coap:RFC7252-5.9.2-1 | SHOULD | A `4.xx` response carries a diagnostic payload as section 5.5.2 says. | `CoapProtocolServerTests.ServeAsync_UnknownCriticalOption_Gets402NamingIt`, `ServeAsync_MissingResource_Gets404WithItsToken` |
| Coap:RFC7252-5.9.2.9-1 | SHOULD | `4.13 Request Entity Too Large` carries Size1, the largest entity the server accepts. | BL-473 |
| Coap:RFC7252-5.9.3-1 | SHOULD | A `5.xx` response carries a diagnostic payload as section 5.5.2 says. | `CoapResourceAnswererTests.AnswerAsync_StoreFailure_Is500WithoutPayload`, `CoapRequestFaultsTests.ServeAsync_RequestUnderARule_GetsTheChosenCodeAndDiagnostic` |
| Coap:RFC7252-5.9.3.4-1 | (unkeyworded) | `5.03 Service Unavailable` carries Max-Age as the seconds after which to retry. | BL-751 |
| Coap:RFC7252-5.10.1-1 | MUST NOT | A Uri-Path of `.` or `..` is not valid; the server answers `4.00 Bad Request` (ADR-0097 decision 3). | `CoapResourceAnswererTests.AnswerAsync_DotSegment_Is400` |
| Coap:RFC7252-5.10.2-1 | MUST | A request with Proxy-Uri (or Proxy-Scheme) to an endpoint unwilling to act as a forward proxy is answered `5.05`. | `CoapResourceAnswererTests.AnswerAsync_Proxying_Is505`, `CoapRequestOptionsTests.Read_ProxyOptions_NameAProxy` |
| Coap:RFC7252-5.10.2-2 | MUST | Proxy-Uri takes precedence over Uri-Host, Uri-Port, Uri-Path and Uri-Query. | BL-750 |
| Coap:RFC7252-5.10.4-1 | MUST | A request whose Accept names a Content-Format the server cannot return is answered `4.06 Not Acceptable`. | `CoapResourceAnswererTests.AnswerAsync_GetWithAccept_IsServedOnlyInTheResourcesFormat` |
| Coap:RFC7252-5.10.5-1 | SHOULD | A resource with a strict Max-Age tolerance updates the value before each retransmission. | n/a: no resource has a strict tolerance; a retransmission repeats its bytes, as section 4.5 asks of duplicates |
| Coap:RFC7252-5.10.6-1 | MUST | A request's entity-tags are opaque, compared byte for byte. | `CoapResourceAnswererTests.AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload`, `AnswerAsync_GetWithIfMatchOfTheCurrentETag_IsServedAndOfAnotherIs412` |
| Coap:RFC7252-5.10.6.1-1 | MUST NOT | A response carries at most one ETag. | `CoapProtocolServerTests.ServeAsync_ConGet_IsAnsweredWithAPiggybacked205CarryingTheTokenContentFormatAndETag`, `CoapResourceAnswererTests.AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload` |
| Coap:RFC7252-5.10.6.2-1 | MAY | A `GET` with ETags is answered `2.03 Valid` echoing the one that is current. | `CoapResourceAnswererTests.AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload`, `AnswerAsync_WellKnownCoreWithItsETag_Is203` |
| Coap:RFC7252-5.10.7-1 | MUST NOT | A Location-Path value the server sends is never `.` or `..`. | `CoapResourceAnswererTests.AnswerAsync_PostToADirectory_CreatesARandomlyNamedFileAnd201sWithItsLocationPath` |
| Coap:RFC7252-5.10.7-2 | MUST | A request carrying a reserved Location-* option (128, 132, 136, 140) is answered `4.02`. | `CoapRequestOptionsTests.Read_UnrecognisedCriticalOption_IsNamed`, `CoapProtocolServerTests.ServeAsync_UnknownCriticalOption_Gets402NamingIt` |
| Coap:RFC7252-5.10.8-1 | MUST NOT, MUST | A request whose If-Match or If-None-Match condition fails is not performed and is answered `4.12 Precondition Failed`. | `CoapProtocolServerTests.ServeAsync_PutWithIfNoneMatchOnAnExistingResource_Gets412AndKeepsIt`, `CoapResourceAnswererTests.AnswerAsync_ConditionalPut_IsPerformedOnlyWhenTheConditionHolds`, `AnswerAsync_GetWithIfMatchOfTheCurrentETag_IsServedAndOfAnotherIs412` |
| Coap:RFC7252-5.10.8-2 | MAY | Conditions are ignored when the request would end in neither `2.xx` nor `4.12`. | BL-1037 |

## URIs, ports and discovery (RFC 7252 sections 6 and 7, RFC 6690)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-6-1 | MUST | A URI with an empty host is invalid. | n/a: binds a client resolving a request URI; the server ignores Uri-Host (ADR-0097 decision 1) |
| Coap:RFC7252-6.2-1 | MUST | `coaps` datagrams are secured with DTLS as section 9.1 says. | BL-475 |
| Coap:RFC7252-6.5-1 | MUST | The request URI built from the options (here, the path handed to the content store) percent-encodes with upper-case hex digits. | BL-1037 |
| Coap:RFC7252-7.2-1 | SHOULD | An endpoint supports the CoRE link format at `/.well-known/core`, except where fully manual configuration is wanted: served under `--list-directories`, `4.04` without it (ADR-0097 decision 1). | `CoapResourceAnswererTests.AnswerAsync_WellKnownCore_IsServedOnlyUnderTheListingSwitchAndOnlyToGet`, `CoapProtocolServerTests.ServeAsync_WellKnownCore_ListsTheResourcesInLinkFormat` |
| Coap:RFC7252-7.2.1-1 | MUST | A `ct` attribute is a decimal integer from 0 to 65535. | `CoapResourceDirectoryTests.Write_ListsVisibleFilesInOrdinalOrderOfPath_LeavingHiddenAndUnmappableOnesOut` |
| Coap:RFC6690-2-1 | MUST NOT | `href` is not used as a link parameter. | `CoapResourceDirectoryTests.Write_ListsVisibleFilesInOrdinalOrderOfPath_LeavingHiddenAndUnmappableOnesOut` |
| Coap:RFC6690-3.3-1 | SHOULD, MUST NOT | `sz` is given for resources larger than one MTU, at most once a link. | `CoapResourceDirectoryTests.Write_ListsVisibleFilesInOrdinalOrderOfPath_LeavingHiddenAndUnmappableOnesOut` (`sz` once on every link) |
| Coap:RFC6690-4-1 | MUST | A server offering discovery serves `/.well-known/core` in the CoRE link format, Content-Format 40; with no link the payload is empty. | `CoapResourceAnswererTests.AnswerAsync_WellKnownCore_IsServedOnlyUnderTheListingSwitchAndOnlyToGet`, `CoapResourceDirectoryTests.Write_EmptyStore_IsEmpty` |
| Coap:RFC6690-4.1-1 | MAY, MUST | Query filtering on `href` and target attributes, complete or prefix with `*`, percent-decoded and compared bitwise; an implementation without filtering ignores the query. | `CoapResourceDirectoryTests.Write_FirstQueryWithAValue_FiltersTheLinks`, BL-1037 (a percent-encoded filter) |

## Multicast (RFC 7252 section 8)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-8-1 | MUST, MAY | An endpoint is prepared to receive multicast messages and may ignore them. | n/a: the server joins no multicast group and binds only its listen URL's address (ADR-0097 decision 6) |
| Coap:RFC7252-8.1-1 | MUST NOT | A server aware that a NON arrived by multicast sends no RST for it. | `CoapProtocolServerTests.ServeAsync_MalformedNon_IsIgnored`, `ServeAsync_NonWithAResponseCode_IsIgnored` |
| Coap:RFC7252-8.1-2 | SHOULD | A server knows whether a request arrived by multicast. | n/a: no group is joined, and the server never answers a NON with RST, so it needs not know (ADR-0097 decision 2) |
| Coap:RFC7252-8.2-1 | MAY, SHOULD | A response to a multicast request is delayed by a random point within a leisure period. | n/a: no multicast request is served |
| Coap:RFC7252-11.3-2 | SHOULD NOT, SHOULD | Multicast requests that cannot be authenticated are not accepted, and multicast is limited to the resources that need it. | n/a: no multicast group is joined |
| Coap:RFC6690-4.1-2 | SHOULD NOT | A multicast discovery request whose filter does not match is not answered. | n/a: no multicast request is served |

## CoAP over DTLS (RFC 7252 section 9)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-9-1 | SHOULD | Under NoSec, lower-layer security is used when appropriate. | n/a: a deployment choice; `coap` is NoSec and its writes need `--allow-anonymous` (ADR-0097 decision 6) |
| Coap:RFC7252-9.1.1-1 | MUST | Every CoAP message over `coaps` is DTLS application data; nothing is read before the handshake finishes. | BL-475 |
| Coap:RFC7252-9.1.1-2 | MUST | An ACK or RST matches a CON or NON only in the same DTLS session and epoch. | BL-475 |
| Coap:RFC7252-9.1.1-3 | MUST NOT | A retransmission never crosses epochs. | BL-475 |
| Coap:RFC7252-9.1.2-1 | MUST | A response to a DTLS-secured request goes in the same session and epoch; a NoSec message never matches a DTLS request. | BL-475 |
| Coap:RFC7252-9.1.3-1 | SHOULD | Devices indicate their authority with SNI. | n/a: binds the client; ADR-0087 serves one certificate whatever the name |
| Coap:RFC7252-9.1.3.1-1 | MUST | PreSharedKey mode supports `TLS_PSK_WITH_AES_128_CCM_8`, with identities from the accounts. | BL-475 |
| Coap:RFC7252-9.1.3.2-1 | MUST | RawPublicKey mode supports `TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8` on secp256r1 with the curve and point-format extensions, uncompressed points. | BL-475 |
| Coap:RFC7252-9.1.3.2.1-1 | MUST, SHOULD | An implementation checking raw-public-key identities supports `sha-256-120` identifiers. | n/a: the server accepts no client raw public key (ADR-0087 decision 3) |
| Coap:RFC7252-9.1.3.3-1 | MUST | Certificate mode supports `TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8` with an ECDSA secp256r1 certificate. | BL-475 |
| Coap:RFC7252-9.1.3.3-2 | MUST, SHOULD | A peer certificate is validated as RFC 5280 section 6 says, its validity dates checked: a client certificate under `--cacert` (ADR-0010 section 5). | BL-475 |
| Coap:RFC7252-9.1.3.3-3 | MUST | The request URI's authority matches the certificate's SubjectAltName or Common Name. | n/a: binds a client verifying the server's certificate |
| Coap:RFC7252-9.1.3.3-4 | SHOULD | With a shared key beside the certificate, an ECDHE_PSK suite is used. | n/a: ADR-0087 decision 2 serves no ECDHE_PSK or CBC suite |
| Coap:RFC7959-7-1 | MUST, MUST NOT | Every block of a resource passes the same security checks; an unprotected exchange cannot influence a protected transfer's blocks (Block1 sessions belong to one flow, one DTLS association). | BL-473, BL-475 |
| Coap:RFC7641-7-2 | MUST | Every notification goes in the same epoch of the same DTLS association as its registration. | BL-475 |

## Security considerations and registries (RFC 7252 sections 11 and 12)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-11.2-1 | MUST NOT | Responses to `coaps` requests are not reused for shared caching. | n/a: the server keeps no response cache |
| Coap:RFC7252-11.3-1 | SHOULD NOT | Large amplification is not given to unauthenticated requests: blocks are at most 1024 bytes, and ADR-0097 decision 4 records why not smaller. | BL-473 |
| Coap:RFC7252-11.6-1 | MUST NOT | A node without good entropy generates no keys. | n/a: keys come from the operator's certificate or accounts, and randomness from `RandomNumberGenerator` |
| Coap:RFC7252-12.2-1 | MUST NOT | Experimental option numbers 65000 to 65535 are not used operationally. | n/a: the server sends only the options ADR-0097 decision 3 lists |
| Coap:RFC7252-12.3-1 | MUST NOT | Experimental Content-Format identifiers 65000 to 65535 are not used operationally. | n/a: the server sends only the formats ADR-0097 decision 1 lists |

## Block-wise transfers (RFC 7959)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7959-2.1-1 | MUST | A Block1 or Block2 option present is processed, or the message rejected. | BL-473 |
| Coap:RFC7959-2.1-2 | MUST NOT | Neither Block option occurs twice in a message the server sends; a second one received is unrecognised (`4.02`). | BL-473 |
| Coap:RFC7959-2.2-1 | MUST | The M bit of a Block2 option in a request is ignored. | BL-473 |
| Coap:RFC7959-2.2-2 | MUST NOT, MUST | SZX 7 is never sent, and in a request is answered `4.00 Bad Request`. | BL-473 |
| Coap:RFC7959-2.3-1 | MUST | A block sent with M set has exactly the size SZX gives; a Block1 block received with M set and another size is answered `4.00` (ADR-0097 decision 3). | BL-473 |
| Coap:RFC7959-2.3-2 | MUST NOT | A response whose Block1 has M set (`2.31 Continue`) carries no Block2. | BL-473 |
| Coap:RFC7959-2.3-3 | MUST, MUST NOT | Content-Format describes the whole body; Block1 blocks with a Content-Format other than block 0's are not assembled (`4.08`). | BL-473 |
| Coap:RFC7959-2.4-1 | MUST | A response uses the block size the request's Block2 asks for, or a smaller one. | BL-473 |
| Coap:RFC7959-2.4-2 | MUST | Every response in one Block2 sequence has the same block size, except the last, and the first when the first request had no Block2. | BL-473 |
| Coap:RFC7959-2.4-3 | SHOULD | Each Block2 response carries an ETag. | BL-473 |
| Coap:RFC7959-2.4-4 | MAY | The server keeps the representation for an ongoing Block2 sequence. | n/a: blocks are read afresh, statelessly, each with the current ETag (ADR-0097 decision 4) |
| Coap:RFC7959-2.5-1 | (unkeyworded) | An atomic Block1 upload answers each non-final block `2.31 Continue` and acts on the final block. | BL-473 |
| Coap:RFC7959-2.5-2 | MUST | A final block arriving without all earlier blocks is answered `4.08 Request Entity Incomplete`. | BL-473 |
| Coap:RFC7959-2.5-3 | MAY | A block out of sequence is answered `4.08`. | BL-473 |
| Coap:RFC7959-2.5-4 | MAY | A partial body is discarded EXCHANGE_LIFETIME after its last block. | BL-473 |
| Coap:RFC7959-2.5-5 | MAY | A server with no room to store blocks answers `4.13` at any point of a Block1 transfer. | BL-473 |
| Coap:RFC7959-2.5-6 | (unkeyworded) | A new block 0 for a resource from the same endpoint replaces the context kept for it. | BL-473 |
| Coap:RFC7959-2.6-1 | SHOULD | The block size of an observation's registration bounds every notification and response for it. | BL-474 |
| Coap:RFC7959-2.6-2 | (unkeyworded) | A notification of a larger representation carries only its first block. | BL-474 |
| Coap:RFC7959-2.6-3 | SHOULD | Each notification and block response carries an ETag. | BL-474 |
| Coap:RFC7959-2.7-1 | MUST | Block2 transfers of a Block1 request's response body run in order. | n/a: no `PUT` or `POST` response has a body (ADR-0097 decision 4) |
| Coap:RFC7959-2.8-1 | (unkeyworded) | A response to a multicast `GET` may use Block2 NUM 0. | n/a: no multicast request is served |
| Coap:RFC7959-2.9.1-1 | (unkeyworded) | `2.31 Continue` carries no payload. | BL-473 |
| Coap:RFC7959-2.9.3-1 | SHOULD | `4.13` carries Size1; a Block1 option with a smaller SZX in it is a hint to use that size. | BL-473 |
| Coap:RFC7959-2.10-1 | MUST NOT | If-None-Match is used only on Block1 block 0; the server evaluates both conditional options on block 0 only. | BL-473 |
| Coap:RFC7959-4-1 | MAY | A request with Size2 0 gets Size2 with the representation's size; the first block of a Block2 sequence carries it unasked. | BL-473 |
| Coap:RFC7959-4-2 | MUST NOT | Neither Size option occurs twice in a message the server sends. | BL-473 |
| Coap:RFC7959-7.1-1 | SHOULD | State created for untrusted sources is bounded: Block1 sessions per flow, uploads by `--max-filesize`, no Block2 state (ADR-0097 decision 7). | BL-473 |

## Observe (RFC 7641)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7641-4.1-1 | MUST | A successful registration (`GET` with Observe 0) returns the current representation and keeps the observer updated while it is on the list. | BL-474 |
| Coap:RFC7641-4.1-2 | MUST NOT, MUST | An entry is keyed by client endpoint and token; a registration matching an entry replaces it and adds none. | BL-474 |
| Coap:RFC7641-4.1-3 | MAY, MUST NOT | A registration the server will not add (a resource that is not observable, a limit) is served as a plain `GET` whose response has no Observe. | BL-474 |
| Coap:RFC7641-4.1-4 | MUST | A `GET` with Observe 1 removes the entry with its endpoint and token and is served as a plain `GET` without Observe. | BL-474 |
| Coap:RFC7641-4.2-1 | MUST | Every notification, the first response included, echoes the registration's token. | BL-474 |
| Coap:RFC7641-4.2-2 | SHOULD | A notification is `2.05` or `2.03`. | BL-474 |
| Coap:RFC7641-4.2-3 | SHOULD, MUST | A change that would make a `GET` fail (a deleted file) is notified with that code (`4.04`), and the entry is then removed. | BL-474 |
| Coap:RFC7641-4.2-4 | MUST | A `2.xx` notification has the Content-Format of the first response. | BL-474 |
| Coap:RFC7641-4.2-5 | SHOULD, MUST | A server unable to keep the format sends `4.06` and removes the entry. | n/a: a file's Content-Format follows its name, which an observation cannot change (ADR-0097 decision 1) |
| Coap:RFC7641-4.2-6 | MUST, MUST NOT | A `2.xx` notification carries Observe; a non-`2.xx` one does not. | BL-474 |
| Coap:RFC7641-4.3.1-1 | MUST | The observed state is kept as close to the actual state as possible: a CoAP write notifies at once, any other change by the next Max-Age refresh. | BL-474 |
| Coap:RFC7641-4.3.1-2 | MUST | Each notification states its freshness in Max-Age. | BL-474 |
| Coap:RFC7641-4.3.1-3 | MAY | A notification of the unchanged state is sent as Max-Age lapses. | BL-474 |
| Coap:RFC7641-4.3.2-1 | MAY | A notification whose ETag the client listed is sent as `2.03 Valid`. | n/a: notifications are always `2.05` (ADR-0097 decision 5) |
| Coap:RFC7641-4.4-1 | MUST, MAY, MUST NOT | Observe carries the 24 low bits of a strictly increasing sequence number, starting anywhere, rising by no more than 2^23 in 256 seconds. | BL-474 |
| Coap:RFC7641-4.4-2 | MUST | Each number exceeds every earlier one for the same client, token and resource, and is current when sent, a retransmission included. | BL-474 |
| Coap:RFC7641-4.5-1 | MAY | Notifications of intermediate states may be skipped. | BL-474 |
| Coap:RFC7641-4.5-2 | MUST | An observer eventually observes the latest state when the resource stops changing. | BL-474 |
| Coap:RFC7641-4.5-3 | MUST | An RST to a notification, or the last retransmission of a CON notification timing out, removes the entry. | BL-474 |
| Coap:RFC7641-4.5-4 | MUST | A notification goes as CON at least every 24 hours (here, every notification is CON). | BL-474 |
| Coap:RFC7641-4.5.1-1 | MUST | At most NSTART (1) notifications and responses are outstanding to a client. | `CoapFlowSessionTests.ServeAsync_SecondSeparateResponse_WaitsUntilTheFirstIsAcknowledged`, BL-474 |
| Coap:RFC7641-4.5.1-2 | SHOULD NOT | Non-confirmable notifications go no faster than one per RTT, or one every 3 seconds. | n/a: no notification is NON (ADR-0097 decision 5) |
| Coap:RFC7641-4.5.2-1 | MAY | An outstanding notification is superseded by a newer one, keeping its timer and counter. | n/a: a newer state waits until the outstanding notification ends (ADR-0097 decision 5) |
| Coap:RFC7641-5-1 | MUST | An intermediary registers once with the next hop, keeps its own sequence numbers and Max-Age, and fans notifications out. | n/a: the server is not an intermediary |
| Coap:RFC7641-6-1 | MAY | A link to an observable resource carries the `obs` attribute. | `CoapResourceDirectoryTests.Write_ListsVisibleFilesInOrdinalOrderOfPath_LeavingHiddenAndUnmappableOnesOut` |
| Coap:RFC7641-6-2 | MUST NOT | `obs` has no value and appears at most once in a link. | `CoapResourceDirectoryTests.Write_ListsVisibleFilesInOrdinalOrderOfPath_LeavingHiddenAndUnmappableOnesOut` |
| Coap:RFC7641-7-1 | MUST | Without client authentication, notifications between acknowledgements are strictly limited: NON notifications are interspersed with CON ones (here, all are CON). | BL-474 |

## The command line (ADR-0083, ADR-0087, ADR-0097 decision 6)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Coap:RFC7252-6.1-1 | MUST | A `coap` listen URL without a port listens on UDP 5683. | BL-476 |
| Coap:RFC7252-6.2-2 | MUST | A `coaps` listen URL without a port listens on UDP 5684. | BL-476 |
| Coap:RFC7252-7.1-1 | MUST, MAY | A server offering discovery supports port 5683; 5684 may serve it too. | BL-476 |
| Coap:ADR0083-6-1 | MUST | `SURL_EXTENSIONS=0` leaves `coap` and `coaps` unregistered. | BL-476 |
| Coap:ADR0087-3-1 | MUST | A `coaps` listen URL with neither a certificate nor an account is refused before any listener binds, `CertificateProblem` (58). | BL-476 |
| Coap:ADR0096-6-1 | MUST | The access rules (`--allow-anonymous`, whether an account exists) and the PSK lookup from the accounts reach both server types. | BL-476 |
