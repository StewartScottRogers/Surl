---
id: BL-1004
title: Add the -x, -U, --proxy-user-file and --proxy-allow-origin options with the proxy's help and aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1003]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-1004 — Add the -x, -U, --proxy-user-file and --proxy-allow-origin options with the proxy's help and aihelp topic

## Goal

`surl --proxy http://127.0.0.1:3128` and `surl --proxy https://127.0.0.1:3129 --cert ... --key ...` serve the HTTP
and HTTPS proxies from the command line as ADR-0088 decision 7 fixes, with their accounts and limits, and
`surl --help`, `surl --manual` and `surl --aihelp` document them. This completes what BL-555 set out to do.

## Context

- Last of the three tasks BL-555 was split into (see BL-1002); follow BL-883's shape for the option rows.
- `--aihelp` completeness (root `CLAUDE.md`): the proxy's topic - a `HelpCategories` row, its
  `AiHelpProse.TopicAbout` paragraphs and an `AiHelpExamples` entry, the topic list pinned in `AiHelpTextTests`
  growing by one (`AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
  `Answer_EveryTopicAndTheOverview_HasItsAboutText`, `Answer_EveryTopicButExitCodesAndSecurity_HasAnExample`); each
  new option needs its `OptionArgumentType` (`AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`).
- Upstream test cases served by the proxies (tag `curl-8_21_0`): `http-proxy` 80, 83, 95, 275, 445, 714, 715, 744,
  1078, 1230, 1287, 1288, 1297, 1319, 1320, 1321, 1428, 1849, 1904, 2050, 2055, 2107, 3028, 3106 and `https-proxy`
  1586, 1630, 1631, 1632, 3401. The conformance run composes the library itself (BL-556's split); this task is what
  a person running `surl` gets.
- Stand-in: none beyond BL-1003's; this task is the command-line surface.

## Acceptance criteria

- [x] `Surl.Cli.UnitTests` has named tests for each new option and listen form, and for each parse-time refusal the
      ADR names, each returning its `SurlExitCode`.
- [x] `Surl.Console.UnitTests` has named tests that run `CommandLineRunner` as the HTTP proxy and as the HTTPS
      proxy, forward one request and tunnel one `CONNECT` to an origin started in the test on loopback, without
      `TestCategory=Integration`.
- [x] The proxy's `--aihelp` topic, facts and example exist and the completeness tests named in Context pass;
      `surl --help` and `surl --manual` list the new options.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli.UnitLibrary`
      keep 100% line and branch coverage.

## Notes

- Decisions are ADR-0088 amendment 7: `-x` is read where given (so `-K` lines work), a proxy-only command
  line is served, a Unix socket beside `--proxy` is refused `(2)`, the `proxy` category claims no scheme,
  `--proxy-allow-origin` is in `security` (no loosening explanation), help argument shown as `<address>`.
- Added `--manual` "PROXY OPTIONS" and the security-topic paragraph; `CommandLineParserProxyTests` (Cli) and
  `CommandLineRunnerProxyTests` (Console: HTTP and HTTPS proxy, forward and CONNECT against a loopback
  origin started in the test, no Integration category).
- Fast tests all green; new code in `Surl.Cli.UnitLibrary` fully covered (two partial branches elsewhere
  predate this task).
## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. -x, -U, --proxy-user-file and --proxy-allow-origin serve the HTTP and HTTPS proxies from the command line, with help, manual and aihelp proxy topic
