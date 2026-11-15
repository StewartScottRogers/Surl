---
id: BL-1199
title: Add Surl.Api's read-back members for mailbox messages, files and bus messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1196, BL-1197, BL-1198]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1199 — Add Surl.Api's read-back members for mailbox messages, files and bus messages

## Goal

A test holding a `SurlServer` reads back mailbox messages through `server.Mail`, files and folder listings through `server.Files`, and queue, retained and log messages through `server.Messages`, with the members BL-1194's ADR names.

## Context

- Decision: the ADR filed by BL-1194. Composition members: BL-1196 (mail), BL-1197 (content) and BL-1198 (bus) on `ComposedStores`.
- Where: `Application/Surl.Api/SurlServerMail.cs`, `SurlServerFiles.cs` and `SurlServerMessages.cs`, which today only write. Follow their style: async, `CancellationToken` last, argument checks as `SeedMailAsync`'s wrappers do, and public result records in `Surl.Api` rather than composition types (as `SurlExchangeRecord` and `SurlAccount` do).
- Stand-in fit: `await server.Mail.ListMessagesAsync("bob")` (or the ADR's name) replaces an IMAP client in a test that checks what the code under test sent. Reads change nothing, so they work before or after a reset and on a `DataDirectory` server.

## Acceptance criteria

- [ ] `SurlServerMail`, `SurlServerFiles` and `SurlServerMessages` have the read members the ADR names, each with an XML doc comment.
- [ ] Tests in `Application/Surl.Api.UnitTests` start a `SurlServer` on port 0, seed through the existing members, and read back exact bytes for one mail message, one file, one queued message, one retained message and one log record.
- [ ] A test shows each not-found case gives the ADR's result, and a test shows a read after `server.ResetAsync()` finds nothing.
- [ ] A test shows a read after `StopAsync` throws `ObjectDisposedException`, as the write members do.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests pass, and no test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
