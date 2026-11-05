# Surl.Extended.Protocol.Grpc.UnitLibrary

An extended server (ADR-0083): it will answer the `grpc` and `grpcs` schemes the Curl port adds, which
upstream curl does not have. ADR-0116 decides how. It is judged by its specifications - grpc/grpc's
PROTOCOL-HTTP2.md, statuscodes.md and compression.md, and the protobuf encoding guide - never by the Curl
port's output, and its unit tests replay bytes written by hand from those documents.

What is here so far (BL-442, ADR-0116 decisions 1, 4, 5 and 6) needs no HTTP/2; BL-443 adds the reference
to `Surl.Http2` and the server itself:

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
- This library references `Surl.Protocol.Abstractions` (and, from BL-443, `Surl.Http2`), never another
  protocol server.
