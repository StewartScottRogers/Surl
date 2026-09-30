---
id: BL-227
title: Stream mail bodies into pending files and read them from their files on fetch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-191]
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests]
requirement: FR-047
created: 2026-09-29
completed:
---
# BL-227 — Stream mail bodies into pending files and read them from their files on fetch

## Goal

With a data directory, the mail store takes a message body as a stream written straight into
`messages/.pending-<guid>`, refuses the delivery or append with a typed `StorageFailed` outcome
carrying the exception message when that file cannot be written, and reads a message's bytes
from its file when a server fetches them instead of holding every message in memory.

## Context

- ADR-0050 decision 7: "the server streams the body into it as it reads", "With a data
  directory, a message's bytes are read from its file when a server fetches them, not held in
  memory", and the `StorageFailed` refusal for a message file that cannot be written.
- BL-191 built persistence with every message's bytes held in memory (loaded at start) and
  message files written by `MailboxStore.SaveChangesAsync` after the change; a message file
  write that fails there leaves the change in memory and is retried by the next save. This
  task replaces that part with the ADR's streaming shape. See BL-191's Notes.
- Changes the store's API the SMTP, IMAP and POP3 servers use (`Deliver`, `Append`,
  `FetchMessage`, `MaildropLock.ReadMessage`), so land it before or with BL-198, BL-201 and
  BL-205, whichever first needs it; `Surl.LineProtocol`'s dot-stuffed body reader writes to
  a caller's `Stream` (ADR-0050 decision 8).
- Tests use `Surl.Content.UnitLibrary/InMemoryContentFileSystem.cs` and
  `Surl.MailStore.UnitTests/UnitTestFaultingContentFileSystem.cs`.

## Acceptance criteria

- [ ] A fast test shows a body written through the store's pending stream lands in
      `messages/<n>` after the delivery commits, and a body abandoned before commit leaves no
      message and its pending file deleted.
- [ ] A fast test shows a pending file that cannot be written refuses the delivery and the
      append with `StorageFailed` carrying the exception message, and stores nothing.
- [ ] A fast test shows that with a data directory a fetched message's bytes come from its
      file (changing the file's bytes on the in-memory file system changes what is fetched),
      and that without one the bytes are held in memory as now.
- [ ] `dotnet build Surl.MailStore.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
