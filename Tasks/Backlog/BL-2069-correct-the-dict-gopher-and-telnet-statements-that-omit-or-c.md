---
id: BL-2069
title: Correct the DICT, Gopher and TELNET statements about fault rules and the fault point
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2053]
touches: [Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/GopherProtocolServer.cs, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/CLAUDE.md, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictCommandResponder.cs, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/CLAUDE.md, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2069 — Correct the DICT, Gopher and TELNET statements about fault rules and the fault point

## Goal

The Gopher server remarks, the DICT responder's doc comments and the DICT, Gopher and TELNET library notes state
truly which fault rules are applied, when a DICT answer ends the connection, and what the TELNET fault point carries.

## Context

Found while working BL-2053 (2026-11-28), read from source. Documentation only; no behaviour change. References:
ADR-0138, ADR-0141 and BL-914. BL-2053 corrects the doc comments of `DictCommandFaults`, `GopherSelectorFaults`
and `TelnetLineFaults`; this task covers the statements outside those three files.

- `Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/GopherProtocolServer.cs`, class remarks (the paragraph "Before
  the content store is asked"): it mentions only the chosen-error rule. `ServeAsync` also applies `delay` and `drop`
  through `GopherSelectorFaults.FindAndApplyAsync`.
- `Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictCommandResponder.cs`, `AnswerAsync`: `<returns>` says
  `false` only after `QUIT`. It is also `false` when a `drop` rule matches, and when a definition file cannot be
  read while it is sent (`AnswerDefineAsync` aborts the connection). The remarks mention only the chosen error.
- `Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/CLAUDE.md`, "Line faults": it says the fault point is
  "listener and scheme only". `TelnetLineFaults.FindAndApplyAsync` sets `ExchangeId`, `ListenUrl` and `Scheme`.
- The "Chosen-error fault" paragraphs of the DICT and Gopher libraries' `CLAUDE.md`: they say "a rule of another
  kind is noted and not applied" just before saying `delay` and `drop` are applied. The TELNET `CLAUDE.md` wording
  ("a rule of a kind other than `error`, `delay` or `drop`") is the correct form. The same two paragraphs do not
  say that a `delay` hold ends early when the client closes or resets the connection, which TELNET's also states
  and which holds for both servers, since each passes its connection to `CommandStageFaults.FindAndApplyAsync`.

This spans three protocol libraries, one more than the usual sizing limit: it is a few comment and note lines, kept
as one task. It adds no store, event or fault, so the stand-in questions do not apply.

## Acceptance criteria

- [ ] `GopherProtocolServer`'s class remarks say that before the content store is asked, a `delay` rule holds the selector, a `drop` rule aborts the connection and a fitting `error` rule is answered with an error menu carrying its text.
- [ ] `DictCommandResponder.AnswerAsync`'s `<returns>` says `false` after `QUIT`, after a `drop` rule, and when a definition cannot be read while it is sent; its remarks say what a `delay`, a `drop` and a fitting `error` rule each do.
- [ ] The TELNET library's `CLAUDE.md` says the fault point carries the exchange ID, the listen URL and the scheme only, with no account, command or path.
- [ ] The DICT and Gopher libraries' `CLAUDE.md` say that only a reply that does not fit, or a rule of a kind other than `error`, `delay` or `drop`, is noted and not applied, and that a `delay` hold ends early when the client closes or resets the connection.
- [ ] The diff to the two `.cs` files changes comment lines only.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
