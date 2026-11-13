---
id: BL-1102
title: Make the gRPC shutdown-before-any-reply test see its trailers-only reply on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1102 — Make the gRPC shutdown-before-any-reply test see its trailers-only reply on macOS

## Goal

`GrpcStreamHandlerTests.ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable`
passes on CI's Windows, Linux and macOS jobs every run, and a server shutting down before
any reply always sends the trailers-only UNAVAILABLE reply.

## Context

CI run 43404096613 (PR #28, frozen branch `factory/merge-20261113-020606`): the test failed on
"Build and test (macos-latest)" at `Assert.AreEqual(TrailersOnly(14, "server shutting down"), Text(fields))`.
The expected value was `:status: 200 / content-type: application/grpc / grpc-status: 14 /
grpc-message: server shutting down`, and the actual value was empty: no response headers
were written on stream 1 at all. It passed on Windows and Linux. That holds the dark
factory's merge to `master`.

- Test: `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcStreamHandlerTests.cs:589`.
  It uses `ShutDownMidCallAsync` with `HasWrittenAWindowUpdate` as the point at which to shut
  down.
- Production: `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary`, the stream handler's
  shutdown path (ADR-0116).
- Find out first which side is wrong. An empty reply means one of these: shutdown can win a
  race and skip the trailers-only reply (a production bug, so fix it), or the test's
  shutdown point fires before the call is registered (a test race, so choose a deterministic
  point).

## Acceptance criteria

- [ ] The task's Log names the cause: either production skips the trailers-only reply, or the test shuts down too early.
- [ ] A shutdown before any reply on an open call always writes HEADERS carrying `grpc-status: 14` and `grpc-message: server shutting down` with END_STREAM, then GOAWAY NO_ERROR, shown by a test that fails without the fix.
- [ ] The test uses no `Task.Delay`, `Thread.Sleep` or real time.
- [ ] The test passes 50 runs in a row on Windows: `dotnet test Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests --filter "Name~ShutdownBeforeAnyReply"` looped 50 times.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green. Surl.Extended.Protocol.Grpc.UnitLibrary keeps 100% line and branch coverage.

## Notes

Filed by an interactive session on 2026-11-13 after PR #28's macOS job failed. Lanes test only
on Windows, where this passes, so reason about the race rather than rely on reproducing it.

## Log

- 2026-11-13: Created.
