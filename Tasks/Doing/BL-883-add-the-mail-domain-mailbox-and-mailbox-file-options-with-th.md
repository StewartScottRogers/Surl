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
completed:
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

- [ ] `CommandLineParserMailTests` in `Application/Surl.Cli.UnitTests` parse each option (one, many), and refuse
      each case above with exit 2 and the ADR's text. No refusal echoes a password.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`. The `mail` topic passes
      `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText` and
      `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample`.
- [ ] `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` passes with the mail
      example (`--mail-domain example.com --mailbox-file mailboxes.csv imap://127.0.0.1:0/`).
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-653 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-06: Backlog -> Doing.
