---
id: BL-445
title: Serve grpc and grpcs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-444, BL-405, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-445 — Serve grpc and grpcs from the surl executable

## Goal

`surl grpc://127.0.0.1:0` answers gRPC over HTTP/2 prior knowledge, and `surl grpcs://127.0.0.1:0` answers over
TLS with ALPN `h2` offered (and `http/1.1` refused, as PROTOCOL-HTTP2.md requires HTTP/2). The `--aihelp` topic
and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `grpcs` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- ALPN is chosen per scheme in `Foundation/Surl.Networking.UnitLibrary` (`TlsApplicationProtocols`; BL-370 added
  `h2` for `https`). `grpcs` offers `h2` only. Keep every other scheme's offer unchanged, which the existing
  Networking tests pin.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming PROTOCOL-HTTP2.md as its oracle, an `AiHelpExamples` entry, and the
  topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `grpc` and `grpcs` registered when the switch is on, and absent when it is off.
- [ ] A named `Surl.Networking.UnitTests` test shows `grpcs` offering exactly `h2`. The existing ALPN tests for
      `https` and `wss` pass unchanged.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and
      branch coverage.

## Notes

- 2026-11-05 (lane 4): Since BL-405 the extended-server composition lives in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`), not in
  `Surl.Console/CommandLineRunner.cs`. So `grpc` and `grpcs` are registered there, and their tests go in
  `Surl.Composition.UnitTests`. Both are added to `touches`. BL-803 (in Doing) touches the same projects, so this
  task goes back to Backlog until BL-803 frees them, as BL-433 did.

- 2026-11-11 (lane 2): Returned to Backlog behind BL-1035: `dotnet build` fails with "SDK not found" because
  global.json pins .NET SDK 10.0.401 and this machine has only 10.0.100-rc.1 and 11.0.x, so no gate can be
  checked. No code was written.

- 2026-11-12 (lane 1): The SDK is fixed (10.0.404 builds). But `Surl.Extended.Protocol.Grpc.UnitLibrary` (BL-443/444)
  has only `GrpcStreamHandler` (an `IHttp2StreamHandler`) and no `IConnectionProtocolServer`, so there is nothing to
  register for grpc/grpcs. The task needs a `GrpcProtocolServer` there (prior-knowledge preface check,
  `Http2ConnectionServer` with `GrpcStreamHandler`, `EchoService` and `ServerReflectionService`, per ADR-0116
  decisions 0-1), so the Grpc projects are added to `touches`. BL-871 in Doing touches `Protocols` whole, so this
  waits for it. Plan once free: `GrpcProtocolServer` + tests, register grpc/grpcs in `ExtendedProtocolServers`
  (grpcs `ImplicitTls`), grpcs h2-only in `TlsApplicationProtocols`, default ports, `HelpCategories`,
  `AiHelpProse`, `AiHelpExamples`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests (ExtendedProtocolServers.Registrations), which BL-803 in Doing touches; resume once BL-803 frees Surl.Composition
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Needs Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary and .UnitTests (a GrpcProtocolServer does not exist yet), which BL-871 in Doing touches via Protocols; resume once BL-871 is done
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Deferred. Split into BL-1091, BL-1092, BL-1093 (timed out at the run's time limit)
