---
id: BL-2101
title: Add a dated note to ADR-0050 that Surl.Composition loads the mail store and sets its message limit
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2101 — Add a dated note to ADR-0050 that Surl.Composition loads the mail store and sets its message limit

## Goal

A reader of ADR-0050 learns from a dated note that `Surl.Composition`, not `Surl.Console` itself, loads the mail store and sets its message limit. No behaviour changes.

## Context

Found while working BL-2084 (checked by reading the code on 2026-11-28). ADR-0050 decision 6 says `Surl.Console` sets
`MailboxStore.MaxMessageBytes` from `ExchangeLimits.MaxUploadBytes`, and decision 7's "Loaded" bullet says
`Surl.Console` loads the store (BL-207). Both are done by `SurlServerComposition.LoadMailStoreAsync` in
`Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs`.

Stand-in: nothing to add; a documentation note, with no store, event, seed, reset or fault changed.

## Acceptance criteria

- [ ] ADR-0050 has a note, dated the day it is written and naming this task, saying `SurlServerComposition.LoadMailStoreAsync` loads the mail store and sets its message limit.
- [ ] Decision 6 and decision 7's "Loaded" bullet each point to that note.
- [ ] The diff changes no file but ADR-0050 (and this task's own), and rewrites none of the original sentences.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
