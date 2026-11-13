# gRPC requirement matrix

The server-side requirements `Surl.Extended.Protocol.Grpc` answers, for the `grpc` and `grpcs`
schemes (ADR-0083 decision 9). Written from grpc/grpc's `doc/PROTOCOL-HTTP2.md` ("gRPC over HTTP2"),
`doc/statuscodes.md`, `doc/compression.md` and `doc/server-reflection.md` with
`src/proto/grpc/reflection/v1/reflection.proto`, and the protobuf encoding guide
(<https://protobuf.dev/programming-guides/encoding/>), read 2026-11-05 at `master`, the
specifications [ADR-0116](../../Planning/Decisions/ADR-0116-how-the-grpc-server-answers-grpc-and-grpcs.md)
decides by, never from the Curl port's client matrices. Each row binds the server; "Covered by"
names the task that proves it, until that task replaces its ID with the named test, or says why the
row needs no test here.

The documents state few requirements in RFC keywords, so each header a server reads or sends and
each status it owes is a row. IDs are `Grpc:HTTP2-<section>-<n>` for PROTOCOL-HTTP2.md,
`Grpc:Status-<n>` for statuscodes.md, `Grpc:Compression-<n>` for compression.md,
`Grpc:Reflection-<n>` for server-reflection.md and reflection.proto, `Grpc:Protobuf-<n>` for the
encoding guide, and `Grpc:Surl-<Decision>-<n>` for a rule ADR-0116 adds where the documents leave
the server a choice. Client-only requirements (User-Agent, idempotency and retries, the client's
own handling of statuses and resets) are not rows.

## Requests (PROTOCOL-HTTP2.md "Requests")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:HTTP2-Requests-1 | MUST | A request is Request-Headers, zero or more Length-Prefixed-Messages, then the client's end of stream; the server reads its messages until `END_STREAM`. | `GrpcStreamHandlerTests.ServeStreamAsync_ClientStreamingCollect_RepliesOnceWithTheMessagesConcatenatedAndTheirCount` |
| Grpc:HTTP2-Requests-2 | SHOULD | A `content-type` not beginning with `application/grpc` is answered HTTP `415` with no `grpc-status`. | `GrpcStreamHandlerTests.ServeStreamAsync_ContentTypeNotGrpc_Is415WithNoGrpcStatus` |
| Grpc:HTTP2-Requests-3 | MUST | `application/grpc` and `application/grpc+proto` are served; another `application/grpc+<codec>` is Trailers-Only `UNIMPLEMENTED` (ADR-0116 decision 3). | `GrpcStreamHandlerTests.ServeStreamAsync_UnaryEchoWithUnknownFieldAndContentTypeParameter_SkipsTheFieldAndAnswersInGrpcProto`, `ServeStreamAsync_GrpcJsonContentType_IsTrailersOnlyUnimplementedContentTypeNotServed` |
| Grpc:HTTP2-Requests-4 | MUST | A request without `te: trailers` is Trailers-Only `INTERNAL`, `grpc-message` `te: trailers required` (ADR-0116 decision 3). | `GrpcStreamHandlerTests.ServeStreamAsync_MissingTeTrailers_IsTrailersOnlyInternalTeTrailersRequired` |
| Grpc:HTTP2-Requests-5 | MUST | `:path` `/<service>/<method>` selects the method; an unknown service or method is Trailers-Only `UNIMPLEMENTED` (12), judged after authentication. | `GrpcStreamHandlerTests.ServeStreamAsync_UnknownServiceOrMethod_IsTrailersOnlyUnimplemented`, `ServeStreamAsync_SeveralFailures_AnswersTheFirstInDecision3sOrder` |
| Grpc:HTTP2-Requests-6 | MUST | Custom-Metadata, ASCII and `-bin`, is accepted and never fails the call, never echoed. | `GrpcStreamHandlerTests.ServeStreamAsync_CustomMetadataAsciiAndBinary_IsAcceptedAndNeverEchoed` |
| Grpc:HTTP2-Requests-7 | MAY | Request-Headers are bounded; the bound is `Surl.Http2`'s `MAX_HEADER_LIST_SIZE` (`--max-message`), not the suggested 8 KiB (ADR-0116 decision 3). | n/a: `Surl.Http2` enforces it and its own tests prove it (BL-409, ADR-0081 decision 5); the gRPC server adds nothing |
| Grpc:HTTP2-Timeout-1 | MUST | `grpc-timeout` is 1 to 8 ASCII digits and one unit of `H M S m u n`; each unit is read at its scale. | `GrpcTimeoutTests.Read_EachUnit_IsReadAtItsScale` |
| Grpc:HTTP2-Timeout-2 | MUST | A malformed `grpc-timeout` (9 digits, no unit, two units, a sign, two fields) is Trailers-Only `INTERNAL`, `grpc-message` `malformed grpc-timeout`; a value of 0 is a deadline already passed (ADR-0116 decision 5). | `GrpcTimeoutTests.Read_MalformedValue_ThrowsInternalMalformedGrpcTimeout`, `GrpcTimeoutTests.Read_TwoFields_ThrowsInternalMalformedGrpcTimeout` and `Read_EachUnit_IsReadAtItsScale` (`0S`) (parsing); `GrpcStreamHandlerTests.ServeStreamAsync_MalformedGrpcTimeout_IsTrailersOnlyInternal`, `ServeStreamAsync_GrpcTimeoutOf0_IsDeadlineExceededAtOnce` (the answer) |
| Grpc:HTTP2-Timeout-3 | SHOULD | Without `grpc-timeout` the server assumes an infinite timeout (bounded only by `--max-time`). | `GrpcStreamHandlerTests.ServeStreamAsync_UnaryEcho_RepliesOnceThenTrailersWithGrpcStatus0`; `ServeStreamAsync_GrpcTimeoutThatDoesNotPass_IsServed` (`99999999H`, past any timer, is no deadline) |
| Grpc:HTTP2-Timeout-4 | MUST | A deadline that passes ends the call `DEADLINE_EXCEEDED` (4): Trailers-Only if nothing was written, otherwise trailers after the last whole message; measured on the injected `TimeProvider`. | `GrpcStreamHandlerTests.ServeStreamAsync_GrpcTimeoutOf1MillisecondThatPasses_IsDeadlineExceeded` (Trailers-Only); after a message the status goes in the trailers as any status does (`ServeStreamAsync_ExpandWithAStatusCode_SendsItInTheTrailersAfterTheMessages`) |
| Grpc:HTTP2-Message-1 | MUST | A Length-Prefixed-Message is a 1-byte Compressed-Flag, a 4-byte big-endian length and the message, read however its bytes are split across `DATA` frames. | `LengthPrefixedMessageReaderTests.TryRead_MessageSplitAcrossReadsByteByByte_ReassemblesIt`, `TryRead_TwoMessagesInOneReadAndOneEmpty_ReadsEachInOrder`; `LengthPrefixedMessageTests` |
| Grpc:HTTP2-Message-2 | MUST | A Compressed-Flag other than 0 or 1 is `INTERNAL` (the message does not parse). | `LengthPrefixedMessageReaderTests.TryRead_CompressedFlagOtherThanZeroOrOne_ThrowsInternal` |

## Responses (PROTOCOL-HTTP2.md "Responses")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:HTTP2-Responses-1 | MUST | Response-Headers are `:status 200`, `content-type` (the request's gRPC type), and `grpc-accept-encoding`, sent only when a message follows. | `GrpcStreamHandlerTests.ServeStreamAsync_UnaryEcho_RepliesOnceThenTrailersWithGrpcStatus0`, `ServeStreamAsync_UnaryEchoWithUnknownFieldAndContentTypeParameter_SkipsTheFieldAndAnswersInGrpcProto` |
| Grpc:HTTP2-Responses-2 | MUST | Each reply is a Length-Prefixed-Message with Compressed-Flag 0. | `GrpcStreamHandlerTests.ServeStreamAsync_ServerStreamingExpandWithCount3_SendsThreeMessagesIndexed0To2` |
| Grpc:HTTP2-Responses-3 | MUST | Trailers are a `HEADERS` frame with `END_STREAM` after the messages, carrying `grpc-status` even when it is 0, and `grpc-message` when it is not. | `GrpcStreamHandlerTests.ServeStreamAsync_UnaryEcho_RepliesOnceThenTrailersWithGrpcStatus0`, `ServeStreamAsync_ExpandWithAStatusCode_SendsItInTheTrailersAfterTheMessages` |
| Grpc:HTTP2-Responses-4 | MUST | A call that ends before any message is sent is Trailers-Only: one `HEADERS` frame with `END_STREAM` holding `:status 200`, `content-type`, `grpc-status`, `grpc-message` and `grpc-accept-encoding`. | `GrpcStreamHandlerTests.ServeStreamAsync_EchoWithAStatusCodeAndMessage_IsTrailersOnlyWithThePercentEncodedMessage` |
| Grpc:HTTP2-Responses-5 | MUST | `grpc-message` is UTF-8 percent-encoded: each byte outside `%x20-%x7E`, and `%`, as `%` and two upper-case hex digits. | `GrpcStatusMessageTests.Encode_Text_PercentEncodesEveryByteOutsidePrintableAsciiAndPercent` |
| Grpc:HTTP2-Responses-6 | MUST | `grpc-status` is the decimal code 0 to 16. | `GrpcStatusTests.StatusValue_Code_IsItsDecimalNumber`, `GrpcStatusTests.StatusCodes_AreTheSeventeenOfStatuscodes` |
| Grpc:HTTP2-Responses-7 | MUST | After the trailers, a stream whose client half is still open is reset `NO_ERROR` (RFC 9113 section 8.1). | n/a: `Http2ConnectionServer` resets the stream when its handler returns with the client's half open, proved in its own tests (BL-409); `GrpcStreamHandler` returns after its trailers |

## HTTP/2 transport mapping (PROTOCOL-HTTP2.md "HTTP2 Transport Mapping")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:HTTP2-Errors-1 | SHOULD | The server sends `RST_STREAM` `CANCEL` only when a reply message was cut off mid-write (deadline or shutdown); otherwise a call ends with trailers. | `GrpcStreamHandlerTests.ServeStreamAsync_ShutdownWhileAReplyWaitsForWindow_ResetsTheStreamCancel`, `ServeStreamAsync_ShutdownWhileAChatCallIsOpen_EndsItUnavailableServerShuttingDownThenGoAwayNoError` (a call cut off between replies ends with trailers); `Http2ConnectionServerTests.ServeAsync_ShutdownWhileAHandlerWaits_LetsItWriteAFarewellResetBeforeTheGoAway` (ADR-0116 amendment 1) |
| Grpc:HTTP2-Errors-2 | MUST | A client's `RST_STREAM` ends the call at once; nothing more is written on the stream. | `GrpcStreamHandlerTests.ServeStreamAsync_ClientResetsTheStream_WritesNothingMoreAndLogsCancelled` |
| Grpc:HTTP2-Errors-3 | MUST | A call past `MAX_CONCURRENT_STREAMS` (100) is reset `REFUSED_STREAM`, which a client maps to `UNAVAILABLE`. | n/a: `Surl.Http2` refuses the stream before the gRPC server sees it, proved in its own tests (BL-409, ADR-0081 decision 5) |
| Grpc:HTTP2-GoAway-1 | SHOULD | Before closing a connection (shutdown, `--idle-timeout`, `--max-time`) the server sends `GOAWAY` naming the last stream processed, `NO_ERROR`. | n/a: `Surl.Http2` sends it, proved in its own tests (BL-409, ADR-0081 decision 5) |
| Grpc:HTTP2-GoAway-2 | MUST | Calls in flight at shutdown finish within the grace; any still open end `UNAVAILABLE` (14), `grpc-message` `server shutting down`. | `GrpcStreamHandlerTests.ServeStreamAsync_ShutdownWhileAChatCallIsOpen_EndsItUnavailableServerShuttingDownThenGoAwayNoError`, `ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable`; `Http2ConnectionServerTests.ServeAsync_ShutdownWhileAHandlerWaits_LetsItWriteFarewellTrailersBeforeTheGoAway` (ADR-0116 amendment 1) |
| Grpc:HTTP2-Ping-1 | MUST | A client's `PING` is answered by echoing its payload exactly. | n/a: `Surl.Http2` answers `PING`, and no handler sees it (ADR-0086 decision 1), proved in its own tests (BL-409) |
| Grpc:HTTP2-Connection-1 | MUST | When the connection fails, the server's open calls are cancelled. | `GrpcStreamHandlerTests.ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled` |
| Grpc:HTTP2-Security-1 | MUST | `grpc` takes HTTP/2 prior knowledge only: a first read that is not the preface is `GOAWAY` `PROTOCOL_ERROR`, then close; no `Upgrade: h2c`. | `GrpcProtocolServerTests.ServeAsync_NotThePreface_AnswersGoAwayProtocolErrorAndCloses`; `GrpcProtocolServerTests.ServeAsync_PrefaceThenUnaryEcho_AnswersTheCall`, `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Echo_DescribedByAProtoFile_RepliesWithTheMessage` (the live client connects by prior knowledge) |
| Grpc:HTTP2-Security-2 | MUST | `grpcs` is TLS 1.2 or 1.3 from the first byte, ALPN offering exactly `h2`; a client offering no ALPN that sends the preface is served. | `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Echo_OverTls_RepliesWithTheMessage` (the live client's ALPN `h2` over TLS); the listener's ALPN offer of exactly `h2` is pinned by BL-1095's `Surl.Networking.UnitTests` test |

## Status codes (statuscodes.md)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:Status-1 | MUST | A method not found at the server is `UNIMPLEMENTED` (12). | `GrpcStreamHandlerTests.ServeStreamAsync_UnknownServiceOrMethod_IsTrailersOnlyUnimplemented` |
| Grpc:Status-2 | MUST | A request message that does not parse is `INTERNAL` (13), `grpc-message` `request message did not parse`. | `GrpcStatusTests.FromException_ProtobufFormatException_ReturnsInternalRequestMessageDidNotParse` (the map); `GrpcStreamHandlerTests.ServeStreamAsync_EchoWhoseMessageIsNotProtobuf_IsInternalRequestMessageDidNotParse`, `ServeStreamAsync_BodyEndingInsideAMessage_IsInternalRequestMessageDidNotParse` (the answer) |
| Grpc:Status-3 | MUST | A unary or server-streaming call with zero or more than one request message is `UNIMPLEMENTED` (12), `grpc-message` `request cardinality violation`. | `GrpcStreamHandlerTests.ServeStreamAsync_UnaryWithNoRequestMessage_IsUnimplementedRequestCardinalityViolation`, `ServeStreamAsync_UnaryWithTwoRequestMessages_IsUnimplementedRequestCardinalityViolation` |
| Grpc:Status-4 | MUST | A message larger than `--max-message` (from its prefix, or once decompressed) is `RESOURCE_EXHAUSTED` (8); so is a call's request total past `--max-filesize` and a reply past `--max-message`. | `LengthPrefixedMessageReaderTests.TryRead_MessageLengthOverTheLimit_ThrowsResourceExhaustedFromThePrefixAlone`, `GrpcMessageDecompressionTests.ReadPayload_DecompressedPastTheLimit_ThrowsResourceExhausted` and `LengthPrefixedMessageReaderTests.TryRead_MessagesTogetherOverTheCallLimit_ThrowsResourceExhausted` (the message and the call's total); `GrpcStreamHandlerTests.ServeStreamAsync_MessagePastTheMessageLimit_IsResourceExhausted`, `ServeStreamAsync_CallPastTheUploadLimit_IsResourceExhaustedCallLargerThanTheLimit`, `ServeStreamAsync_CollectPastTheMessageLimit_IsResourceExhaustedReplyLargerThanTheLimit` (each answered, and the reply) |
| Grpc:Status-5 | MUST | Incorrect auth metadata is `UNAUTHENTICATED` (16). | `GrpcStreamHandlerTests.ServeStreamAsync_UnauthenticatedCall_IsUnauthenticatedWithTheChallengeAsMetadata` |
| Grpc:Status-6 | MUST | An exception the service did not expect is `UNKNOWN` (2), `grpc-message` `server error`. | `GrpcStreamHandlerTests.ServeStreamAsync_MethodThatThrowsUnexpectedly_IsUnknownServerErrorAndLogged` |
| Grpc:Status-7 | MUST NOT | The library never generates `INVALID_ARGUMENT`, `NOT_FOUND`, `ALREADY_EXISTS`, `FAILED_PRECONDITION`, `ABORTED`, `OUT_OF_RANGE` or `DATA_LOSS`; only the `surl.test.Echo` service (by request) and reflection's `error_response` send them. | `GrpcStatusTests.LibraryStatuses_AreTheCodesAndTextsADR0116Names`, `GrpcStatusTests.FromException_AnyOtherException_ReturnsUnknownServerError` (the status map); `GrpcStreamHandlerTests.ServeStreamAsync_EchoWithAFieldOutOfRange_IsInvalidArgument` (the service's `INVALID_ARGUMENT`, not the library's) |

## Compression (compression.md)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:Compression-1 | MUST | `grpc-encoding` `identity`, `gzip` and `deflate` (zlib) requests are read; a compressed message is decompressed. | `GrpcMessageDecompressionTests.ReadEncoding_ServedEncoding_ReturnsIt`, `ReadPayload_GzipFlaggedMessage_IsDecompressed`, `ReadPayload_DeflateFlaggedMessage_IsDecompressedFromTheZlibFormat` (decompression); `GrpcStreamHandlerTests.ServeStreamAsync_CompressedRequest_IsDecompressedAndAnsweredUncompressed` (gzip and deflate calls answered) |
| Grpc:Compression-2 | MUST | Another `grpc-encoding` is Trailers-Only `UNIMPLEMENTED` with `grpc-accept-encoding: identity,deflate,gzip`, which never names the refused encoding. | `GrpcStreamHandlerTests.ServeStreamAsync_UnsupportedGrpcEncoding_IsUnimplementedWithGrpcAcceptEncoding` |
| Grpc:Compression-3 | MUST | Compressed-Flag 1 without a `grpc-encoding` (or with `identity`) is `INTERNAL`, `grpc-message` `compressed flag set without grpc-encoding`. | `GrpcMessageDecompressionTests.ReadPayload_CompressedFlagUnderIdentity_ThrowsInternal` |
| Grpc:Compression-4 | MUST | A message that does not decompress under a served encoding is `INTERNAL`, `grpc-message` `message did not decompress`. | `GrpcMessageDecompressionTests.ReadPayload_BytesThatDoNotDecompress_ThrowsInternal` |
| Grpc:Compression-5 | MUST | Every response's `grpc-accept-encoding` lists each encoding the server reads; replies are sent uncompressed with no `grpc-encoding`. | `GrpcStreamHandlerTests.ServeStreamAsync_CompressedRequest_IsDecompressedAndAnsweredUncompressed` |

## Protobuf (encoding guide)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:Protobuf-1 | MUST | Varints, zigzag, the `I64`, `LEN` and `I32` wire types, and packed repeated fields encode and decode as the guide's worked examples show. | `ProtobufEncodingGuideExampleTests`; `ProtobufReaderTests.TryReadField_Fixed64AndFixed32_ReadsThemLittleEndian` |
| Grpc:Protobuf-2 | MUST | Unknown fields are skipped; a known field with the wrong wire type, a truncated varint or length, or a non-UTF-8 string is a parse failure. | `ProtobufReaderTests.TryReadField_UnknownFields_AreKeptByWritingThemBack`, `TryReadField_BytesThatAreNotAField_ThrowsProtobufFormatException`, `ReadAccessors_KnownFieldWithTheWrongWireType_ThrowProtobufFormatException`, `ReadString_BytesThatAreNotUtf8_ThrowsProtobufFormatException` |

## Server reflection (server-reflection.md; reflection.proto)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:Reflection-1 | MUST | `grpc.reflection.v1.ServerReflection/ServerReflectionInfo` and its `v1alpha` twin are bidirectional streams answering one response per request, in order, each as its request is read. | `ServerReflectionServiceTests.ServerReflectionInfo_ThreeRequestsOnOneStream_GetThreeResponsesInOrder`; `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Echo_DescribedByReflection_RepliesWithTheMessage` (a live client reflects, then calls) |
| Grpc:Reflection-2 | MUST | Each response carries `valid_host` and `original_request`; an `error_response` does not end the stream, which ends with trailers OK on the client's half-close. | `ServerReflectionServiceTests.Answer_Host_IsEchoedAsValidHostBeforeTheOriginalRequest`, `ServerReflectionInfo_FileByFilenameUnknown_IsErrorResponseNotFoundAndTheStreamStillEndsOk` |
| Grpc:Reflection-3 | MUST | `list_services` names `grpc.reflection.v1.ServerReflection`, `grpc.reflection.v1alpha.ServerReflection` and `surl.test.Echo`, in ordinal order. | `ServerReflectionServiceTests.ServerReflectionInfo_ListServices_NamesTheBuiltInServiceAndBothReflectionServicesInOrdinalOrder` |
| Grpc:Reflection-4 | MUST | `file_by_filename` returns the named file and all its transitive dependencies, the named file first. | `ServerReflectionServiceTests.Answer_FileByFilenameForReflectionV1_DescribesItsOneofsRepeatedFieldsAndMessageTypes`; `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Echo_DescribedByReflection_RepliesWithTheMessage` |
| Grpc:Reflection-5 | MUST | `file_containing_symbol` returns the file declaring a fully qualified service, method, message or field, with its transitive dependencies; a leading `.` is accepted. | `ServerReflectionServiceTests.ServerReflectionInfo_FileContainingSymbolForTheBuiltInService_ReturnsEchoProtoWithItsServiceMethodsAndFields`; `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Echo_DescribedByReflection_RepliesWithTheMessage` |
| Grpc:Reflection-6 | MUST | Every request results in an answer or an error: an unknown file or symbol, and `file_containing_extension`, is `error_response` `NOT_FOUND` (5); a request with no kind set is `INVALID_ARGUMENT` (3). | `ServerReflectionServiceTests.Answer_FileContainingExtension_IsErrorResponseNotFound`, `Answer_NoRequestKind_IsErrorResponseInvalidArgument`, `Answer_UnknownName_IsErrorResponseNotFound` |
| Grpc:Reflection-7 | MUST | `all_extension_numbers_of_type` for a known message answers its `base_type_name` with no numbers; for an unknown one, `NOT_FOUND`. | `ServerReflectionServiceTests.Answer_AllExtensionNumbersOfAKnownType_IsItsNameAndNoNumbers`, `Answer_UnknownName_IsErrorResponseNotFound` |
| Grpc:Reflection-8 | MAY | A server may skip files already sent on the stream; this server always sends the whole closure (ADR-0116 decision 9). | n/a: the server never skips a file, so there is nothing to observe beyond Grpc:Reflection-4's closure |
| Grpc:Reflection-9 | MUST | The `FileDescriptorProto` of `surl/test/echo.proto` decodes to ADR-0116 decision 1's package, messages, fields and service with each method's streaming flags; the reflection files describe reflection.proto's messages, `oneof`s included. | `ServerReflectionServiceTests.ServerReflectionInfo_FileContainingSymbolForTheBuiltInService_ReturnsEchoProtoWithItsServiceMethodsAndFields`, `Answer_FileByFilenameForReflectionV1_DescribesItsOneofsRepeatedFieldsAndMessageTypes` |

## Rules ADR-0116 adds

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Grpc:Surl-Method-1 | MUST | A `:method` other than `POST` is HTTP `405` with `allow: POST`, no body and no `grpc-status`. | `GrpcStreamHandlerTests.ServeStreamAsync_Get_Is405WithAllowPostAndNoGrpcStatus` |
| Grpc:Surl-Echo-1 | MUST | `Echo` replies once with `message` copied and `index` 0; `Expand` sends `count` replies indexed from 0, then its status; `Collect` replies once with the messages concatenated and their count; `Chat` replies to each request as it arrives. | `GrpcStreamHandlerTests.ServeStreamAsync_UnaryEcho_RepliesOnceThenTrailersWithGrpcStatus0`, `ServeStreamAsync_ServerStreamingExpandWithCount3_SendsThreeMessagesIndexed0To2`, `ServeStreamAsync_ClientStreamingCollect_RepliesOnceWithTheMessagesConcatenatedAndTheirCount`, `ServeStreamAsync_BidirectionalChat_RepliesToEachRequestIndexedByPosition` |
| Grpc:Surl-Echo-2 | MUST | A request's `status_code` 1 to 16 ends the call with that status and `status_message` (Trailers-Only when nothing was sent); `delay_milliseconds` waits on the `TimeProvider` before each reply. | `GrpcStreamHandlerTests.ServeStreamAsync_EchoWithAStatusCodeAndMessage_IsTrailersOnlyWithThePercentEncodedMessage`, `ServeStreamAsync_CollectWithAStatusCode_EndsAtOnceAsTrailersOnly`, `ServeStreamAsync_ChatWithAStatusCodeAfterAReply_EndsWithItInTheTrailers`; `ServeStreamAsync_BidirectionalChat_RepliesToEachRequestIndexedByPosition` and `ServeStreamAsync_GrpcTimeoutOf1MillisecondThatPasses_IsDeadlineExceeded` (the delay) |
| Grpc:Surl-Echo-3 | MUST | `count` above 1000, `status_code` outside 0 to 16, or `delay_milliseconds` above 60000 is `INVALID_ARGUMENT`, `grpc-message` `request field out of range`. | `GrpcStreamHandlerTests.ServeStreamAsync_EchoWithAFieldOutOfRange_IsInvalidArgument` |
| Grpc:Surl-Auth-1 | MUST | Without `--allow-anonymous` every call, reflection included, needs `authorization` `Basic` (an account) or `Bearer` (an empty-name account's password); none is `UNAUTHENTICATED` with the session's `www-authenticate` values, after the refusal delay. | `GrpcStreamHandlerTests.ServeStreamAsync_UnauthenticatedCall_IsUnauthenticatedWithTheChallengeAsMetadata`, `ServeStreamAsync_BearerTokenTheSessionAccepts_IsServedAndTheSessionSeesTheAuthorization`; the refusal delay is the session's (`Surl.Authentication`, ADR-0032 section 8) |
| Grpc:Surl-Auth-2 | MUST | Another `authorization` scheme is `UNAUTHENTICATED` without asking the session; a `Forbidden` verdict is `PERMISSION_DENIED` (7); neither text names the account or the cause. | `GrpcStreamHandlerTests.ServeStreamAsync_AuthorizationNeitherBasicNorBearer_IsUnauthenticatedWithoutAskingTheSession`, `ServeStreamAsync_ForbiddenCall_IsPermissionDenied` |
| Grpc:Surl-Auth-3 | MUST | On `grpc` credentials are refused unless `--allow-plaintext-auth`; under `--allow-anonymous` a call without credentials is served and one with bad credentials is still refused. | `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Credentials_OverPlainText_AreRefusedUnlessPlaintextAuthIsAllowed`, `CurlPortClientCallsMethodsOnSurlOverGrpcTests.Call_WithoutCredentials_IsUnauthenticated` |
| Grpc:Surl-Order-1 | MUST | Checks run in ADR-0116 decision 3's order (method, content-type, codec, `te`, authentication, `grpc-timeout`, `grpc-encoding`, path), and the first failure is the answer. | `GrpcStreamHandlerTests.ServeStreamAsync_SeveralFailures_AnswersTheFirstInDecision3sOrder` |
