---
id: BL-1976
title: Keep mailbox folder uses across a restart of a persisted mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1976 — Keep mailbox folder uses across a restart of a persisted mail store

## Goal

A mail store loaded from `--directory` holds the same RFC 6154 folder uses it held before it stopped, so IMAP
offers `SPECIAL-USE CREATE-SPECIAL-USE` and lists the uses after a restart.

## Context

Found while working BL-1956 (2026-11-27), read from source, not run; reproduce it with a test first. A test that runs
Surl as a stand-in with a persisted store loses its folder uses on a restart.

- `Protocols/Mail/Surl.MailStore.UnitLibrary/MailStoreIndex.cs` never writes or restores `StoredMailbox.Use`
  (`DecodeMailbox`, about line 116). BL-1507's Notes put persistence of uses out of its scope.
- `MailboxStore.AddConfiguredMailbox` (`MailboxStore.cs`, about line 1619) returns early for a mailbox that exists, so a
  configured use is not applied again to a loaded mailbox.
- A change to the index format must still read the indexes written today. Record the format and what a configured use
  does to a loaded mailbox that has another use, or none, in an ADR (ADR-0050 decision 7 and ADR-0055 Amendment 1 are
  where to start), marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [x] A test in `Surl.MailStore.UnitTests` saves a store with a configured use and a use given to `CreateMailbox`, loads it again, and reads both uses back.
- [x] A test loads an index written in today's format and gets its mailboxes with no error.
- [x] An ADR states the index format change and how a configured use applies to a mailbox already in the index.
- [x] `Surl.MailStore.UnitLibrary/CLAUDE.md` says what the index keeps of folder uses.

## Notes

- Reproduced first: 9 of the new `MailboxStoreFolderUsePersistenceTests` failed before the change.
- Decided in ADR-0192 (under Stewart's delegation): header `SURL-MAIL-INDEX-2` adds one use byte per mailbox and is
  written only when some mailbox holds a use, so a store with none writes today's bytes; a configured use wins over
  the index at start and is taken from any other mailbox of the owner. Why: the same command line gives the same
  uses on an old and a new directory, and a peer's `RENAME` cannot stop the next start.
- Loading writes nothing; an applied use reaches the index at the next save, as a start-up `INBOX` does.
- Pipeline: planned, tested and implemented in the lane's session rather than by one agent per stage (small
  one-library change); `code-reviewer` reviewed the diff and its findings are fixed. No `conformance-auditor` run:
  no wire byte changes, and ADR-0055 Amendment 1 already holds the measured `CAPABILITY` and `LIST` bytes.
- Gates: `dotnet build` clean, `dotnet format --verify-no-changes` clean, fast tests green (MailStore 455 passed),
  `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A persisted mail store keeps its RFC 6154 folder uses across a restart (ADR-0192)
