---
id: BL-875
title: Read a mail upstream test case's servercmd script and record its exchange in Surl.LineProtocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-875 — Read a mail upstream test case's servercmd script and record its exchange in Surl.LineProtocol

## Goal

`Surl.LineProtocol` holds `MailUpstreamTestCaseReplies`: one upstream test case's `<reply>` parts and
`<servercmd>` lines as the IMAP, POP3 and SMTP server halves play them, with a record of every command line and
uploaded message the half received. It is fully unit tested; no server uses it yet.

## Context

- First of BL-395's split (BL-395 timed out after 120 min; Stewart asked for the split 2026-11-05). The
  SMTP, POP3 and IMAP server halves build on this task, and the conformance run comes after them.
- Decision: ADR-0080 decision 2. Shared line-protocol instructions go in `Surl.LineProtocol`; the type takes
  BCL types only (parts by name as bytes, `<servercmd>` lines as strings) and is never composed by the shipped
  `surl`. The FTP precedent is BL-394's `FtpUpstreamTestCaseReplies` and `FtpUpstreamTestCaseServerCommands` in
  `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary`. Copy their shape and their tests' shape.
- FILEFORMAT.md at tag `curl-8_21_0`, "For FTP/SMTP/POP/IMAP" (lines 367 to 395). The instructions kept are
  `Testnum`, `REPLY` (with `welcome` for the greeting, a quoted `"<command> <arguments>"` form that matches the
  exact command text, and an empty command for an empty SASL response), `REPLYLF`, `COUNT`, `CAPA`, `AUTH` and
  `POSTFETCH`. `DELAY`, `SLOWDOWN`, `SLOWDOWNDATA`, `NOSAVE` and FTP's own words are refused with
  `NotSupportedException` so the selector skips the case `servercmd-unsupported:<command>`.
- Data parts: a message's number is the first run of digits in the name the client sends; over 10000 it picks
  `<dataNUM>` with NUM its remainder of 10000, falling back to `<data>`, as upstream's `getreplydata` does.
- **Reuse the stash.** `git stash list` and find the entry whose message is `darkfactory BL-395`; call it
  `stash@{N}` (indexes shift, so match by message). The two files this task needs are untracked files, stored
  in the stash's third parent. Restore them with
  `git checkout "stash@{N}^3" -- Protocols/Mail/Surl.LineProtocol.UnitLibrary/MailUpstreamTestCaseReplies.cs Protocols/Mail/Surl.LineProtocol.UnitLibrary/MailUpstreamTestCaseServerCommands.cs`
  and then `git reset` those paths, so they are not staged. They compiled in the lane but have **no tests**:
  writing the tests and closing the coverage gaps is this task's work. Ignore every other file in the stash,
  because BL-876, BL-877, BL-878 and BL-879 take them. Do not apply or drop the stash.
- Stand-in (ADR-0124): none applies. This is a test-only seam with no shared store, no events and no faults.

## Acceptance criteria

- [x] `Protocols/Mail/Surl.LineProtocol.UnitTests` has `MailUpstreamTestCaseServerCommandsTests` and
      `MailUpstreamTestCaseRepliesTests`. They cover each kept instruction (`REPLY` with `welcome`, the quoted
      full-text form and an empty command; `REPLYLF`; `COUNT` limiting a reply to N answers; `CAPA` with a
      double-quoted capability holding spaces; `AUTH`; `POSTFETCH`), the Perl escapes `\r`, `\n`, `\t`, `\xHH`
      and `\<char>`, the `<dataNUM>` choice and its fallback, and the received and uploaded byte records.
- [x] Each refused instruction (`DELAY`, `SLOWDOWN`, `SLOWDOWNDATA`, `NOSAVE`, `RETRSIZE`) throws
      `NotSupportedException` naming the word, and a malformed `COUNT` or unclosed quoted `REPLY` throws
      `FormatException`, each with a named test.
- [x] `MailUpstreamTestCaseReplies.SupportedServerCommands` is exactly `Testnum`, `REPLY`, `REPLYLF`, `COUNT`,
      `CAPA`, `AUTH` and `POSTFETCH`, pinned by a test.
- [x] `dotnet build Protocols/Mail/Surl.LineProtocol.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.LineProtocol.UnitLibrary` keeps
      100% line and branch coverage (`Measure-CodeQuality.ps1 -Library Surl.LineProtocol.UnitLibrary`).

## Notes

- The previous run (cut off) had already restored both files from the `darkfactory BL-395` stash and written both test classes; this run measured them. `FindData` measured cyclomatic complexity 14, so it now calls `ReadFirstNumber` and `FindNumberedPart`; behaviour is unchanged and the library is at 100% line and branch coverage with no failing member (worst CRAP 8).
- The project's CLAUDE.md now lists `MailUpstreamTestCaseReplies` and `MailUpstreamTestCaseServerCommands`.
- No new ADR: every decision here was already made by ADR-0080 decision 2 and the task's Context.

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-395 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MailUpstreamTestCaseReplies and its server commands are fully tested at 100% coverage
