---
id: BL-555
title: Serve the HTTP and HTTPS proxies from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-553, BL-554]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-555 — Serve the HTTP and HTTPS proxies from the surl executable

## Goal

`surl` serves the HTTP proxy and the HTTPS proxy from the command line BL-550's ADR decision 7
fixes (planning assumption: `surl --proxy http://127.0.0.1:3128` and
`surl --proxy https://127.0.0.1:3129 --cert ... --key ...`), with its proxy accounts and limits,
and `surl --aihelp` documents them.

## Context

- Decision: BL-550's ADR, decisions 2 (allowed origins), 5 (accounts), 6 (TLS) and 7 (command
  line, limits).
- Code: `Application/Surl.Console/CommandLineRunner.cs` (`ComposeProtocolServers`,
  `ServerTlsComposition`, `AuthenticationComposition`) composes `Surl.Protocol.HttpProxy` with
  `Surl.Networking`'s listener, TLS and BL-551's outbound connector; `Application/Surl.Cli.UnitLibrary`
  parses the options (`CommandLineOptions`, `CommandLineParser`, `ListenUrlParser`,
  `SchemeDefaultPorts`).
- `--aihelp` completeness (root `CLAUDE.md`): a protocol server registered in
  `ComposeProtocolServers` needs its `HelpCategories` row with its schemes, its
  `AiHelpProse.TopicAbout` paragraphs and an `AiHelpExamples` entry, and the topic list pinned in
  `AiHelpTextTests` grows by one
  (`CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`,
  `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
  `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText`,
  `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample`); each new option needs
  its `OptionArgumentType` (`AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`).
- Upstream test cases served by the proxies (tag `curl-8_21_0`): `http-proxy` 80, 83, 95, 275, 445,
  714, 715, 744, 1078, 1230, 1287, 1288, 1297, 1319, 1320, 1321, 1428, 1849, 1904, 2050, 2055,
  2107, 3028, 3106 and `https-proxy` 1586, 1630, 1631, 1632, 3401. The conformance run composes the
  library itself (BL-556); this task is what a person running `surl` gets.

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests` has named tests for each new option and listen form, and for each
      refusal the ADR names, each returning its `SurlExitCode`.
- [ ] `Surl.Console.UnitTests` has named tests that run `CommandLineRunner` as the HTTP proxy and
      as the HTTPS proxy, forward one request and tunnel one `CONNECT` to an origin started in the
      test on loopback, without `TestCategory=Integration`.
- [ ] The proxy's `--aihelp` topic, facts and example exist and the completeness tests named in
      Context pass; `surl --help` and `surl --manual` list the new options.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Planning assumption (BL-399): the command-line form above; BL-550's ADR decides it.
- BL-554 (ADR-0088 Amendment 2): every listen URL built from `--proxy` must set `ListenUrl.IsProxy = true`, so an `https` proxy listener offers ALPN `http/1.1` only; without it curl's `--proxy-http2` agrees `h2` and the proxy closes the connection.

## Log

- 2026-11-04: Created.
