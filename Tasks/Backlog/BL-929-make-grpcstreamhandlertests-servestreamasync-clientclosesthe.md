---
id: BL-929
title: Make GrpcStreamHandlerTests.ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled deterministic
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-929 — Make GrpcStreamHandlerTests.ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled deterministic

## Goal

`ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled` passes on every run on
every runner, because either the handler or the test no longer races the connection close against the
reply headers.

## Context

- Failed once on the `Publish linux-arm64` leg of the `workflow_dispatch` Release run
  https://github.com/StewartScottRogers/Surl/actions/runs/42222500303 (master 8eb0301b):
  `Assertion failed. Expected collection to be empty.` at `Assert.IsEmpty(fields)` - header fields
  were written before the handler saw the client close the connection mid-message.
- The test is in `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcStreamHandlerTests.cs`;
  it sends a `Chat` call and only six bytes of a message, then the replayed input ends.
- Decide whether the server sending reply headers before the first full request message on a
  bidirectional call is correct (then the test is wrong) or not (then the handler is wrong), from the
  gRPC over HTTP/2 specification, and make the outcome deterministic. A failing test that reproduces
  the race comes first (for example a fake that delays the end of input).

## Acceptance criteria

- [ ] The test (or its corrected form) passes 200 times in a row locally:
      `dotnet test Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests --filter "Name~ClientClosesTheConnectionMidCall"` in a loop.
- [ ] Notes record which side was wrong and why, from the gRPC over HTTP/2 specification.

## Notes

- Filed by BL-603 (lane 9) after the flake failed a release dry run.

## Log

- 2026-11-06: Created.
