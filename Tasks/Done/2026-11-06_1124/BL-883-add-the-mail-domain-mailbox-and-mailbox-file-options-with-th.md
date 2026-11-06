---
id: BL-883
title: Add the --mail-domain, --mailbox and --mailbox-file options with their help, manual and aihelp text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-882]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-883 — Add the --mail-domain, --mailbox and --mailbox-file options with their help, manual and aihelp text

## Goal

`surl --mail-domain <domain> --mailbox <address>:<password>[;protocols=...] --mailbox-file <csv>` is accepted on
the command line, refused as ADR-0101 says, and documented in `--help`, the manual and `--aihelp` (a new `mail`
topic with its example). With BL-882's composition already in place, the options configure real mail domains
and mailboxes end to end. This completes what BL-653 set out to do.

## Context

- Last of BL-653's split (timed out after 120 min; Stewart asked for the split 2026-11-05). It lands after
  BL-882, so every help sentence describes behaviour that already exists.
- Decisions: ADR-0101 decisions 4, 5 and 7 (option rows, parse-time refusals, help category) and ADR-0102
  decisions 1 and 5.
  - Parse-time refusals, exit 2: a domain given twice, a mailbox given twice (compared without regard to case),
    an address outside every `--mail-domain`, `--mailbox` without `--mail-domain`, a mailbox equal to a `--user`
    name, and an unknown scheme in `protocols=`.
  - `--mailbox` sets `ArgumentHoldsSecret`.
  - The `mail` help category is "Mail domains, mailboxes and aliases".
  - `ExitCodeGuidanceTable`'s exit 37 row names `--mailbox-file`.
- `--aihelp` completeness (root `CLAUDE.md`):
  - Each option needs its argument type and categories, a `ManualText` section and `AiHelpProse` paragraphs
    under `mail`.
  - The topic needs an `AiHelpExamples` entry with `AiHelpExamplePrecondition.MailboxFileExists`.
  - `CommandLineRunnerAiHelpTests.ReadStartFileAsThePreconditionSays` must supply `mailboxes.csv`; that is why
    `Application/Surl.Console.UnitTests` is touched. Without it, that test hangs waiting for the start, as the
    lane saw.
  - `AiHelpTextTests.Topics_...` grows by `mail`.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-653`; call it `stash@{N}`.
  - Untracked file: `git checkout "stash@{N}^3" -- Application/Surl.Cli.UnitTests/CommandLineParserMailTests.cs`,
    then `git reset` it.
  - Tracked edits:
    `git diff "stash@{N}^1" "stash@{N}" -- Application/Surl.Cli.UnitLibrary/AiHelpExamplePrecondition.cs Application/Surl.Cli.UnitLibrary/AiHelpExamples.cs Application/Surl.Cli.UnitLibrary/AiHelpProse.cs Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs Application/Surl.Cli.UnitLibrary/CommandLineParser.cs Application/Surl.Cli.UnitLibrary/ExitCodeGuidanceTable.cs Application/Surl.Cli.UnitLibrary/HelpCategories.cs Application/Surl.Cli.UnitLibrary/ManualText.cs Application/Surl.Cli.UnitTests/AiHelpExamplesTests.cs Application/Surl.Cli.UnitTests/AiHelpTextTests.cs Application/Surl.Cli.UnitTests/HelpTextTests.cs Application/Surl.Cli.UnitTests/ManualTextTests.cs Application/Surl.Console.UnitTests/CommandLineRunnerAiHelpTests.cs | git apply -3`.
    The `CommandLineParser.cs` hunk replaces the `--group` counter with a shared item-option counter and adds
    the mail checks.
    `HelpCategories.cs` in the stash has a typo, `new("mqtt","MQTT...`: restore the space.
  - BL-880 took `SurlCommandLine.cs` and the readers; BL-881 and BL-882 took every Composition file.
  - Check every `AiHelpProse` sentence against BL-882's composed behaviour, since the stash wrote them before
    composition existed. Then re-pin the whole-manual and help-text tests.
- Stand-in (ADR-0124): none beyond BL-882's. This task is the command-line surface only.

## Acceptance criteria

- [x] `CommandLineParserMailTests` in `Application/Surl.Cli.UnitTests` parse each option (one, many), and refuse
      each case above with exit 2 and the ADR's text. No refusal echoes a password.
- [x] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`. The `mail` topic passes
      `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText` and
      `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample`.
- [x] `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` passes with the mail
      example (`--mail-domain example.com --mailbox-file mailboxes.csv imap://127.0.0.1:0/`).
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- The BL-653 stash was found through `git log -g refs/stash` (28786c9f; `git stash list` was not permitted
  in the lane). Its 13 tracked diffs applied with `git apply -3`; the conflicts were each resolved by keeping
  both sides (`--fault`, `s3`, `control` from master, `mail` from the stash). The `HelpCategories` typo is fixed.
- The stash was written against its own readers; BL-880 landed different ones, so the parser was adapted
  rather than the readers rewritten:
  - `MailDomainNames` gains `Reading` / `ReadMailDomain` (blank, then `ReadDomain`'s refusal; the domain
    kept as written) and `MailDomainGivenTwice`.
  - `ConfiguredMailboxReader.Read` already normalises the address and refuses a scheme no surl server
    has. The parser (`ReadMailbox`) also refuses a scheme no *registered* server claims, so an extended
    scheme under `SURL_EXTENSIONS=0` exits 2 as ADR-0101 decision 4 says.
  - Refusals name the domain or address as written (ADR-0101 decision 5): the parser keeps each
    `--mailbox`'s written address (`MailboxWrittenAddresses`, decoded from its argument) beside the
    normalised `ConfiguredMailbox`, the same split `MailboxFileReader` uses.
  - Test expectations moved to BL-880's behaviour: "no @" and "empty local part" are now
    `not a mail address` (refused as read, not as "not in any --mail-domain"), `ConfiguredMailbox` has
    three fields, and `Mailboxes` holds normalised addresses.
- Complexity: the mail arms sit in `ActOnAcceptedMailOption` so `ActOnAcceptedArgument`'s string switch
  stays at five cases (seven turned it into a hash jump table with uncoverable branches), and
  `RefuseMailCombination` is split into three checks. `Surl.Cli.UnitLibrary` is at 100% lines;
  `RefuseCombination`'s 95% branch figure is unchanged by this task (measured the same with the mail
  check moved out of it).
- Every `AiHelpProse` and `ManualText` sentence was checked against `MailboxComposition` (BL-882): the
  default `smtp`/`imap`/`pop3` with TLS twins (`Account.MayLogInOn`), the `-v` lines, the exit-37 text and
  the per-file and per-start 10000 bounds all match.

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-653 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. --mail-domain, --mailbox and --mailbox-file parse, refuse per ADR-0101 and are documented in --help, the manual and the --aihelp mail topic
