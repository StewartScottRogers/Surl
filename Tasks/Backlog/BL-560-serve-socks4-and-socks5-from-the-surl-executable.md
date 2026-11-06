---
id: BL-560
title: Serve SOCKS4 and SOCKS5 from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-559, BL-555]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-560 — Serve SOCKS4 and SOCKS5 from the surl executable

## Goal

`surl` serves the SOCKS server from the command line BL-557's ADR decision 5 fixes (planning
assumption: `surl socks4://127.0.0.1:1080` and `surl socks5://127.0.0.1:1080`), with its accounts
and limits, over TCP, and `surl --aihelp` documents it.

## Context

- Decision: BL-557's ADR, decisions 4 to 6; BL-550's ADR decision 2 for the allowed-origin rule.
- Code: `Application/Surl.Console/CommandLineRunner.cs` (`ComposeProtocolServers`,
  `AuthenticationComposition`) composes `Surl.Protocol.Socks` with `Surl.Networking`'s listener and
  BL-551's outbound connector; `Application/Surl.Cli.UnitLibrary` (`ListenUrlParser`,
  `SchemeDefaultPorts`, `CommandLineOptions`) parses the schemes, the default port 1080 and the
  options.
- `--aihelp` completeness (root `CLAUDE.md`): the SOCKS server's `HelpCategories` row with its
  schemes, its `AiHelpProse.TopicAbout` paragraphs and an `AiHelpExamples` entry; the topic list
  pinned in `AiHelpTextTests` grows by one
  (`CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`,
  `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
  `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText`,
  `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample`); each new option needs
  its `OptionArgumentType`.
- Upstream test cases served by this server (tag `curl-8_21_0`): `socks4` 700, 702, 706, 708, 729;
  `socks5` 701, 703, 707, 709, 710, 711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 742, 2055. The
  conformance run composes the library itself (BL-562).

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests` has named tests for the SOCKS listen URLs, their default port and each
      refusal the ADR names, each returning its `SurlExitCode`.
- [ ] `Surl.Console.UnitTests` has named tests that run `CommandLineRunner` as a SOCKS4 and a SOCKS5
      server and relay one request to an origin started in the test on loopback, without
      `TestCategory=Integration`.
- [ ] The SOCKS `--aihelp` topic, facts and example exist and the completeness tests named in
      Context pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Planning assumption (BL-399): the listen URL schemes above; BL-557's ADR decides them.
- 2026-11-06 (lane 4): ADR-0121 decision 5 fixes the schemes (`socks4`, `socks4a`, `socks5`, `socks5h`, default port 1080). Protocol servers are no longer composed in `CommandLineRunner`: ADR-0124 moved the registration list to `Application/Surl.Composition.UnitLibrary` (`ProtocolServerRegistrations.Original`, `ComposedProtocolServices`, which must also carry the outbound `IOriginConnector`), so this task now touches `Surl.Composition.UnitLibrary` and its `.UnitTests`. BL-803 (in Doing) touches both, so the task went back to Backlog until BL-803 is Done. Note also that `-x`/`--proxy`, `-U`/`--proxy-user`, `--proxy-user-file` and `--proxy-allow-origin` (ADR-0088 decision 7) do not exist in `Surl.Cli` yet; BL-555 adds them, and this task reuses them for SOCKS (ADR-0121 decision 4), so BL-555 is added to `depends-on` rather than building the shared proxy options twice.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 (Doing) touches; also waits on BL-555 for the shared -x/-U proxy options
