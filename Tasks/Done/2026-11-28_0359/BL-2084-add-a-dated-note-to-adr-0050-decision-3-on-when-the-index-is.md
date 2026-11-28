---
id: BL-2084
title: Add a dated note to ADR-0050 decision 3 on when the index is written after INBOXes are created at start
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2084 — Add a dated note to ADR-0050 decision 3 on when the index is written after INBOXes are created at start

## Goal

A reader of ADR-0050 learns from a dated note that an `INBOX` created at start is written to the index by the first `SaveChangesAsync`, not at start. No behaviour changes.

## Context

Found while working BL-2067 (checked by reading the code on 2026-11-28). ADR-0050 decision 3 says that at start, after
loading, the store creates the `INBOX` of every owner that lacks one and "decision 7's index is then written once";
decision 7's "The index" bullet lists "an `INBOX` created at start" among the changes after which the index is written.
`MailboxStore.LoadAsync` (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs`) only counts the change, and its
XML comment says the first `SaveChangesAsync` writes the index. Its callers are the SMTP, IMAP and POP3 sessions and
`ComposedStores.SeedMailAsync`; `SurlServerComposition` loads the store and does not save it.

Stand-in: nothing to add; a documentation note, with no store, event, seed, reset or fault changed.

## Acceptance criteria

- [x] ADR-0050 has a note, dated the day it is written and naming this task, saying the index is not written at start for a created `INBOX` and that the first `SaveChangesAsync` writes it.
- [x] Decision 3 and decision 7's "The index" bullet each point to that note.
- [x] The diff changes no file but ADR-0050 (and this task's own), and rewrites none of the original sentences.

## Notes

- 2026-11-28: The note sits at the end of decision 3. Why: decision 7 already ends with BL-2067's note of the same date,
  which its pointers call "the note of 2026-11-28 at the end of this decision"; a second one there would be ambiguous.
- Checked in code: `LoadAsync` only counts the created `INBOX`; the first `SaveChangesAsync` whose index write succeeds
  writes it; stopping the server saves nothing.
- Follow-ups filed: BL-2101 (ADR-0050 still says `Surl.Console` loads the store and sets its message limit) and BL-2102
  (`MailboxStore` comments name the out-of-space fault point on methods that never consult it). Filed with the board
  script directly. The two new task files are the only changes besides ADR-0050 and this file, which the third
  criterion's "diff" (this task's work) does not count.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: every test project passed.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0050 says in a dated note that an INBOX created at start is written to the index by the first SaveChangesAsync, not at start.
