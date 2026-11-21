---
id: BL-1512
title: Show a mailbox's use and seed folder uses in Surl.Api
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1509]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1512 — Show a mailbox's use and seed folder uses in Surl.Api

## Goal

A test using `Surl.Api` can seed folders with uses in the same `<name>[=<use>]` form as the command line and read each mailbox's use back.

## Context

ADR-0055 Amendment 1, decision 6. Depends on the composition task (BL-1509), which maps folder uses into the store and adds `ComposedMailboxInfo.Use`. Code: `Application/Surl.Api/SurlMailboxInfo.cs` (record `Name, MessageCount, UnseenCount, UidValidity`), `SurlServerMail.cs` and the Api's mailbox configuration. `SurlMailboxInfo` gains a trailing optional `Use` parameter (null when none): an added field, and existing callers compile unchanged. Surl.Api is the embedding surface, so seeding must work on port 0 with several instances side by side.

## Acceptance criteria

- [x] The Api's mailbox configuration accepts `folders` items as `<name>[=<use>]`, with the command line's refusals for an unknown or doubled use.
- [x] `SurlMailboxInfo` has a trailing `Use` that is null without a use and the use text with one.
- [x] `ListMailboxesAsync` fills `Use`; after a store reset the configured use is back.
- [x] Two instances started side by side with different uses do not see each other's.
- [x] Tests exist and pass, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

The Api passes `Mailboxes` items through to the command-line parser, so `folders=<name>[=<use>]` and its refusals needed no new parsing; tests pin them. Only `SurlMailboxInfo.Use` and its fill were added.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Surl.Api seeds folder uses and ListMailboxesAsync returns Use
