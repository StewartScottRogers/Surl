---
id: BL-888
title: Bring GrpcTimeout.ReadValue, ProtobufReader.ReadField and EchoRequest.Read to cyclomatic complexity 10 or less
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-888 — Bring GrpcTimeout.ReadValue, ProtobufReader.ReadField and EchoRequest.Read to cyclomatic complexity 10 or less

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Grpc.UnitLibrary` reports no failing member.

## Context

- Found by BL-444 (2026-11-05): the coverage report's complexity column flags `GrpcTimeout.ReadValue(string)`
  (22), `ProtobufReader.ReadField(int)` (13) and `EchoRequest.Read(ReadOnlyMemory<byte>)` (12), all from
  BL-442/BL-443. The build's CA1502 does not stop them, but the quality gate in the root `CLAUDE.md` is at most 10.
- Split each into smaller private methods; behaviour and tests stay as they are.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Grpc.UnitLibrary`
      reports 0 failing members, with 100% line and branch coverage.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
