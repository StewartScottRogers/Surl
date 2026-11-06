---
id: BL-928
title: Stop GrpcStreamHandlerTests mid-call cancellation test failing on linux-arm64
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Extended.Protocol.Grpc.UnitTests, Surl.Extended.Protocol.Grpc.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-928 — Stop GrpcStreamHandlerTests mid-call cancellation test failing on linux-arm64

## Goal

`GrpcStreamHandlerTests.ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled` passes on every run on every runner, linux-arm64 included.

## Context

- It failed in release.yml's `Publish linux-arm64` leg (fast tests) of run https://github.com/StewartScottRogers/Surl/actions/runs/42222500303 (master, `8eb0301b`, 25 ms) while passing on every other leg and on Windows locally, so it is timing-dependent: likely a race between the fake connection closing and the handler's write or log.
- Start at `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests/GrpcStreamHandlerTests.cs`. Fix the race, not the timeout; time comes from `TimeProvider`.
- Found by BL-688.

## Acceptance criteria

- [ ] The cause of the linux-arm64 failure is named in Notes, and the test or handler no longer depends on thread scheduling.
- [ ] The test passes 200 times in a row locally (`dotnet test --filter` in a loop).

## Notes

## Log

- 2026-11-06: Created.
