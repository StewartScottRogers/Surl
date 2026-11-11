---
id: BL-445
title: Serve grpc and grpcs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-444, BL-405]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
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

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests (ExtendedProtocolServers.Registrations), which BL-803 in Doing touches; resume once BL-803 frees Surl.Composition
- 2026-11-11: Backlog -> Doing.
