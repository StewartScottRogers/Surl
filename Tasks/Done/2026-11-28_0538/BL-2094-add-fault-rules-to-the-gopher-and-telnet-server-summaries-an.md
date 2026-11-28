---
id: BL-2094
title: Add fault rules to the Gopher and TELNET server summaries and correct the ADR citations of DICT and TELNET replies
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2085]
touches: [Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/GopherProtocolServer.cs, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/TelnetProtocolServer.cs, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictCommandResponder.cs, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictProtocolServer.cs, Documentation/Planning/Decisions/ADR-0138-how-the-dict-gopher-and-telnet-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2094 — Add fault rules to the Gopher and TELNET server summaries and correct the ADR citations of DICT and TELNET replies

## Goal

The Gopher and TELNET server summaries, the DICT doc comments that cite ADR-0011 and ADR-0138's TELNET decision state
truly what fault rules do and which ADR records each reply.

## Context

Found while working BL-2085 (2026-11-28), read from source. Documentation only; no behaviour change.

- `GopherProtocolServer`'s class summary says every selector is answered with a file, a menu or an error menu and the
  connection is then closed. A `drop` fault rule aborts the connection with nothing written (its remarks say so).
- `TelnetProtocolServer`'s class summary says it echoes each line back until `quit`, and leaves fault rules to its
  remarks.
- `DictCommandResponder`'s class summary says "ADR-0011 records every reply below", and `DictProtocolServer`'s class
  summary says "ADR-0011 records the answers". A chosen-error reply is recorded by ADR-0138.
- ADR-0138, decision 3, says a TELNET chosen error is sent "in place of the line's echo". On a `quit` line it replaces
  `bye` (`TelnetSession.AnswerAwaitingLine`).

## Acceptance criteria

- [x] The class summaries of `GopherProtocolServer` and `TelnetProtocolServer` name fault rules among what changes the answer or ends the connection.
- [x] The class summaries of `DictCommandResponder` and `DictProtocolServer` cite ADR-0138 for the chosen-error reply beside ADR-0011.
- [x] ADR-0138 says a TELNET chosen error is sent in place of the line's answer, the echo or `bye`.
- [x] The diff to the `.cs` files changes comment lines only, and `dotnet build` is clean and the fast tests pass.

## Notes

- Done by `align-and-document`; the four Context claims were checked against the code and hold.
- Also corrected, in files the task already touches, because each was a false statement about the same behaviour:
  `GopherProtocolServer.ServeAsync`'s summary now says a `drop` rule aborts with no answer; ADR-0138 decision 3 now
  says "before the line is answered" (was "echoed") and "Data after `quit` completes no line" (was "A line after
  `quit` is never read" - the bytes are still parsed, `TelnetSession.AnswerData` discards the data).
- Those ADR-0138 changes are wording corrections to describe the code as it is, so no dated amendment note was added
  and no new ADR is needed.
- Verified 2026-11-28: build with 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests exit 0 with 0
  failures; no non-comment line changed in any `.cs` file. Integration tests not run (comments only).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Gopher, TELNET and DICT doc comments and ADR-0138 now state what fault rules do and which ADR records each reply
