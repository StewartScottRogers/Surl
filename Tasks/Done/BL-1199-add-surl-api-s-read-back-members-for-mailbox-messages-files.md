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
completed: 2026-11-15
---
# BL-1199 — Add Surl.Api's read-back members for mailbox messages, files and bus messages

## Goal

A test holding a `SurlServer` reads back mailbox messages through `server.Mail`, files and folder listings through `server.Files`, and queue, retained and log messages through `server.Messages`, with the members BL-1194's ADR names.

## Context

- Decision: the ADR filed by BL-1194. Composition members: BL-1196 (mail), BL-1197 (content) and BL-1198 (bus) on `ComposedStores`.
- Where: `Application/Surl.Api/SurlServerMail.cs`, `SurlServerFiles.cs` and `SurlServerMessages.cs`, which today only write. Follow their style: async, `CancellationToken` last, argument checks as `SeedMailAsync`'s wrappers do, and public result records in `Surl.Api` rather than composition types (as `SurlExchangeRecord` and `SurlAccount` do).
- Stand-in fit: `await server.Mail.ListMessagesAsync("bob")` (or the ADR's name) replaces an IMAP client in a test that checks what the code under test sent. Reads change nothing, so they work before or after a reset and on a `DataDirectory` server.

## Acceptance criteria

- [x] `SurlServerMail`, `SurlServerFiles` and `SurlServerMessages` have the read members the ADR names, each with an XML doc comment.
- [x] Tests in `Application/Surl.Api.UnitTests` start a `SurlServer` on port 0, seed through the existing members, and read back exact bytes for one mail message, one file, one queued message, one retained message and one log record.
- [x] A test shows each not-found case gives the ADR's result, and a test shows a read after `server.ResetAsync()` finds nothing.
- [x] A test shows a read after `StopAsync` throws `ObjectDisposedException`, as the write members do.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests pass, and no test needs `TestCategory=Integration`.

## Notes

- Read members follow ADR-0164 decision 5; results are Surl.Api records (`SurlMailboxInfo`, `SurlMessagePage`, `SurlMailMessage`, `SurlFileContents`, `SurlFolderPage`, `SurlQueuePage`, `SurlRetainedMessage`, `SurlLogPage`), absent items throw `SurlNotFoundException`, refusals `SurlReadRefusedException`; an absent retained message is null.
- Defaults taken: mail flags are IMAP names (`Seen`); `afterUid` and `uid` are long, outside 0..uint.MaxValue is refused (afterUid) or refused (uid).
- The existing write members do not throw `ObjectDisposedException` after stop (the criterion assumed they did); only the new read members do, through a stopped check `SurlServer` hands each area. Writes are unchanged so as not to alter today behaviour.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Read members for mail, files and bus added to Surl.Api
