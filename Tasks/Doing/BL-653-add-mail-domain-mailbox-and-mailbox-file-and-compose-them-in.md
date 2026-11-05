---
id: BL-653
title: Add --mail-domain, --mailbox and --mailbox-file and compose them into the mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-647, BL-648, BL-676, BL-614]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-653 — Add --mail-domain, --mailbox and --mailbox-file and compose them into the mail store

## Goal

`surl --mail-domain <domain> --mailbox <addr>:<password>[;protocols=…] --mailbox-file <csv>` configure real
mail domains and hundreds of mailboxes, each mailbox an account restricted to the protocols it names, composed
into the one `MailboxStore` and authentication policy the SMTP, IMAP and POP3 servers share.

## Context

- Decisions: BL-641's ADR (option names, item grammar, the `--mailbox-file` CSV dialect, refusals with
  `<file>:<line>`, bounds, help category, which option widens access) and BL-642's ADR decisions 1, 4 and 5 (what
  each option configures; a mailbox creating an account; `protocols=`). The ADRs win over this summary;
  `quota=` and `folders=` arrive with BL-654 unless the ADR places them here.
- Where: option rows and parsing into the configuration model in `Application/Surl.Cli.UnitLibrary` (BL-645's item
  reader); composition in `Application/Surl.Composition.UnitLibrary` - the mail configuration passed to the
  `MailboxStore` (BL-648) and the per-scheme authentication views (BL-647); reading the CSV through the same kind of
  seam as `--user-file`. The SMTP relaying reply is BL-676's.
- Default unchanged: with no `--mail-domain`, the composed store is ADR-0050's, and every existing test passes.
- `--aihelp` completeness (root `CLAUDE.md`): each option needs its argument type, categories, `OptionHelp`,
  `ManualText`, `AiHelpProse` paragraph, and an `AiHelpExamples` entry (with an `AiHelpExamplePrecondition` for
  the CSV file) that `CommandLineRunnerAiHelpTests` runs - which is why `Application/Surl.Console.UnitTests` is
  touched. If BL-641's ADR adds a `mail` help category, `AiHelpTextTests.Topics_…` grows by it here.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse each option (one, many, a duplicate address refused, a
      malformed item and a malformed CSV line refused with the ADR's exit code and text naming `<file>:<line>`, a
      password never echoed) and read a 500-row CSV.
- [ ] Named tests in `Application/Surl.Composition.UnitTests` show the store and policy composed: a configured
      mailbox's owner exists with its `INBOX`; its account logs in over `imap` and is refused over `smtp` when its
      `protocols=` says `imap,pop3`; no `--mail-domain` composes exactly as before.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` and `Surl.Composition.UnitLibrary` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
