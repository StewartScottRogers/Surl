---
id: BL-1094
title: Answer grpc connections with a GrpcProtocolServer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-444]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1094 — Answer grpc connections with a GrpcProtocolServer

## Goal

`Surl.Extended.Protocol.Grpc.UnitLibrary` has a `GrpcProtocolServer` (an `IConnectionProtocolServer` claiming
`grpc` and `grpcs`) that checks the HTTP/2 prior-knowledge preface and serves `EchoService` and server reflection
over `Http2ConnectionServer` with `GrpcStreamHandler`, so the composition has something to register.

## Context

- Split from BL-445, whose run timed out (ADR-0116 decisions 0-1). That run wrote this task's code and tests as
  untracked files in the stash named `darkfactory BL-445 20261112-162230`. Find its index N with `git stash list`
  (match the message), then take each file (lanes may not run git checkout or git stash), from Git Bash:
  - `git show "stash@{N}^3:Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/GrpcProtocolServer.cs" > Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/GrpcProtocolServer.cs`
  - `git show "stash@{N}^3:Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/AlreadyReadBytesConnection.cs" > Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary/AlreadyReadBytesConnection.cs`
  - `git show "stash@{N}^3:Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcProtocolServerTests.cs" > Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcProtocolServerTests.cs`
  - `git show "stash@{N}^3:Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/AlreadyReadBytesConnectionTests.cs" > Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/AlreadyReadBytesConnectionTests.cs`

  (In PowerShell `>` writes UTF-16; pipe to `Set-Content -Encoding utf8NoBOM <path>` instead.) The run never
  reached the gates, so build, test and measure coverage before trusting them.
- `GrpcProtocolServer.ServeAsync` reads an `Http2ConnectionPreface`; first bytes that are not the preface get
  `GOAWAY` `PROTOCOL_ERROR` (RFC 9113 sections 3.4 and 6.8) and the connection closes. `AlreadyReadBytesConnection`
  replays the bytes read past the preface to `Http2ConnectionServer`.
- Stand-in: the server is stateless (echo and reflection only), uses no shared store, publishes through the
  `ExchangeContext` it is given like the other HTTP/2 handlers, and judges `authorization` through the injected
  `IAuthenticationPolicy`.

## Acceptance criteria

- [ ] `GrpcProtocolServer` exists with `PlainScheme = "grpc"`, `TlsScheme = "grpcs"` and `Schemes` holding both,
      and is an `IConnectionProtocolServer`.
- [ ] A named `GrpcProtocolServerTests` test shows a client that sends the HTTP/2 preface and a unary `Echo` call
      gets the echoed message; another shows first bytes that are not the preface get exactly the 17-byte `GOAWAY`
      `PROTOCOL_ERROR` frame, then writes complete.
- [ ] Named `AlreadyReadBytesConnectionTests` show the already-read bytes are returned before the inner
      connection's bytes, and writes pass through to the inner connection.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Extended.Protocol.Grpc.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
