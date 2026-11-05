---
id: BL-673
title: Add the LDAP account source option and compose it beside --user and --user-file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-672, BL-646]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-673 — Add the LDAP account source option and compose it beside --user and --user-file

## Goal

The option BL-671's ADR names (planning `--user-ldap <ldap-url>`) makes `surl` check password logins against an
LDAP account source beside `--user` and `--user-file`, composed in `Surl.Composition` with whatever outbound
connection seam the ADR names.

## Context

- Decisions: BL-671's ADR (option, URL form, TLS requirement, refusals, exit codes, bounds), BL-641's ADR (item
  grammar and help category), BL-642's ADR decision 4 (groups).
- Builds on: BL-672 (the check in `Surl.Authentication`), BL-646 (groups may name LDAP accounts if the ADR says
  so). The outbound connection comes from `Foundation/Surl.Networking.UnitLibrary` (BL-551's outbound
  connections); if a change there is needed, record it in Notes and file it rather than widen this task.
- Where: `Application/Surl.Cli.UnitLibrary` (option row) and `Application/Surl.Composition.UnitLibrary`.
- `--aihelp` completeness (root `CLAUDE.md`). The example cannot reach a real directory; it shows the refusal of
  an unreachable or non-TLS URL as the ADR says, which `CommandLineRunnerAiHelpTests` can run.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse the option and refuse a malformed URL with the ADR's exit
      code and text, never echoing a password in the URL.
- [ ] Named tests in `Application/Surl.Composition.UnitTests` show the source composed beside `--user` accounts and
      the plaintext-URL refusal or warning the ADR names.
- [ ] The option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
