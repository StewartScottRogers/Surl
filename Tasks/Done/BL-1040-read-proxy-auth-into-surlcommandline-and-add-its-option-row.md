---
id: BL-1040
title: Read --proxy-auth into SurlCommandLine and add its option row
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1004]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
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

- [x] Named `Surl.Cli.UnitTests` tests read `--proxy-auth` words into the new member, refuse an unknown word with
      `FailedInit` (2), and show `--auth` and `--proxy-auth` stay independent.
- [x] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` and the `--aihelp` completeness tests pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- Added `SurlCommandLine.GivenProxyAuthenticationMethods` and `AcceptedProxyAuthenticationMethods` (default
  `digest`, `basic`, ADR-0088 decision 5) and the `--proxy-auth <methods>` row (categories `auth`, `proxy`),
  reusing `OptionArgumentReader.AuthenticationMethods`. Both members are in `CommandLinePathMembers.MembersWithoutPaths`.
- Widened `touches` to `Surl.Composition.UnitLibrary`/`.UnitTests` (no task in Doing named them): leaving the
  option read but unapplied would have made the proxy topic's prose false. `ComposeProxyPolicy` now uses
  `AcceptedProxyAuthenticationMethods`; the default `407` is unchanged (Digest then Basic), tested.
- Decision: `--proxy-auth` warns like `--auth` (`surl: warning: --proxy-auth: accepted proxy methods are ...`),
  since it can switch on NTLM or Negotiate. Recorded as ADR-0088 Amendment 8 (ADR file added to `touches`).
- Tests: `CommandLineParserProxyTests` (words, any case, last wins, `--auth` independence, bad words `(2)`,
  missing argument), `ProxyCompositionTests` (default and `--proxy-auth ntlm` challenges, the warning).
  Build clean with `-warnaserror`; all 78 fast test projects green (Surl.Cli.UnitTests 1405).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --proxy-auth <methods> reads into SurlCommandLine apart from --auth, sets the proxy's 407 challenges (digest,basic by default) and warns
