---
id: BL-1093
title: Register grpc and grpcs in the surl executable with their aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1094, BL-1095, BL-405, BL-1035]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1093 — Register grpc and grpcs in the surl executable with their aihelp topic

## Goal

`surl grpc://127.0.0.1:0` answers gRPC over HTTP/2 prior knowledge and `surl grpcs://127.0.0.1:0` answers it over
TLS; `--aihelp` has a grpc topic and help category, and the extension switch (`SURL_EXTENSIONS=0`) turns both off.

## Context

- Split from BL-445, whose run timed out. Registration and the `--aihelp` topic land together on purpose:
  `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` and
  `ProtocolTopics_ClaimOnlyRegisteredSchemes` fail if either lands alone. Both are small (about 30 lines of
  production code); most of the diff is pinned scheme lists in tests.
- Register in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`): one entry
  claiming `GrpcProtocolServer.PlainScheme` and `TlsScheme`, plus the `ProjectReference` in its csproj. `grpcs` is
  TLS-wrapped through `TlsSchemes` (BL-1095). Default ports: `grpc` 80, `grpcs` 443.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming PROTOCOL-HTTP2.md as its oracle, an `AiHelpExamples` entry, and the
  topic list in `AiHelpTextTests` growing by one.
- The BL-445 run wrote all of it in the stash named `darkfactory BL-445 20261112-162230`. Find its index N with
  `git stash list` (match the message), then, from Git Bash (lanes may not run git checkout or git stash):
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Composition.UnitLibrary Application/Surl.Composition.UnitTests Application/Surl.Cli.UnitLibrary Application/Surl.Cli.UnitTests Application/Surl.Console.UnitTests | git apply`
  - `git show "stash@{N}^3:Application/Surl.Composition.UnitTests/SurlServerCompositionTests.Grpc.cs" > Application/Surl.Composition.UnitTests/SurlServerCompositionTests.Grpc.cs`

  Known defect in that diff: the `ExtendedProtocolServers` summary says "the gRPC server for grpc and grpcs
  (BL-445)" twice; say it once. Other tasks may have changed the pinned scheme lists since; redo a rejected hunk
  by hand.
- Stand-in: the gRPC server is stateless (echo and reflection), shares no store, publishes its exchanges on the
  instance's event stream through `ExchangeContext`, and is reachable from `Surl.Api` and `Surl.Mcp` through
  `Surl.Composition` like every extended server.

## Acceptance criteria

- [ ] A named test (`SurlServerCompositionTests.ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersGrpcAndGrpcsOnlyWhenOn`)
      finds `grpc` and `grpcs` registered when the switch is on, and absent when it is off.
- [ ] `Surl.Console.UnitTests` pin `grpc` and `grpcs` in the extended scheme list, in `--version`'s `Extended:`
      line, and at default ports 80 and 443.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
