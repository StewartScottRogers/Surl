---
id: BL-1406
title: Name the account alternative in the exit-58 message for a coaps listen URL
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1406 — Name the account alternative in the exit-58 message for a coaps listen URL

## Goal

`surl coaps://127.0.0.1:0/` with no certificate and no account stops with exit 58 and a message that names both ways
out, a certificate or an account for a pre-shared key, as `surl --aihelp coap` already says.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 5 (coap part); review
  `Z:\repos\Surly\Book\parts\05-new-protocols\chapters\03-remote-calls-and-streams\recipes\coap-get-a-resource\review.md`
  finding 5: `surl --aihelp coap` (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:118`) says a `coaps` listen URL
  "needs a certificate or an account, else it exits 58", but the exit-58 message names only `--cert` and
  `--self-signed`.
- Judged code, not help: the help states the rule ADR-0087 decision 3 and ADR-0097 (line 469: "no `--user` or
  `--user-file` account is refused `(58)` like an implicit-TLS one") decide, and
  `Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs:254-257` implements it
  (`coaps` requires a certificate only when `commandLine.Accounts.Count == 0 && commandLine.UserFile is null`).
  The message is the one that leaves the account path out:
  `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:114-119` (`FormatMissingCertificate`,
  one text for every scheme: `(58) <url> needs a certificate: give --cert <file>, or --self-signed for a throwaway
  one`).
- The work: for a `coaps` listen URL, `FormatMissingCertificate` adds the account alternative, e.g.
  `(58) coaps://127.0.0.1:0/ needs a certificate or an account: give --cert <file>, --self-signed for a throwaway
  one, or --user <name:key> for a pre-shared key`; every other scheme's text is unchanged.
- Pinned today at `Application/Surl.Console.UnitTests/CommandLineRunnerExtendedProtocolTests.cs:720`; the format
  is reached through `CommandLineRunner.FormatMissingCertificate` (`Application/Surl.Console/CommandLineRunner.cs:167`),
  which needs no change.
- Stand-in: a test author who reads the refusal learns the PSK route without opening the help; no store, event or
  fault change.

## Acceptance criteria

- [x] A failing test first: a `Surl.Composition.UnitTests` test pins `FormatMissingCertificate` for
      `new ListenUrl("coaps", "127.0.0.1", 0)` as the new text, naming `--cert`, `--self-signed` and `--user`.
- [x] A test pins that `https`, `xmpps` and `webtransport` still get today's text unchanged.
- [x] `CommandLineRunnerExtendedProtocolTests.cs:720` expects the new `coaps` line and passes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` stays at
      100% line and branch coverage.

## Notes

- `--user-file` is the other account source; naming `--user` alone keeps the line short, as the `--aihelp coap`
  paragraph explains the rest.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. coaps exit-58 message names the --user alternative
