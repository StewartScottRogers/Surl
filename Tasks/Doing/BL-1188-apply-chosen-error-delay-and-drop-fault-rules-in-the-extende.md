---
id: BL-1188
title: Apply chosen-error, delay and drop fault rules in the extended gRPC server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1188 — Apply chosen-error, delay and drop fault rules in the extended gRPC server

## Goal

An `error`, `delay;stage=command` or `drop;stage=command` fault rule naming `grpc` or `grpcs` (or a listener of one) makes the gRPC server answer the matching call with the chosen status, hold it, or cut it off - instead of `--fault` refusing the `error` with "scheme grpc has no error shape" and the server ignoring delay and drop.

## Context

- Stand-in audit 2026-11-14: `Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary` never asks `ExchangeContext.Faults`, and `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs` has no `grpc`/`grpcs` row. BL-837's audit (2026-11-12) left them to "the serve task", which did not add them; ADR-0124 decision 9 says every server wires `error` and adds its row.
- Decided by Claude under Stewart's delegation in this task, as BL-1062 did for STOMP, and recorded as ADR-0124 decision 4's row: the reply is a gRPC status code 1 to 16 (never 0, `OK`) and an optional message, sent as `grpc-status` and `grpc-message` in a trailers-only response (gRPC over HTTP/2 protocol document); `command` is the method path's method name and `path` the full `/<service>/<method>`. The task may refine this and says so in the row.
- Delay and drop: `CommandStageFaults.FindAndApplyAsync` (ADR-0141) at the same point; a drop aborts the HTTP/2 connection. ADR-0116 is the gRPC server's decision record; judged by the gRPC specification (ADR-0083).

## Acceptance criteria

- [ ] Named tests: a call matching an `error` rule gets a trailers-only response with exactly the chosen `grpc-status` and `grpc-message`; a non-matching call is served; one `FaultAppliedEvent`.
- [ ] Named tests: `delay;stage=command` holds the matching call for `ms` on a fake `TimeProvider` then answers it; `drop;stage=command` aborts with no response; one `FaultAppliedEvent` each.
- [ ] Named `Surl.Cli` tests: one accepted and one refused `reply` for `grpc` and for `grpcs`.
- [ ] ADR-0124 decision 4's extended-server table has the `grpc`, `grpcs` row.
- [ ] With no rule, every existing gRPC test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
