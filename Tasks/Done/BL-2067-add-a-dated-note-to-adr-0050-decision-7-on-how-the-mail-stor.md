---
id: BL-2067
title: Add a dated note to ADR-0050 decision 7 on how the mail store serialises index writes and reports failed saves
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2067 — Add a dated note to ADR-0050 decision 7 on how the mail store serialises index writes and reports failed saves

## Goal

A reader of ADR-0050 decision 7 learns from a dated note how index writes are serialised and how a failed index write
or message file delete reaches the server, where the code differs from what the decision says. No behaviour changes.

## Context

Found while working BL-2049 (checked by reading the code on 2026-11-28). The document is
`Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md`; the
store is in `Protocols/Mail/Surl.MailStore.UnitLibrary/`.

**Difference 1, serialisation.** The "**The index**" bullet says "Writes are serialised inside the store's lock". In
`MailboxStore.cs` they are serialised by a separate semaphore, `saveLock` (line 79), not by `storeLock` (line 78).
`SaveChangesAsync` (line 308) holds `saveLock` around `WriteChangesAsync` (line 1502), which takes `storeLock` to
encode the index and copy the released file numbers, writes the index with `storeLock` free, takes `storeLock` again
to record the saved change count and drop those numbers, then deletes the files, still holding `saveLock`. The
sentence's conclusion still holds: the file holds a state the store held.

**Difference 2, failed saves.** Under "**A write that fails while serving**", the bullets for the index and for a
message file that cannot be deleted say an outcome carries the exception message. `SaveChangesAsync` returns a `Task`
with no outcome, and throws:

- An index write that fails: the file system's exception comes out as thrown (such as `IOException` or
  `UnauthorizedAccessException`), after `MailStoreFiles.WriteReplacingAsync` deletes the temporary file. The saved
  change count does not advance and the released files stay queued, so the next save writes the index and then deletes
  them.
- A message file that cannot be deleted: the index is already written. `DeleteMessageFiles` (line 1455) tries every
  released file, then throws one `IOException` with the first failure's message. The numbers have left the queue, so no
  later save retries; the file is left behind and ignored at the next load.
- The three servers catch every exception but `OperationCanceledException` and note `Mail store: <exception message>`:
  `SmtpSession.SaveMailStoreAsync`, `ImapSession.SaveMailStoreIfChangedAsync` and
  `Pop3Session.RemoveDeletedMessagesAsync`.

Tests that already pin this, in `Protocols/Mail/Surl.MailStore.UnitTests/MailboxStoreSaveTests.cs`:
`SaveChangesAsync_IndexWriteThrows_LeavesTheStoreChangedAndTheNextChangeRewritesTheIndex` and
`SaveChangesAsync_MessageFilesCannotBeDeleted_AreReportedLeftBehindAndIgnoredAtTheNextLoad`. The XML comment on
`SaveChangesAsync` and the library's `CLAUDE.md` need no change.

The first bullet of that list (a message file that cannot be written) is not one of the two differences: the store
answers `MailStoreOutcome.StorageFailed`. If the note mentions it, the exception message is held in
`PendingMessage.StorageFailure`.

Stand-in: nothing to add; a documentation note, with no store, event, seed, reset or fault changed.

## Acceptance criteria

- [x] ADR-0050 has a note in or directly after decision 7, dated the day it is written and naming BL-2067, with a pointer to it from the "Writes are serialised inside the store's lock" statement and from the "A write that fails while serving" list.
- [x] The note says index writes are serialised by a semaphore of their own, apart from the store's lock, which is held only to take the index's bytes and released file numbers and to record the save, not while files are written or deleted.
- [x] The note says a failed index write makes `SaveChangesAsync` throw the file system's exception, leaves the change in memory and the released files queued, and that the next save writes the index.
- [x] The note says a failed message file delete makes `SaveChangesAsync` throw an `IOException` with the first failure's message, after the index is written and every released file is tried, and that the file is never retried and is ignored at the next load.
- [x] The note says the SMTP, IMAP and POP3 servers catch what `SaveChangesAsync` throws, other than cancellation, and note it as `Mail store: <exception message>`.
- [x] The diff changes no file but ADR-0050 (and this task's own), and rewrites none of decision 7's original sentences.

## Notes

- 2026-11-28: Added "*Note, 2026-11-28 (BL-2067)*" at the end of decision 7, with a pointer from the "The index"
  bullet and from "A write that fails while serving". Claims checked against `MailboxStore.cs`, `MailStoreFiles` and
  the three servers' sessions; they match the Context. The note also says `ComposedStores.SeedMailAsync` does not
  catch the exception.
- Docs only, no `.cs` or project file changed, so the `verify` skill was not needed.
- Decision 3's "index is then written once" at start also differs from the code; outside this task, filed as BL-2081.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0050 decision 7 carries a dated note on how index writes are serialised and how a failed index write or message file delete reaches the server
