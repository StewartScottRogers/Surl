---
id: BL-1964
title: Name Surl.Composition as the loader of the mail store in the Surl.MailStore notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1964 — Name Surl.Composition as the loader of the mail store in the Surl.MailStore notes

## Goal

The `Surl.MailStore` project notes name `Surl.Composition`, not `Surl.Console`, as the project that loads the mail
store.

## Context

Found while working BL-1949 (2026-11-27), read from source. Project-notes text only.

- `Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md`, about lines 54-56: "`Surl.Console` loads the store (BL-207)".
- The code: `SurlServerComposition.LoadMailStoreAsync` in `Application/Surl.Composition.UnitLibrary` calls
  `MailboxStore.LoadAsync` over the `MailStoreFiles` that `ComposeMailStoreFiles` gives with `--directory`, and makes
  an in-memory `MailboxStore` without it.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [x] The `CLAUDE.md` says `Surl.Composition`'s `SurlServerComposition.LoadMailStoreAsync` loads the store.
- [x] No line of that `CLAUDE.md` says `Surl.Console` loads or composes the store.
- [x] Only that `CLAUDE.md` changes.

## Notes

- `align-and-document` made the edit in the "Persistence" text; checked against
  `SurlServerComposition.LoadMailStoreAsync` and `ComposeMailStoreFiles`. A search of that `CLAUDE.md` for "Console"
  finds nothing. "Only that `CLAUDE.md`" is about the documentation change; the board files (this task, BL-1977) are
  committed beside it.
- The new text also names BL-609, the task that moved composition into `Surl.Composition`.
- Filed BL-1977: the "Streaming" paragraph of the same file omits the in-memory case.
- Other `Surl.Console` composer claims the search turned up in the Authentication, Protocol.Abstractions, SSH and
  Telnet files have open tasks naming those projects (BL-1958, BL-1961, BL-1962, BL-1963, BL-1965); none filed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.MailStore notes name Surl.Composition's SurlServerComposition.LoadMailStoreAsync as the loader of the mail store; filed BL-1977.
