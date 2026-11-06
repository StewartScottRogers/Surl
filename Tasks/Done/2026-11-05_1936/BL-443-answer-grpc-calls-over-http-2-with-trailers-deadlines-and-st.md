---
id: BL-443
title: Answer gRPC calls over HTTP/2 with trailers, deadlines and status codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-442, BL-409]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-443 — Answer gRPC calls over HTTP/2 with trailers, deadlines and status codes

## Goal

The gRPC server answers calls on HTTP/2 streams through the shared HTTP/2 library. It serves the built-in
service's unary and streaming methods with response headers, messages and `grpc-status` trailers. It answers
errors as Trailers-Only, and enforces request validation, deadlines, compression and authentication.

## Context

- Decision: BL-441's ADR, decisions 1 to 8.
- The HTTP/2 connection: BL-409's library. Add its project reference, which its ADR-0002 row allows.
- PROTOCOL-HTTP2.md: "Responses" (`:status 200`, `content-type`, then `Length-Prefixed-Message`s, then trailers
  with `grpc-status` and `grpc-message`), "Trailers-Only", and `http-grpc-status-mapping.md`.
- Authentication through the `Surl.Protocol.Abstractions` contracts. Time through an injected `TimeProvider`.

## Acceptance criteria

- [x] Named tests, driving HTTP/2 frames through the in-memory connection with HPACK-encoded headers written from
      the documents above, show:
      - a unary call answered with `grpc-status: 0`;
      - a server-streaming call sending three messages;
      - a client-streaming call;
      - an unknown method gets Trailers-Only `UNIMPLEMENTED` (12);
      - a missing `te: trailers` or a wrong `content-type` answered as the ADR says;
      - a `grpc-timeout` of 1 ms that passes gets `DEADLINE_EXCEEDED` (4);
      - a gzip request is answered;
      - an unsupported encoding gets `UNIMPLEMENTED`;
      - an unauthenticated call gets `UNAUTHENTICATED` (16);
      - an over-limit message gets `RESOURCE_EXHAUSTED` (8).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built: `GrpcStreamHandler` (the `IHttp2StreamHandler`, one per connection, given the services, the
  connection's `IHttpAuthenticationSession` and the `ExchangeContext`), `GrpcService`/`GrpcMethod` (the
  extension point: a new service, e.g. BL-444's reflection, is handed in, never switched on), `GrpcCall`,
  `GrpcRequestReader`, `GrpcResponseWriter`, `GrpcDeadline`, and the built-in `EchoService` with
  `EchoRequest`. `Surl.Http2` reference added, as ADR-0116 decision 0 says.
- Tests (`GrpcStreamHandlerTests`) drive `Http2ConnectionServer` over `InMemoryConnection` with
  frames and HPACK headers written from RFC 9113 and PROTOCOL-HTTP2; protobuf bytes from the encoding
  guide. Acceptance mapping: unary `ServeStreamAsync_UnaryEcho_RepliesOnceThenTrailersWithGrpcStatus0`;
  three messages `ServeStreamAsync_ServerStreamingExpandWithCount3_SendsThreeMessagesIndexed0To2`;
  client streaming `ServeStreamAsync_ClientStreamingCollect_...`; unknown method
  `ServeStreamAsync_UnknownServiceOrMethod_IsTrailersOnlyUnimplemented`; te/content-type
  `ServeStreamAsync_MissingTeTrailers_...`, `ServeStreamAsync_ContentTypeNotGrpc_Is415WithNoGrpcStatus`,
  `ServeStreamAsync_GrpcJsonContentType_...`; 1 ms deadline
  `ServeStreamAsync_GrpcTimeoutOf1MillisecondThatPasses_IsDeadlineExceeded`; gzip
  `ServeStreamAsync_CompressedRequest_IsDecompressedAndAnsweredUncompressed`; unsupported encoding
  `ServeStreamAsync_UnsupportedGrpcEncoding_...`; unauthenticated
  `ServeStreamAsync_UnauthenticatedCall_IsUnauthenticatedWithTheChallengeAsMetadata`; over-limit
  `ServeStreamAsync_MessagePastTheMessageLimit_IsResourceExhausted`. 190 tests in the project; line and
  branch coverage of the library measured at 100% with the MSTest coverage collector (cobertura).
- Choices (defaults, within ADR-0116): the deadline test uses a hand-written `ShortTimersOnlyTimeProvider`
  (timers due within 1 ms fire at once, longer ones never), so a 1 ms deadline always passes during a
  60 s delay with no real waiting. A `grpc-timeout` longer than a timer can hold (~49.7 days, up to
  `99999999H`) is treated as no deadline. `delay_milliseconds` is waited before each request's answer,
  including one that ends the call with its `status_code`, and `Collect` waits each request's delay as it
  is read. A `status_code` with an empty `status_message` sends no `grpc-message`. Trailers-Only for an
  unserved `application/grpc+<codec>` carries `content-type: application/grpc`; a served type is echoed
  lowercased without parameters. The refusal delay is the session's own (`Surl.Authentication`), so the
  handler adds none.
- Touches: added `Documentation/Conformance/Extended/Grpc.md` (no task in Doing names it) because
  ADR-0116 says each task replaces its ID there with the named tests; every BL-443 row now names its
  tests, or `Surl.Http2`/BL-445 where the behaviour lives there.
- Filed BL-886: calls still open at shutdown should end `UNAVAILABLE` `server shutting down` (ADR-0116
  decision 4), which needs a `Surl.Http2` seam to write within the grace; today `Http2ConnectionServer`
  cuts writes off at shutdown, so the call gets only the connection's `GOAWAY`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The gRPC server answers unary and streaming surl.test.Echo calls on HTTP/2 with trailers, Trailers-Only errors, deadlines, compression, authentication and limits
