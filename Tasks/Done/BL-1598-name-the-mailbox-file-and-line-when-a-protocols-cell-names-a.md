---
id: BL-1598
title: Name the mailbox file and line when a protocols cell names a scheme with no login, in MailboxFileReader
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1598 — Name the mailbox file and line when a protocols cell names a scheme with no login, in MailboxFileReader

## Goal

`MailboxFileReader` can be given the schemes whose servers make no login, and refuses a record whose
`protocols` cell names one with `(2) Mailbox file <file>, line <n>: scheme <scheme> has no login`, as it already
refuses `unknown scheme <scheme>`.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 1 (Low):
  `f.csv` = `address,password,protocols` / `ann@example.com,annpass,gopher`, then
  `surl --mail-domain example.com --mailbox-file f.csv smtp://127.0.0.1:0/` prints
  `surl: (2) scheme gopher has no login`, with no file or line. `nosuch` in the same cell gets
  `surl: (2) Mailbox file f.csv, line 2: unknown scheme nosuch`.
- The `--aihelp` prose (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:229`) and the manual
  (`Application/Surl.Cli.UnitLibrary/ManualText.cs:319` onwards) already promise that a malformed record exits 2 with
  `surl: (2) Mailbox file <file>, line <n>: <reason>`, so the code contradicts its own help today.
- Cause in today's code: the no-login check is not in the file reader at all. It runs once at start over every
  mailbox, from `--mailbox` and `--mailbox-file` alike, in
  `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:205`
  (`SchemeLoginCheck.FindRefusal`, `Application/Surl.Composition.UnitLibrary/SchemeLoginCheck.cs:34`), after
  `MailboxComposition.Compose` has merged the file's records and dropped their line numbers.
- ADR-0102 amendment of 2026-11-20 (BL-1483), "When": only composition knows the registrations, so `Surl.Cli`
  cannot derive the no-login schemes itself. The reader therefore *receives* them, as it already receives
  `extendedSchemes` (`MailboxFileReader.cs:53`), and keeps no list of its own (no central switch).
- Where the check goes: `MailboxFileReader.ReadMailbox` (`Application/Surl.Cli.UnitLibrary/MailboxFileReader.cs:151`),
  after `ConfiguredMailboxReader.Build` (line 169) has accepted every scheme as served, so an unknown scheme keeps its
  own refusal first. Its reason goes through `LineFailure` (line 124) like every other record refusal.
- Composition passing the set is BL-1599; this task keeps today's behaviour for every caller that passes none.
- Stand-in: no store, event or fault changes. A test starting through `Surl.Api` or `surl-mcp` with a mailbox file
  gets the same refusal text, since both start through `Surl.Composition`.

## Acceptance criteria

- [x] `MailboxFileReader` gains a constructor overload (or optional parameter) taking
      `IReadOnlyCollection<string> schemesWithoutLogin`, compared without regard to case; the existing two-argument
      constructor behaves exactly as today (an empty set).
- [x] A record whose `protocols` cell names a scheme in that set is refused with
      `(2) Mailbox file <file>, line <n>: scheme <scheme lower-cased> has no login`, `<n>` the line the record starts
      on, and the file's mailboxes are empty; the scheme check order is: unknown scheme first, then no login.
- [x] New tests in `Application/Surl.Cli.UnitTests/MailboxFileReaderTests.cs`:
      `Read_ProtocolsNamingASchemeWithoutLogin_IsRefusedWithFileAndLine` (the report's `gopher` record, line 2),
      `Read_ProtocolsNamingASchemeWithoutLoginInAnyCase_IsRefusedLowerCased` (`GOPHER`),
      `Read_ProtocolsNamingOnlySchemesWithLogin_IsRead`, and
      `Read_SchemesWithoutLoginNotGiven_AcceptsAServedSchemeAsToday`, plus a null-argument test for the new parameter.
- [x] No refusal holds the password (the existing password-absence assertion style in the test file is used).
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"`
      is green, and `Surl.Cli.UnitLibrary` stays at 100% line and branch coverage.

## Notes

The help texts need no change: they already describe this refusal. ADR-0102's amendment says the check runs at
start; the mailbox file is read at start, so this is still true.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. MailboxFileReader refuses a no-login scheme with file and line
