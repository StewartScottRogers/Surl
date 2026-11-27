---
id: BL-1982
title: Say in the Surl.MailStore notes that a pending message is buffered in memory without a data directory
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1982 — Say in the Surl.MailStore notes that a pending message is buffered in memory without a data directory

## Goal

The "Streaming" paragraph of the `Surl.MailStore` project notes says where a pending message's bytes go both with and
without a data directory.

## Context

Found while working BL-1964 (2026-11-27), read from source. Project-notes text only.

- `Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md`, the paragraph starting "Streaming (BL-227": it says
  `PendingMessage.Body` "writes straight into `messages/.pending-<guid>`", with no condition.
- The code: the `PendingMessage` constructor in the same project buffers into a `MemoryStream` when the store has no
  `MailStoreFiles`, and creates a pending file only when it has them.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [ ] The paragraph says the pending file is used by a store with files.
- [ ] The paragraph says a store without files buffers the pending message in memory.
- [ ] Only that `CLAUDE.md` changes.

## Notes

## Log

- 2026-11-27: Created.
