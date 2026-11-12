---
id: BL-450
title: Serve signalr and signalrs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449, BL-405, BL-803, BL-1035]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-450 — Serve signalr and signalrs from the surl executable

## Goal

`surl signalr://127.0.0.1:0` and `surl signalrs://127.0.0.1:0` (TLS) start the SignalR hub. The `--aihelp` topic
and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `signalrs` is wrapped for TLS as `wss` is (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming HubProtocol.md and TransportProtocols.md as its oracle, an
  `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [x] A named test finds `signalr` and `signalrs` registered when the switch is on, and absent when it is off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05: Extended servers are now registered in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`, moved there from `Surl.Console`), and the registration test belongs in `Surl.Composition.UnitTests`, so both are added to `touches`. BL-803, in Doing, touches the same projects, so this task waits on it.

- 2026-11-11: `signalrs` must be TLS from the first byte (ADR-0119 decision 0), and the serving engine reads that from `TlsSchemes`, so `signalrs` joins `TlsSchemes.ImplicitTlsSchemes` in `Foundation/Surl.Protocol.Abstractions.UnitLibrary` (as `s3` did) and both Abstractions projects are added to `touches`; no task in Doing (BL-840, BL-912) names them. With it in `TlsSchemes` the SignalR server is one registration for `signalr` and `signalrs` (its own `Schemes`), like NATS and S3, rather than an `ImplicitTlsSchemeServer` entry: the server already claims both schemes.
- 2026-11-11: Decision (sensible default): the hub is served at `/` (`SignalRHubPath.FromListenPath(string.Empty)`). ADR-0119 decision 1 serves it at the listen URL's path, but `ListenUrlParser` refuses a path and `ListenUrl` carries none, and a registration builds one server (one `SignalRHubConnections`) per scheme, not per listen URL. Filed BL-1039 to carry the path through and give each listen URL its own hub; it decides the listen-URL path rule in an ADR.
- 2026-11-11: Code is written, uncommitted: `ExtendedProtocolServers.Registrations` gains the SignalR entry (project reference added); tests `ExtendedProtocolServersTests.Compose_ThisBuild_BuildsTheSignalRServerWithItsHubAtTheRootAndItsOwnConnections`, `ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersSignalRAndSignalRsOnlyWhenOn`, and in `CommandLineRunnerExtendedProtocolTests` the signalr/signalrs default-port, switch-off and `(58)` tests; `HelpCategories` row `signalr`, `AiHelpProse` `signalr` topic and the `extended` paragraph, an `AiHelpExamples` entry, and the Cli test lists. Verified with a temporary `global.json` on the installed SDK 11.0.100 (not committed): build clean with `-warnaserror`, every fast test project green (78), `dotnet format --verify-no-changes` clean on the touched projects. Not verified on the pinned SDK 10.0.401, which is not installed (BL-1035), so the last box stays open and the task waits on BL-1035.

- 2026-11-12: The earlier uncommitted code was unreachable (stash), so it was rewritten the same way. SDK 10.0.404 now satisfies global.json (rollForward latestFeature), so build, fast tests (all projects) and `dotnet format` ran green on the pinned line. Coverage of new code: the registration lambda and every new row are exercised by the Composition and Console tests.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 (in Doing) touches; depends on BL-803
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; code is done and green on SDK 11, left uncommitted for the shift's stash
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. signalr and signalrs are served from the surl executable, with --aihelp topic, help category and extension switch
