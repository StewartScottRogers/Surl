---
id: BL-567
title: Serve HTTPS over TLS-SRP from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-566]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-567 — Serve HTTPS over TLS-SRP from the surl executable

## Goal

`surl` serves HTTPS over TLS-SRP from the command line BL-563's ADR decision 6 fixes (planning
assumption: `surl https://127.0.0.1:8443 --tlsauthtype SRP --tlsuser jsmith --tlspassword abc`),
composing `Surl.TlsSrp` in front of `Surl.Protocol.Http`, and `surl --aihelp` documents the new
options.

## Context

- Decision: BL-563's ADR, decisions 4 to 6.
- Code: `Application/Surl.Console/CommandLineRunner.cs` and `ServerTlsComposition.cs` (today's TLS
  composition through `Surl.Networking`'s `ServerTlsHandshake`), `ImplicitTlsSchemeServer.cs`;
  `Application/Surl.Cli.UnitLibrary` (`CommandLineOptions`, `CommandLineParser`,
  `OptionArgumentReading`, `HelpCategories`, `AiHelpProse`, `AiHelpExamples`,
  `ExitCodeGuidanceTable`).
- `--aihelp` completeness (root `CLAUDE.md`): each new option needs its `OptionArgumentType` and
  appears on each of its categories' pages
  (`AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
  `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`); an
  `AiHelpExamples` entry shows TLS-SRP and
  `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` runs it.
- Upstream test cases this makes reachable by hand (tag `curl-8_21_0`), `httptls+srp`: 320, 321,
  322, 324, from the Linux and macOS reference pins (the Windows pin has no `TLS-SRP`).

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests` has named tests for each new option, a missing user or password, an
      unreadable verifier file if the ADR has one, and TLS-SRP combined with an option the ADR
      refuses beside it, each returning its `SurlExitCode`.
- [ ] `Surl.Console.UnitTests` has a named test that runs `CommandLineRunner` with TLS-SRP and
      completes one HTTPS request with a client in the test that drives `Surl.TlsSrp`'s handshake
      from recorded bytes, without `TestCategory=Integration`.
- [ ] `surl --help`, `surl --manual` and `surl --aihelp all` describe the options, and the
      completeness tests named in Context pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Planning assumption (BL-399): the option names mirror curl's; BL-563's ADR decides them.
- BL-563 decided it in ADR-0122: options `--tlsauthtype`, `--tlsuser`, `--tlspassword`,
  `--srp-verifiers`, `--srp-groups` (decision 6); the TLS-SRP handshake plugs in through
  `Surl.Networking` for every TLS listener and `TlsSession` gains `SrpUserName` (decision 5), so
  BL-563 added `Foundation/Surl.Networking.*` and `Foundation/Surl.Protocol.Abstractions.*` to
  `touches` on 2026-11-05.

## Log

- 2026-11-04: Created.
