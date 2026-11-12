---
id: BL-1040
title: Read --proxy-auth into SurlCommandLine and add its option row
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1004]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1040 — Read --proxy-auth into SurlCommandLine and add its option row

## Goal

`surl --proxy http://127.0.0.1:3128 -U u:p --proxy-auth basic,ntlm` reads the proxy's accepted authentication
methods into `SurlCommandLine`, separate from `--auth`, as ADR-0088 decision 5 names.

## Context

- ADR-0088 decision 5 lists `--proxy-auth <methods>` beside `-U` and `--proxy-user-file`; BL-1002 (members and
  readers), BL-1003 (composition) and BL-1004 (option rows) cover the other proxy options but not this one.
  Found by BL-1002 on 2026-11-11.
- Reuse `OptionArgumentReader.AuthenticationMethods` and mirror `GivenAuthenticationMethods` /
  `AcceptedAuthenticationMethods` (default Digest and Basic for the proxy, decision 5).
- The new member must be classified in `Surl.Mcp.UnitLibrary`'s `CommandLinePathMembers`
  (`Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce`). The option row needs its `--aihelp` facts and the
  proxy topic's page. Composing the methods into the proxy's policy is in `Surl.Composition`, a follow-up if
  BL-1003 has not done it.

## Acceptance criteria

- [ ] Named `Surl.Cli.UnitTests` tests read `--proxy-auth` words into the new member, refuse an unknown word with
      `FailedInit` (2), and show `--auth` and `--proxy-auth` stay independent.
- [ ] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` and the `--aihelp` completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
