---
id: BL-1555
title: Word the Unix socket bind refusals as ADR-0094 decisions 3 and 5 give them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary]
requirement: none
created: 2026-11-21
completed:
---
# BL-1555 — Word the Unix socket bind refusals as ADR-0094 decisions 3 and 5 give them

## Goal

`surl --unix-socket <path>` refused before binding says why in ADR-0094's words, still with exit 45
(`SurlExitCode.BindFailed`): a path too long names its length and the platform's limit, a path that exists
says so, and an abstract name off Linux says abstract names exist only on Linux.

## Context

- Surly cookbook bug report 2026-11-21, item 6: `surl --unix-socket /tmp/<a 120-character name> --directory site http://localhost/`
  prints `surl: (45) ... Bind failed`.
- Decided, not built: ADR-0094 decision 5 gives `Unix socket path too long: '<path>' is <n> bytes and this platform allows <limit>`
  and `Could not bind <path>: the path exists already`; decision 3 gives
  `Abstract Unix socket names exist only on Linux`. All three are exit 45.
- Cause: `Surl.Networking` classifies the failures correctly
  (`Foundation/Surl.Networking.UnitLibrary/UnixSocketBindRules.cs:45-59` returns
  `ListenerBindFailure.UnixSocketPathTooLong` or `AbstractUnixSocketUnsupported`, and `FindPathRefusal`
  `AddressInUse` for a path that exists; `UnixSocketConnectionListener.cs:88-94` throws them), but
  `SurlServerComposition.FormatBindFailure`
  (`Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:93-104`) words every listen URL as
  `(45) Could not bind <scheme>://<host>:<port>/: <reason>`, and `DescribeBindFailure` (`:127-133`) has no arm
  for the two Unix failures, so they fall to `Bind failed`.
- The limit: `UnixSocketBindRules.MaximumPathBytes(bool isMacOs)` (`UnixSocketBindRules.cs:33`, 107 on Linux and
  Windows, 103 on macOS) is in an `internal` class. Make the class `public` (one word in `Surl.Networking`; it is
  already fully tested by `Surl.Networking.UnitTests`) and call `MaximumPathBytes(OperatingSystem.IsMacOS())` from
  Composition; `<n>` is `Encoding.UTF8.GetByteCount` of the address's name. `ListenerBindException.ListenUrl.UnixSocket`
  carries the path or abstract name.
- Wording when `ListenUrl.UnixSocket` is set:
  - `UnixSocketPathTooLong`: `(45) Unix socket path too long: '<path>' is <n> bytes and this platform allows <limit>`
  - `AddressInUse`: `(45) Could not bind <path>: the path exists already`
  - `AbstractUnixSocketUnsupported`: `(45) Abstract Unix socket names exist only on Linux`
  - every other failure: unchanged.
  Every IP listen URL's message is unchanged
  (`Application/Surl.Console.UnitTests/CommandLineRunnerTests.cs:540-550` pins them and must keep passing).
- Not in scope: the report's remark that `SurlServerOptions` has no Unix-socket property. ADR-0099 (the
  `AdditionalArguments` decision) gives typed properties only to options a test commonly needs and
  `AdditionalArguments` the rest, so it is decided, not a defect.
- Stand-in: no store, event or fault changes; `Surl.Api` and `Surl.Mcp` report the same text through
  `SurlServerStartException` and `start_server`'s `lines`.

## Acceptance criteria

- [ ] Tests in `Application/Surl.Composition.UnitTests` pin each of the three messages above for a
  `ListenerBindException` on a listen URL with `UnixSocket` set, the too-long one with a 120-byte path and the
  platform's limit.
- [ ] A test pins that a Unix listen URL failing with `PermissionDenied` keeps today's message.
- [ ] `UnixSocketBindRules` is `public` and nothing else in `Surl.Networking` changes.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, and
  `dotnet test Application/Surl.Composition.UnitTests` and `dotnet test Application/Surl.Console.UnitTests`
  pass with `--filter "TestCategory!=Integration"`, `FormatBindFailure` at 100% line and branch coverage.
- [ ] Each refusal still returns `SurlExitCode.BindFailed` (45).

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
