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
completed:
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

- [ ] A test in `Surl.MailStore.UnitTests` saves a store with a configured use and a use given to `CreateMailbox`, loads it again, and reads both uses back.
- [ ] A test loads an index written in today's format and gets its mailboxes with no error.
- [ ] An ADR states the index format change and how a configured use applies to a mailbox already in the index.
- [ ] `Surl.MailStore.UnitLibrary/CLAUDE.md` says what the index keeps of folder uses.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
