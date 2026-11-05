---
id: BL-441
title: Decide how the gRPC server answers grpc and grpcs
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-402]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-441 — Decide how the gRPC server answers grpc and grpcs

## Goal

An ADR decides how `surl grpc://` (HTTP/2 prior knowledge) and `surl grpcs://` (HTTP/2 over TLS with ALPN `h2`)
answer gRPC calls: which services and methods exist, how messages are encoded, the status for each failure,
deadlines, compression, server reflection, authentication and limits. A server-side requirement matrix traces
each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - grpc/grpc `doc/PROTOCOL-HTTP2.md` (Requests, Responses, Trailers-Only, `grpc-timeout`, `grpc-encoding`,
    errors, GOAWAY and PING), `doc/statuscodes.md` and `doc/http-grpc-status-mapping.md`;
  - `doc/server-reflection.md` with `src/proto/grpc/reflection/v1/reflection.proto`;
  - the protobuf encoding guide (https://protobuf.dev/programming-guides/encoding/) and `descriptor.proto`
    (FileDescriptorProto, which reflection returns).
- Which calls a client makes (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Grpc.UnitLibrary`
  and its matrices `Grpc.md`, `Grpc-Responses.md`, `Grpc-Transport.md` and `Grpc-Reflection.md`. The client calls
  a method given a `.proto` file or, without one, by reflection, and writes JSON (the ProtoJSON mapping).
  The port's `GrpcReferenceServer` is copyable code.
- HTTP/2 comes from BL-402's library (BL-409). TLS and ALPN follow ADR-0010 and BL-364's ADR.

Decide and record:

1. The services served. Planning assumption: a built-in test service (for example `surl.test.Echo` with a unary
   `Echo`, a server-streaming method and a client-streaming method), described by reflection. Also decide whether
   a user may serve methods from a `.proto` file given on the command line, and if so how replies are produced.
2. Unary and streaming support (reflection itself is bidirectional streaming).
3. Request validation: `:method POST`, `content-type: application/grpc[+proto]`, `te: trailers`, and the
   response to each violation (HTTP `415`, or the status the mapping document gives).
4. Trailers-Only responses for errors, `grpc-status`, and `grpc-message` percent-encoding.
5. `grpc-timeout` parsing and `DEADLINE_EXCEEDED` on an injected `TimeProvider`.
6. `grpc-encoding`/`grpc-accept-encoding`: identity and gzip (BCL `GZipStream`), and `UNIMPLEMENTED` for others.
7. Authentication: secure-by-default (bearer or Basic in `authorization` metadata, from the accounts file), with
   `UNAUTHENTICATED` and `PERMISSION_DENIED`, and anonymous only under `--allow-anonymous`.
8. Limits: the largest message (`RESOURCE_EXHAUSTED`), concurrent calls, and each ADR-0006 limit.
9. Reflection v1 (and whether v1alpha is answered): `list_services`, `file_containing_symbol`,
   `file_by_filename`, and `error_response` for unknown names.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 9, citing the document section behind each,
      and is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Grpc.md` exists in the matrix format with every server-binding
      requirement of PROTOCOL-HTTP2.md and server-reflection.md, each "covered by" naming BL-442,
      BL-443 or BL-444, or "n/a: <reason>".

## Notes

- Decided in ADR-0116 (next free number on 2026-11-05; ADR-0115 was the last). Specifications read
  2026-11-05 at grpc/grpc `master`: PROTOCOL-HTTP2.md, statuscodes.md, http-grpc-status-mapping.md,
  compression.md, server-reflection.md with reflection.proto, and the protobuf encoding guide.
- The port's client was read only for what it sends: Request-Headers in PROTOCOL-HTTP2 order with
  `te: trailers` and `content-type: application/grpc`, `-H` values as metadata (so `authorization`
  arrives that way), one uncompressed message, and reflection v1 `file_containing_symbol` for the
  service then `file_by_filename` for missing dependencies.
- Choices with a sensible default, taken: the built-in service is `surl.test.Echo` with four
  methods (one per call shape) and request fields that select any status and a delay, so tests
  can reach every status and deadline; no user `.proto` services (replies would need a language no
  gRPC document defines); `deflate` served beside `gzip` (BCL `ZLibStream`, costs nothing);
  replies never compressed (compression.md allows it); reflection v1alpha answered too (older
  clients); missing `te` refused with `INTERNAL`; wrong `:method` HTTP `405`; `grpc-timeout` 0 read
  as an expired deadline. Each is argued in the ADR.
- Matrix rows that `Surl.Http2` (BL-409) or BL-445's ALPN offer already prove are marked `n/a`
  with that reason, since the task allows only BL-442, BL-443 or BL-444 as "covered by".
- BL-445 should put `surl/test/echo.proto`'s text in `surl --aihelp grpc` (ADR-0116 decision 1);
  its task already owns the topic, so no new task was filed.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0116 decides how grpc and grpcs answer gRPC calls and reflection; Grpc.md traces every server-side requirement to BL-442, BL-443 or BL-444
