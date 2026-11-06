---
id: BL-880
title: Read --mail-domain names, --mailbox items and --mailbox-file CSV records into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-880 — Read --mail-domain names, --mailbox items and --mailbox-file CSV records into the Surl.Cli configuration model

## Goal

`Surl.Cli` can read a mail domain name, a `--mailbox` item and a `--mailbox-file` CSV file into the configuration
model (`ConfiguredMailbox`, and `SurlCommandLine.MailDomains`, `Mailboxes` and `MailboxFiles`), each read and
refused as ADR-0101 says, fully unit tested. No option row uses the readers yet; BL-883 adds the rows.

## Context

- First of BL-653's split (timed out after 120 min; Stewart asked for the split 2026-11-05). Order: this task
  and BL-881 (independent, can run side by side), then BL-882 (composition), then BL-883 (option rows and help
  text).
- Decisions: ADR-0101 decision 3 (item grammar, read by BL-645's `ConfigurationItemReader`), decision 4 (the
  options and the `--mailbox-file` CSV dialect), decision 5 (duplicates, conflicts and refusals, a refusal
  naming `<file>:<line>` and never the password) and decision 6 (bounds). ADR-0102 decisions 1.1 (domains,
  compared in ASCII lower case, one trailing dot dropped), 1.2 (a mailbox is an account named by its
  normalised address) and 1.4 (`protocols=`). The ADRs win over this summary.
- The model: `SurlCommandLine.MailDomains` (`IReadOnlyList<string>`), `Mailboxes`
  (`IReadOnlyList<ConfiguredMailbox>`) and `MailboxFiles` (`IReadOnlyList<string>`), each empty by default.
  Write their XML docs to say what each holds. BL-883 adds the options that fill them and names those options
  in the docs.
- **Reuse the stash.** `git stash list` and find the entry `darkfactory BL-653`; call it `stash@{N}` (match by
  message, since indexes shift).
  - Untracked files: `git checkout "stash@{N}^3" -- Application/Surl.Cli.UnitLibrary/ConfiguredMailbox.cs Application/Surl.Cli.UnitLibrary/ConfiguredMailboxReader.cs Application/Surl.Cli.UnitLibrary/MailDomainNames.cs Application/Surl.Cli.UnitLibrary/MailboxFileReader.cs Application/Surl.Cli.UnitTests/MailboxFileReaderTests.cs`,
    then `git reset` those paths.
  - Tracked edit: only the `SurlCommandLine.cs` hunk, from
    `git diff "stash@{N}^1" "stash@{N}" -- Application/Surl.Cli.UnitLibrary/SurlCommandLine.cs | git apply -3`.
  - Leave `CommandLineOptions.cs`, `CommandLineParser.cs`, the help, manual and aihelp files and
    `CommandLineParserMailTests.cs` to BL-883, and every `Surl.Composition` file to BL-881 and BL-882.
  The lane reported the Cli side green (1,014 tests) with these and BL-883's files together. Coverage was
  never measured. `ConfiguredMailboxReader` and `MailDomainNames` have no test file of their own in the stash,
  so write them.
- Stand-in (ADR-0124): none applies in this task. It reads configuration only. The mail store's seeding,
  events and faults are BL-799's and ADR-0127's.

## Acceptance criteria

- [ ] `Application/Surl.Cli.UnitTests` has `MailDomainNamesTests` (an IDN U-label converted to ASCII, case
      folded, one trailing dot dropped, a non-domain refused with `not a domain name`) and
      `ConfiguredMailboxReaderTests` (`<address>:<password>` split at the first colon, `;protocols=imap,pop3`,
      percent-encoded `%`, `;`, `:` and `,`, an unknown scheme refused, an address holding a colon refused,
      `ToString()` never showing the password).
- [ ] `MailboxFileReaderTests` read a 500-row CSV, a byte-order mark, CR LF and LF, quoted fields with doubled
      quotes, columns in any order and case, an empty `protocols` field and blank records. It refuses a missing
      header, non-UTF-8 input, an unclosed quote, text after a quoted field, a duplicate address, the 10,000
      mailbox and 16 MiB bounds, each with text naming `<file>, line <n>` and none showing a password.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Cli.UnitLibrary` keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-653 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-05: Backlog -> Doing.
