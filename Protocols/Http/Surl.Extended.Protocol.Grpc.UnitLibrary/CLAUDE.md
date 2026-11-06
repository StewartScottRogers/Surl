# Surl.Extended.Protocol.Grpc.UnitLibrary

An extended server (ADR-0083): it answers the `grpc` and `grpcs` schemes (served from `surl` by BL-445) the Curl port adds, which
upstream curl does not have. ADR-0116 decides how. It is judged by its specifications - grpc/grpc's
PROTOCOL-HTTP2.md, statuscodes.md and compression.md, and the protobuf encoding guide - never by the Curl
port's output, and its unit tests replay bytes written by hand from those documents.

The calls on HTTP/2 (BL-443, ADR-0116 decisions 0 to 8), through `Surl.Http2`:

- `GrpcStreamHandler` is the `IHttp2StreamHandler` for one connection, built from the services served,
  the connection's `IHttpAuthenticationSession` and the `ExchangeContext` (clock, log, `--max-message`,
  `--max-filesize`). A wrong `:method` is HTTP `405` and a non-gRPC `content-type` HTTP `415`; then it
  judges, in decision 3's order, the subtype, `te: trailers`, authentication (only Basic and Bearer reach
  the session; `Challenge` is `UNAUTHENTICATED` with `www-authenticate` metadata, `Forbidden`
  `PERMISSION_DENIED`), `grpc-timeout`, `grpc-encoding` and the `:path`, and answers the first failure
  as Trailers-Only. It then serves the method within its deadline (`GrpcDeadline`, on the injected
  `TimeProvider`; `DEADLINE_EXCEEDED` when it passes). A client that resets or leaves gets nothing more,
  logged `CANCELLED`. What it does not do yet: answer calls open at shutdown `UNAVAILABLE` (BL-886).
- `GrpcService` and `GrpcMethod` are what it serves: a method's `IsClientStreaming` decides whether the
  handler reads the whole request first and enforces exactly one message (`request cardinality
  violation`), and its `ServeAsync` answers through a `GrpcCall` and returns the status.
- `GrpcCall` is a call as its method sees it: `ReadRequestAsync` (decompressed, limit-checked, by
  `GrpcRequestReader`) and `WriteReplyAsync` (by `GrpcResponseWriter`, which writes Response-Headers
  before the first reply and ends with Trailers, or Trailers-Only when nothing was written).
- `EchoService` is the built-in `surl.test.Echo`: `Echo`, `Expand`, `Collect`, `Chat`, with `EchoRequest`'s
  `delay_milliseconds`, `status_code`/`status_message` and range checks (`INVALID_ARGUMENT`).
  `EchoService.File` is `surl/test/echo.proto` as a `ProtoFileDescriptor`.

Server reflection (BL-444, ADR-0116 decision 9):

- `ServerReflectionService` is built from the files served (`[EchoService.File]`) and adds its own
  two; its `Services` are `grpc.reflection.v1.ServerReflection` and `…v1alpha.ServerReflection`, each
  with the bidirectional `ServerReflectionInfo`, handed to `GrpcStreamHandler` beside the others. Each
  request read gets one response at once (`valid_host`, `original_request`, then
  `list_services_response`, `file_descriptor_response`, `all_extension_numbers_response` or
  `error_response` `NOT_FOUND` 5 `not found` / `INVALID_ARGUMENT` 3 `no request kind`); the stream
  ends OK at half-close. It keeps no state: each file is sent whole every time.
- `ProtoFileDescriptor`, `ProtoMessageDescriptor`, `ProtoFieldDescriptor`, `ProtoServiceDescriptor`,
  `ProtoMethodDescriptor` and `ProtoFieldType` describe a proto3 file that imports nothing, and write
  its `FileDescriptorProto` with `ProtobufWriter`, never `protoc`. `ServerReflectionFiles` holds the
  two `reflection.proto` files. A new service is described by its own `ProtoFileDescriptor` handed to
  `ServerReflectionService`; a file with imports would need `dependency` (3) and the closure added.

The framing, codec and statuses (BL-442, ADR-0116 decisions 1, 4, 5 and 6) need no HTTP/2:

- `LengthPrefixedMessage` is one Length-Prefixed-Message (Compressed-Flag, 4-byte big-endian length,
  message); `WriteUncompressed` frames a reply, always with Compressed-Flag 0.
- `LengthPrefixedMessageReader` reassembles a call's messages from reads split anywhere. It judges each
  message from its 5-byte prefix before reading it: a Compressed-Flag other than 0 and 1 is
  `request message did not parse` (`INTERNAL`), a length past `--max-message` is
  `message larger than the limit` and the call's lengths together past `--max-filesize`
  `call larger than the limit` (`RESOURCE_EXHAUSTED`). `Complete` refuses a body that ends inside a message.
- `GrpcMessageDecompression` reads `grpc-encoding` (`identity`, `gzip`, `deflate` in the zlib format,
  case-insensitive; anything else `UNIMPLEMENTED` `grpc-encoding not served`) and turns a message into its
  payload: Compressed-Flag 0 as is, 1 under identity `INTERNAL`, decompression stopping one byte past
  `--max-message`. `AcceptEncodingValue` is the `grpc-accept-encoding` every response carries.
- `GrpcTimeout.Read` reads the `grpc-timeout` fields: 1 to 8 ASCII digits and one unit `H M S m u n`;
  none is `null` (an infinite deadline), 0 is a deadline already passed, a nanosecond value rounds up to
  the 100 ns tick, and anything else, or two fields, is `INTERNAL` `malformed grpc-timeout`.
- `GrpcStatusCode` is statuscodes' 0 to 16. `GrpcStatus` is a code and its text; the library's fixed texts
  are its static members, `StatusValue` and `MessageValue` are the `grpc-status` and percent-encoded
  `grpc-message` header values (`GrpcStatusMessage.Encode`), and `FromException` maps any failure:
  a `GrpcCallException` to its own status, a `ProtobufFormatException` to `INTERNAL`
  `request message did not parse`, anything else to `UNKNOWN` `server error`.
- `ProtobufReader` and `ProtobufWriter` are the hand-written protobuf wire codec: varints (at most 10
  bytes), the `VARINT`, `I64`, `LEN` and `I32` wire types, groups read to their matching end (at most 100
  deep) only so they can be skipped, field numbers 1 to 2^29 - 1. `ProtobufField` is one field read, with
  typed accessors (`ReadInt32`, `ReadSInt64`, `ReadString`, `ReadMessage`, `ReadRepeatedVarints` for
  packed or unpacked, …) that refuse the wrong wire type, and its `Encoded` bytes so
  `ProtobufWriter.WriteEncodedField` keeps an unknown field. `ProtobufZigZag` is `sint32`/`sint64`.

Rules:

- No protobuf package and no `protoc`: the codec is hand-written (root `CLAUDE.md`, "Base class library only").
- Every failure that ends a call is a `GrpcCallException` carrying an ADR-0116 status, or a
  `ProtobufFormatException`; never invent a new `grpc-message` text outside `GrpcStatus` and ADR-0116.
- This library references `Surl.Protocol.Abstractions` and `Surl.Http2`, never another protocol server.
- A new service plugs in as a `GrpcService` handed to `GrpcStreamHandler`; never add a switch on service
  names to the handler.
