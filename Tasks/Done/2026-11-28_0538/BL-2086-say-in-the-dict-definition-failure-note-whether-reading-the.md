---
id: BL-2086
title: Say in the DICT definition failure note whether reading the file or writing to the connection failed
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2085]
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2086 — Say in the DICT definition failure note whether reading the file or writing to the connection failed

## Goal

When a DICT definition cannot be sent in full, the exchange log note says what failed: reading the definition file,
or writing to the connection.

## Context

Found while working BL-2069 (2026-11-28), read from source.
`Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictCommandResponder.cs`, `AnswerDefineAsync`, catches
`IOException` and `UnauthorizedAccessException` around `DictContentDictionary.CopyDefinitionAsync`. That copy reads
the file and writes each block through `DictTextWriteStream` to the connection, and `IConnection.WriteAsync` throws
`IOException` when the connection is aborted, reset or fails. The note nevertheless always reads
"`<location>` could not be read while it was sent (`<message>`); the connection was aborted.", which is false for a
write failure.

Keep that note, word for word, for a read failure
(`DictProtocolServerTests.ServeAsync_DefinitionUnreadableOnceAnnounced_AbortsTheConnectionAndNotesIt` pins it).
For a write failure, note "`<location>` could not be written to the connection while it was sent (`<message>`); the
connection was aborted." The two cannot be told apart by exception type, so the code has to record where the
exception came from. Nothing sent to the client changes, `AnswerAsync` still returns `false`, and the connection is
still aborted. The 150 and 151 lines and `DictTextWriteStream.EndTextAsync` are written outside this catch and stay
as they are. `Protocols/Lookup/Surl.Protocol.Dict.UnitTests/WriteStallingConnection.cs` shows how a test wraps the
in-memory connection; a write failure needs a double that throws `IOException`.

Check the doc comments of `DictCommandResponder` and `DictProtocolServer` (as BL-2085 leaves them) and the library's
`CLAUDE.md` stay true of the note.

Stand-in: the content store is the shared store, unchanged, and no new event or fault is added. The change is to
what a test or a person reads in the exchange log when a transfer fails.

## Acceptance criteria

- [x] A new test in `Surl.Protocol.Dict.UnitTests` makes the connection throw `IOException` while the definition's bytes are copied, and asserts the write-failure note above, that the connection is aborted and that no later command is answered.
- [x] `ServeAsync_DefinitionUnreadableOnceAnnounced_AbortsTheConnectionAndNotesIt` passes for both its data rows with its expected note unchanged.
- [x] `dotnet build Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary -warnaserror` is clean.
- [x] The fast tests of `Surl.Protocol.Dict.UnitTests` pass with no `TestCategory=Integration` test needed, and the library keeps 100% line and branch coverage.

## Notes

- `ContentStore.CopyFileBytesAsync` passes exceptions through unwrapped, so `DictTextWriteStream.HasFailedWrite`
  records that `IConnection.WriteAsync` threw `IOException` during the copy, and `DictCommandResponder` words the note
  from it. This follows `Surl.HttpServing`'s `ConnectionWriteStream.HasFailedWrite`. No ADR: the task fixes the
  wording, and nothing sent to the client, no option and no exit code changes, so no conformance stage ran.
- A `drop after-bytes` fault whose cut lands in the copy now gets the write-failure note.
- A write that fails in `EndTextAsync` or later still gets no DICT note, as the Context keeps it.
- Gates, 2026-11-28: solution build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30552 passed,
  3 skipped, 0 failed (Dict 166 of 166); `Measure-CodeQuality.ps1 -Library Surl.Protocol.Dict.UnitLibrary` over the
  Dict tests' coverage report: 0 failing members. Review: nothing under Must fix.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The DICT definition failure note now says whether reading the file or writing to the connection failed.
