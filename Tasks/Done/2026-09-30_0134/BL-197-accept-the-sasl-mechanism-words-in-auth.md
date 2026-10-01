---
id: BL-197
title: Accept the SASL mechanism words in --auth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-196]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-29
---
# BL-197 — Accept the SASL mechanism words in --auth

## Goal

`--auth` accepts the SASL mechanism words BL-185's ADR decides (or maps the mechanisms onto the
existing words as it decides), `Surl.Console` composes them into the authentication policy, and
the help, manual, AI help and start-up warning describe them.

## Context

- Decisions: BL-185's ADR (words, default set, order, descriptions); ADR-0032 sections 1, 3 and 9
  (the `--auth` rules, the method order, the warning line `surl: warning: --auth: accepted
  methods are <methods>`); ADR-0034 (help); ADR-0046 (AI help facts).
- Code: `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (the `--auth` row and its
  `GivenAuthenticationMethods`), `ManualText.cs`, `AiHelpProse.cs`;
  `Surl.Console/AuthenticationComposition.cs` (maps words to `Surl.Authentication` methods).
- This task lands after BL-194 to BL-196 so every word it accepts is implemented; no "not
  available in this build" refusal is needed.
- If BL-185's ADR decided no new word, this task still aligns the `--auth` description, manual and
  AI help with which mechanisms each existing word enables; say so in Notes.

## Acceptance criteria

- [x] `CommandLineParserTests` cover each new word (any case), a repeated word, and an unknown
      word refused with `option --auth: is badly used here`.
- [x] A `Surl.Console.UnitTests` test shows each word enabling its mechanism in the composed
      policy, and the warning line listing the accepted set in ADR-0032 section 3's order.
- [x] `HelpTextTests`, `ManualTextTests`, `AiHelpFactsTests` and `AiHelpTextTests` pass with the
      updated texts.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Plan (ADR-0049 section 3): `OptionArgumentReader.AuthenticationMethodWords` grows to the
  fifteen words in the ADR's order (negotiate, gssapi, ntlm, digest, digest-md5, cram-md5, apop,
  basic, plain, login, bearer, oauthbearer, xoauth2, external, aws-sigv4); the default set becomes
  `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,aws-sigv4` in `SurlCommandLine`
  and the `--auth` help row; `AuthenticationComposition.MethodsByWord` maps each available word to
  its `AuthenticationMethod`. `AuthenticationPolicy` already offers and runs the mechanisms from
  `AcceptedMethods` (BL-193 to BL-196), so nothing in `Surl.Authentication` changed. The warning
  line already lists the parsed words, which the reader returns in the ADR's order.
- `gssapi` and `external` follow the ADR, not the task's Context line ("no refusal is needed"):
  ADR-0049 section 3 says both are read and refused as `surl: (2) --auth <word> is not available
  in this build` until BL-218 and BL-216 land, so they join `CommandLineRunner`'s
  `UnavailableOptions`, first (the `--auth` row comes before the SSH rows in the option table).
  BL-216 and BL-218 must take them out of that list and add their `MethodsByWord` rows.
- Choice: the `--auth` explanation and manual say "this build serves none of those three
  protocols yet", since `ComposeProtocolServers` registers no SMTP, IMAP or POP3 server; the
  servers' login tasks (BL-200, BL-204, BL-206) should drop that clause when they compose them.
- Choice: the explanation no longer repeats the default set (the help's `Default:` line shows it),
  and names curl's "login option AUTH=<mech>" rather than `--login-options`, which the
  names-only-surl-options checks would read as a surl option.
- Checked, unchanged: `Documentation/Product/Requirements.md` FR-008 lists `--auth` without its
  words and FR-046 says the mail servers "are to" log in with ADR-0049's mechanisms; both stay
  true, so nothing outside `touches` was edited.
- Quality: `Surl.Cli.UnitLibrary` 100% line and branch, 0 failing, worst CRAP 10;
  `Surl.Console` 100% line and branch, 0 failing, worst CRAP 10. Cli tests 691, Console 249.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --auth accepts the SASL words digest-md5, cram-md5, apop, plain, login, oauthbearer and xoauth2 (ntlm for both), composed into the policy; gssapi and external refused as not available
