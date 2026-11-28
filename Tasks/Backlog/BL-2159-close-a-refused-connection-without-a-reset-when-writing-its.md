---
id: BL-2159
title: Close a refused connection without a reset when writing its refusal throws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2159 — Close a refused connection without a reset when writing its refusal throws

## Goal

A connection refused for a connection limit is never aborted by the serving engine: when its refusal writer throws, the engine notes why and closes it gracefully. This task changes nothing in `Documentation/Product/Requirements.md`; BL-2143 re-states NFR-017.

## Context

Found while working BL-2120 (2026-11-28). ADR-0006 section 5 says every limit hit is a graceful close and surl "never resets a connection for a limit; `Abort` stays for a peer that broke the protocol". `ServingEngine.RefuseAsync` in `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs` calls `connection.Abort()` and notes "the connection was reset" when the writer fails for a reason other than the deadline or shutdown. Instead, complete the writes (best effort, bounded by `ServingEngine.RefusalWriteDeadline` on the injected `TimeProvider`, a failure there ignored) and dispose, as ADR-0004 section 2 describes; the note still names the exception and says what was done. The tests `ServeAsync_RefusalWriterThatThrows_HasTheConnectionAbortedAndClosed` and `ServeAsync_RefusalWriterThatThrows_NotesWhyTheConnectionWasReset` in `ServingEngineTests.Limits.cs` pin the old behaviour and are renamed to match. Stand-in: a test using a connection limit sees the same graceful close however the refusal write went; nothing new to seed, reset or publish.

## Acceptance criteria

- [ ] A test in `ServingEngineTests.Limits.cs` shows a refusal writer that throws leaves the connection not aborted, its writes completed and the connection disposed.
- [ ] A test shows the note outside the exchange names the exception type and message and no longer says the connection was reset.
- [ ] A test shows a connection whose completion of writes also throws, or never completes within the deadline, is still disposed and not aborted.
- [ ] The XML or code comment on `RefuseAsync` describes the new behaviour, and `Foundation/Surl.Core.UnitLibrary` has no statement left that a refused connection is aborted.
- [ ] `dotnet build Foundation/Surl.Core.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
