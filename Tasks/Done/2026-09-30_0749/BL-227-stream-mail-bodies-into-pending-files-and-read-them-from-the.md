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
completed: 2026-09-30
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

- [x] A fast test shows a body written through the store's pending stream lands in
      `messages/<n>` after the delivery commits, and a body abandoned before commit leaves no
      message and its pending file deleted.
- [x] A fast test shows a pending file that cannot be written refuses the delivery and the
      append with `StorageFailed` carrying the exception message, and stores nothing.
- [x] A fast test shows that with a data directory a fetched message's bytes come from its
      file (changing the file's bytes on the in-memory file system changes what is fetched),
      and that without one the bytes are held in memory as now.
- [x] `dotnet build Surl.MailStore.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- New API: `MailboxStore.CreatePendingMessage()` gives a `PendingMessage` whose `Body` stream
  writes into `messages/.pending-<guid>`; `Deliver(recipients, PendingMessage)` and
  `Append(view, name, PendingMessage, ...)` rename it to the message's number or delete it,
  and dispose it either way. `OpenMessage` and `MaildropLock.OpenMessage` hand a server a
  stream; `FetchMessage` and `ReadMessage` still return the bytes whole, read from the file.
- Choice: a write to `Body` never throws for a failing pending file, nor past the most bytes
  the store could keep: it counts and drops the rest, so a server reads the whole body off the
  wire and answers `StorageFailed` or `MessageTooLarge` in its own words. A pending file that
  cannot be created, written, closed or renamed is `StorageFailed`.
- Choice: `Deliver(recipients, ReadOnlySpan<byte>)` keeps BL-191's shape (held in memory,
  written by the next save), so the SMTP server, outside this task's `touches`, gets no new
  outcome under it; the SMTP task moving to streaming switches to the pending overload.
  `Append(... bytes ...)` routes through a pending message, since no server calls it yet.
- Choice: a POP3 maildrop lock pins its messages' bodies (reference count), so an IMAP expunge
  meanwhile cannot delete a file the lock may still read; releasing the lock lets it go.
- Loading no longer reads message bytes; it checks each file is there at the index's size.
- No ADR: ADR-0050 decision 7 already decides the streaming shape; these are defaults under it.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Mail bodies stream into pending files, StorageFailed refuses unwritable ones, and fetch reads from the message file; 100% coverage
