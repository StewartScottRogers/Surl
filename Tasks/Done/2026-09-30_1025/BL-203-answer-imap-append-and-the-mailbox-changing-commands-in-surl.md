---
id: BL-203
title: Answer IMAP APPEND and the mailbox-changing commands in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-202]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md]
requirement: FR-044
created: 2026-09-29
completed: 2026-09-30
---
# BL-203 — Answer IMAP APPEND and the mailbox-changing commands in Surl.Protocol.Imap

## Goal

`ImapProtocolServer` answers `APPEND` (curl's `-T`) and the commands that change mailboxes and
messages, which curl sends through `-X` - `CREATE`, `DELETE`, `RENAME`, `SUBSCRIBE`,
`UNSUBSCRIBE`, `STORE`, `COPY`, `EXPUNGE` and their `UID` forms, and any other BL-187's ADR
lists - against the mail store.

## Context

- Decisions: BL-187's ADR (responses, flags, `APPEND`'s literal bound, whether `--allow-uploads`
  gates it per BL-184's ADR); ADR-0006 section 5 (an `APPEND` past `--max-filesize` answered
  tagged `NO`, nothing stored); RFC 3501 sections 6.3.3 to 6.3.11, 6.4.3, 6.4.6, 6.4.7.
- Fixtures: BL-187's recordings of `-T` and the `-X` commands.

## Acceptance criteria

- [x] A fast test replays each fixture named in Context and asserts surl's responses and the
      store's contents afterwards.
- [x] Fast tests cover: `APPEND` to a missing mailbox (`[TRYCREATE]` if the ADR says so); an
      `APPEND` literal past `--max-filesize`; `DELETE` and `RENAME` of `INBOX`; `EXPUNGE`'s
      untagged responses in order; `STORE` with `+FLAGS`, `-FLAGS` and `.SILENT`; `COPY` to a
      missing mailbox; a write refused where BL-184's ADR gates it.
- [x] `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- `touches` widened (2026-09-30), no task in `Doing` names these: `Surl.MailStore.UnitLibrary`
  and its tests gain `MailboxStore.Move` (copy and remove in one locked step, so `MOVE` is all or
  none) and an `Expunge` overload taking UIDs (`UID EXPUNGE <set>`), which the store had no way to
  do; ADR-0055 gains decision 17, the details this task settled, as BL-202 added decision 16.
- Fixtures (criterion 1): BL-187 recorded no files for `-T` or `-X` changes, so twelve were
  recorded afresh with pinned curl 8.21.0 and `Record-CurlExchange.ps1 -Imap` (`append`, `create`,
  `delete`, `rename`, `subscribe`, `unsubscribe`, `store`, `uid-store-silent`, `copy`, `uid-move`,
  `expunge`, `uid-expunge`; `Fixtures/README.md`); curl exited 0 on each.
  `RecordedFixtureTests.ServeAsync_RecordedChange_WritesTheRecordedResponsesAndChangesTheStore`
  replays each byte for byte and asserts every mailbox's UIDs and flags afterwards;
  `ServeAsync_RecordedAppend_StoresTheUploadedBytesExactly` checks the appended bytes. The recorder
  always sends `+` before a literal, so the refusals sent in its place are pinned by fast tests only.
- Criterion 2: `ImapAppendTests` (TRYCREATE, TOOBIG past `--max-filesize` before `+`, OVERQUOTA,
  SERVERBUG, a close inside the literal, bad flags and dates), `ImapMailboxChangeTests` (`DELETE
  INBOX` is `NO [CANNOT]`, `RENAME INBOX` moves its messages and leaves it empty),
  `ImapMessageChangeTests` (`EXPUNGE` highest number first, `STORE` with `+FLAGS`, `-FLAGS` and
  `.SILENT`, `COPY` and `MOVE` to a missing mailbox are `TRYCREATE`). The write gate: ADR-0055
  decision 8 and ADR-0050 decision 5 say `--allow-uploads` does not gate `APPEND`, so the gated
  writes are those of a read-only (`EXAMINE`) view, `NO [READ-ONLY]`
  (`ServeAsync_ChangeUnderExamine_AnswersReadOnly`), and a change before a login.
- Criterion 3: `dotnet build` clean (warnings are errors), 616 IMAP and 238 mail store tests pass
  on in-memory connections; `Measure-CodeQuality.ps1` reports 100% line and branch, no failing
  member, for both `Surl.Protocol.Imap.UnitLibrary` and `Surl.MailStore.UnitLibrary`.
- Review (code-reviewer) found three defects, all fixed with tests: a `date-time` whose UTC moment
  is outside years 1 to 9999 threw and ended the session (now `BAD`); `MOVE`'s view update was
  quadratic in the moved UIDs; a `STORE` racing another session's expunge could report flags for
  the gone message.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ImapProtocolServer answers APPEND, CREATE, DELETE, RENAME, SUBSCRIBE, UNSUBSCRIBE, STORE, COPY, MOVE, EXPUNGE and their UID forms; 12 pinned-curl fixtures replay
