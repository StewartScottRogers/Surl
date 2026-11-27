---
id: BL-1957
title: Name Surl.Composition as the loader of the mail store in the Surl.MailStore notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1957 — Name Surl.Composition as the loader of the mail store in the Surl.MailStore notes

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

- [ ] The `CLAUDE.md` says `Surl.Composition`'s `SurlServerComposition.LoadMailStoreAsync` loads the store.
- [ ] No line of that `CLAUDE.md` says `Surl.Console` loads or composes the store.
- [ ] Only that `CLAUDE.md` changes.

## Notes

## Log

- 2026-11-27: Created.
