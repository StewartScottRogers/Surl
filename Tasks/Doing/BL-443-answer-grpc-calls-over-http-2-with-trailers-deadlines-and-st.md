---
id: BL-443
title: Answer gRPC calls over HTTP/2 with trailers, deadlines and status codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-442, BL-409]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Named tests, driving HTTP/2 frames through the in-memory connection with HPACK-encoded headers written from
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
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
