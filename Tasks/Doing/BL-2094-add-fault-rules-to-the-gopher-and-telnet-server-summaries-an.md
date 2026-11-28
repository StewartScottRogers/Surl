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
completed:
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

- [ ] The class summaries of `GopherProtocolServer` and `TelnetProtocolServer` name fault rules among what changes the answer or ends the connection.
- [ ] The class summaries of `DictCommandResponder` and `DictProtocolServer` cite ADR-0138 for the chosen-error reply beside ADR-0011.
- [ ] ADR-0138 says a TELNET chosen error is sent in place of the line's answer, the echo or `bye`.
- [ ] The diff to the `.cs` files changes comment lines only, and `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
