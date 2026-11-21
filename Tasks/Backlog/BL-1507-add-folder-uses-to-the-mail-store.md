---
id: BL-1507
title: Add folder uses to the mail store
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1486]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1507 — Add folder uses to the mail store

## Goal

A mailbox in the mail store can hold an RFC 6154 use (`all`, `archive`, `drafts`, `flagged`, `junk`, `sent`, `trash`) that survives reset, snapshot, restore and rename, so the IMAP server and the hosts have one place to read it.

## Context

ADR-0055 Amendment 1 (`Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md`), decision 6, and ADR-0131 decisions 2-4 (reset, snapshot, restore). Code: `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxSettings.cs` and `MailboxStore.cs` (`RenameMailbox`, reset, snapshot and restore members). Only this library changes: the store holds the use and says nothing about `LIST` bytes. One use per folder and one folder per use per owner; a violation is refused by the store with a named `MailStoreOutcome`, which the IMAP task (BL-1510) maps to `NO [USEATTR]`. Store events are unchanged. Defaults keep today's behaviour (empty map).

## Acceptance criteria

- [ ] `MailboxSettings.FolderUses` exists (folder name to use, default empty) and a configured folder is created with its use.
- [ ] The store exposes a mailbox's use for reading and takes one at create time; a second folder of one owner given the same use is refused with a named outcome, and so is a use outside RFC 6154 section 2.
- [ ] Reset recreates each configured folder with its configured use and drops a use gained at runtime along with its mailbox.
- [ ] TakeSnapshot and Restore copy each mailbox's use beside its name and next UID.
- [ ] RenameMailbox carries the use to the new name; deleting a mailbox ends its use.
- [ ] Tests for each of the above exist and pass; the library keeps 100% line and branch coverage; `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
