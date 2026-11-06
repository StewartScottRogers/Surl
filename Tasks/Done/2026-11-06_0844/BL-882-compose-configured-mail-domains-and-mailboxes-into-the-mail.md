---
id: BL-882
title: Compose configured mail domains and mailboxes into the mail store and accounts in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-880, BL-881]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-882 — Compose configured mail domains and mailboxes into the mail store and accounts in Surl.Composition

## Goal

When `SurlCommandLine.MailDomains`, `Mailboxes` or `MailboxFiles` are set, surl's start reads the mailbox files,
adds each mailbox's account after the `--user-file` accounts, and builds the one `MailboxStore` with a
`MailDomainConfiguration`. It notes the domains and mailboxes at `-v`. With none set, the start composes exactly
as before.

## Context

- Part of BL-653's split (timed out after 120 min; Stewart asked for the split 2026-11-05). Needs BL-880 (the
  model and `MailboxFileReader`) and BL-881 (per-scheme policies, which make `protocols=` bite). BL-883 then
  adds the options and help text.
- Decisions: ADR-0102 decisions 0 (no option, no change), 1.1 to 1.4, and 5 (what `Surl.Composition`
  composes and what `surl -v` notes: `* Mail domains: <domains>`, `* Mailbox <address>: logins on <schemes>`,
  and under `--allow-anonymous` `* Mail domains not applied: ...`). ADR-0101 decision 5 (a mailbox named like a
  `--user-file` account exits 2; a file that cannot be read exits 37 `Could not read mailbox file <file>`; a
  malformed record exits 2 naming `<file>, line <n>`).
- BL-648 (Done) made `MailboxStore` take `mailDomains` in its constructor and `LoadAsync`. BL-676 (Done) made SMTP
  refuse recipients outside them.
- The pieces:
  - `MailboxComposition.Compose(commandLine, readFile, extendedSchemes)` reads the files through the
    `SurlServerCompositionSeams.ReadStartFile` seam (so tests need no disk), giving `MailDomains`, `Accounts`
    and `WriteStartLines`.
  - `AuthenticationComposition.Compose` takes `mailboxAccounts`.
  - `LoadServiceStateAsync` and `LoadMailStoreAsync` take `mailDomains`.
  - `ComposedAuthentication` gains `Mailboxes`, and `ComposedServing` writes the mailbox notes after the
    groups' notes.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-653`; call it `stash@{N}`.
  - Untracked files: `git checkout "stash@{N}^3" -- Application/Surl.Composition.UnitLibrary/MailboxComposition.cs Application/Surl.Composition.UnitTests/MailboxCompositionTests.cs`,
    then `git reset` those paths.
  - Tracked edits, by hand from `git diff "stash@{N}^1" "stash@{N}" -- Application/Surl.Composition.UnitLibrary Application/Surl.Composition.UnitTests`:
    the mailbox hunks of `AuthenticationComposition.cs`, `ComposedAuthentication.cs` (`Mailboxes`),
    `ComposedServing.cs`, `SurlServerComposition.cs` (`mailDomains` parameters), `SurlServerComposition.Start.cs`
    (the `MailboxComposition.Compose` call and passing `mailboxes.Accounts` and `MailDomains`), and
    `SurlServerCompositionTests.Start.cs`.
  - BL-881 has already taken the per-scheme hunks. Master moved after the stash, so expect to re-make hunks by
    hand.
  The lane reported these Composition tests green (155) before measuring coverage.
- Stand-in (ADR-0124): this composes the shared mail store. It adds no event: the store's own events are
  ADR-0127's, and configured mailboxes are start-time state. Tests seed through the command line (and BL-799's
  seed and reset once it lands, which this task does not duplicate). A test observes a configured mailbox
  through the composed store's owners and `INBOX`.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Composition.UnitTests` show:
  - a configured mailbox's owner exists in the composed store with its `INBOX`;
  - its account logs in through the `imap` policy and is refused through the `smtp` policy when its
    `protocols=` says `imap,pop3`;
  - mailboxes from two `MailboxFiles` and from `Mailboxes` are all composed;
  - a mailbox named like a `--user-file` account exits 2 with the ADR's text;
  - an unreadable mailbox file exits 37, and a malformed record exits 2 naming `<file>, line <n>`;
  - with no `MailDomains`, the composed store and accounts are exactly as before.
- [x] A named test captures `-v` start output with the `* Mail domains:` and `* Mailbox <address>: logins on
      <schemes>` lines, and the `--allow-anonymous` note.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

- Reused the BL-653 stash (`28786c9f`): `MailboxComposition.cs` and its tests verbatim in
  substance; the `AuthenticationComposition`, `ComposedAuthentication`, `ComposedServing`,
  `SurlServerComposition(.Start)` hunks re-made by hand over BL-881 and ADR-0124's events and faults.
  `mailboxAccounts` and `mailDomains` are new trailing optional parameters, so every existing caller
  (Console tests included) is unchanged.
- The options are BL-883's and not parsed yet, so the tests set `MailDomains`, `Mailboxes` and
  `MailboxFiles` with `with` on a parsed command line (`MailboxCompositionTests.WithMail`).
- Choice: the start lists the extended schemes (which builds the extended servers once) only when
  there is a `--mailbox-file` to read; `--mailbox` items are already checked by the parser, so a
  start without files pays nothing.
- Choice: a mailbox named like a `--user-file` account is `(2) option --mailbox: mailbox <address> is also
  a --user-file account`, ADR-0101 decision 5 row 15's form with the `--user-file` noun, without the
  `try` line, as `--user-file`'s other composition-time refusals (ADR-0032 section 2).
- Tests: `MailboxCompositionTests` (15) and four `ComposeAsync_*` start tests; Composition 200 green,
  the whole fast run green (79 assemblies). The new lines and branches are all covered by
  `Surl.Composition.UnitTests` alone.

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-653 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. surl's start composes --mail-domain, --mailbox and --mailbox-file into the mail store and per-scheme accounts, with the -v notes
