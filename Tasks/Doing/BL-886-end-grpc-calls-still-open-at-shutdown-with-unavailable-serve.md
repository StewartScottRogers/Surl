---
id: BL-886
title: End gRPC calls still open at shutdown with UNAVAILABLE server shutting down
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-443]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-886 — End gRPC calls still open at shutdown with UNAVAILABLE server shutting down

## Goal

A gRPC call still open when the server shuts down ends with trailers `grpc-status: 14`,
`grpc-message: server shutting down` (or `RST_STREAM` `CANCEL` when a reply was cut off mid-write), as
ADR-0116 decision 4 says, instead of the stream being cut off with nothing written.

## Context

- ADR-0116 decision 4 ("Shutdown, `--idle-timeout` and `--max-time`") and the `Grpc.md` rows
  `Grpc:HTTP2-GoAway-2` and `Grpc:HTTP2-Errors-1`.
- Today (BL-443) `Http2ConnectionServer` cancels every stream's token and cuts every write off when the
  exchange is cancelled, so `GrpcStreamHandler` cannot write anything once shutdown starts; it treats the
  cancellation as the client leaving and writes nothing.
- `Surl.Http2` needs a way for a handler to finish within the shutdown grace (ADR-0059): e.g. a
  stream-level shutdown signal (`ExchangeContext.ShutdownToken`) that is separate from the token that cuts
  writes off, so the handler can end its call before the farewell `GOAWAY`. Decide the seam in the task's
  plan, without changing how `Surl.Protocol.Http` and `Surl.Extended.Protocol.DnsQuery` behave.

## Acceptance criteria

- [ ] A named `GrpcStreamHandlerTests` test shuts the exchange down while a `Chat` call is open and
      shows trailers `grpc-status: 14`, `grpc-message: server shutting down` on its stream, followed by
      the connection's `GOAWAY` `NO_ERROR`.
- [ ] A named `Http2ConnectionServerTests` test pins the new seam, and the existing `Surl.Http2`,
      `Surl.Protocol.Http` and DNS over HTTPS tests still pass unchanged.
- [ ] `Grpc.md` rows `Grpc:HTTP2-GoAway-2` and `Grpc:HTTP2-Errors-1` name the tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line
      and branch coverage.

## Notes

Filed by BL-443 (dark factory lane 5), which built the gRPC call handling but could not write after
shutdown without changing `Surl.Http2`, outside its `touches`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
