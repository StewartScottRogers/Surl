---
id: BL-2085
title: Correct the DICT and TELNET statements that leave out or misstate what fault rules do
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2069]
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictCommandResponder.cs, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictProtocolServer.cs, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/CLAUDE.md, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/CLAUDE.md, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2085 — Correct the DICT and TELNET statements that leave out or misstate what fault rules do

## Goal

The DICT server's doc comments and the DICT, Gopher and TELNET library notes state truly where a DICT reply's text
comes from, what the exchange context supplies, how a DICT session ends, and what a TELNET `error` rule's text
replaces.

## Context

Found while working BL-2069 (2026-11-28), read from source. Documentation only; no behaviour change. References:
ADR-0138, ADR-0141 and BL-914. File names below are under `Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/` unless
a path says otherwise.

- `DictCommandResponder.cs`, class remarks: "The reply text is Surl's own fixed text" is false for a chosen-error
  reply. `AnswerAsync` writes the matching fault rule's `Reply` followed by CRLF.
- `DictCommandResponder.cs`, constructor `<param name="context">`: it says "for its cancellation token". The context
  also supplies the log (`AnswerDefineAsync`), and the fault plan and event stream (`DictCommandFaults`).
- `DictProtocolServer.cs`: the class summary and the `ServeAsync` summary list how a session ends and leave out a
  `drop` fault rule. `ServeAsync`'s also leaves out a definition that cannot be read, or whose bytes cannot be
  written, while it is sent (`DictCommandResponder.AnswerDefineAsync` aborts the connection). The class remarks
  never mention fault rules; `DictCommandFaults`'s remarks say what each kind does.
- `Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/CLAUDE.md`, "Line faults": it says an `error` rule's text is
  sent "in place of the echo". On a `quit` line it replaces `bye` (`TelnetSession.AnswerAwaitingLine`).
  `TelnetProtocolServer`'s remarks say "the line's answer", which is correct.
- The `CLAUDE.md` of the DICT library and of `Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/`: the paragraph
  headed "Chosen-error fault" also covers `delay` and `drop` rules, so the heading names less than the paragraph
  says. TELNET's equivalent is headed "Line faults". The `Fixtures/README.md` sections of the same name record
  chosen-error fixtures and are not in scope.

The matching statement in `CommandStageFaults`'s class summary is BL-2070 and is not part of this task.

This spans three protocol libraries, one more than the usual sizing limit: it is a few comment and note lines, kept
as one task. It adds no store, event or fault, so the stand-in questions do not apply.

## Acceptance criteria

- [ ] `DictCommandResponder`'s class remarks say the reply text is Surl's own fixed text except a chosen-error reply, whose text is the fault rule's.
- [ ] `DictCommandResponder`'s constructor `<param name="context">` names the cancellation token, the log, the fault plan and the events.
- [ ] `DictProtocolServer`'s class and `ServeAsync` summaries include a `drop` fault rule among the ways a session ends, `ServeAsync`'s includes a definition that cannot be sent, and the class remarks say what a `delay`, a `drop` and a fitting `error` rule each do.
- [ ] The TELNET library's `CLAUDE.md` says an `error` rule's text is sent in place of the line's answer, the echo or `bye`.
- [ ] The "Chosen-error fault" headings in the DICT and Gopher libraries' `CLAUDE.md` are replaced by headings that cover `delay`, `drop` and `error` rules.
- [ ] The diff to the two `.cs` files changes comment lines only, and `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
