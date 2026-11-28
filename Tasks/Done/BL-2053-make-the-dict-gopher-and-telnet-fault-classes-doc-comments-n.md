---
id: BL-2053
title: Make the DICT, Gopher and TELNET fault classes' doc comments name the whole fault point and the applied delay and drop
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2053 — Make the DICT, Gopher and TELNET fault classes' doc comments name the whole fault point and the applied delay and drop

## Goal

A reader of `DictCommandFaults`, `GopherSelectorFaults` and `TelnetLineFaults` learns what each fault point carries and which rule kinds are applied.

## Context

Found while working BL-2009 (2026-11-27), read from source. Doc comments only; no behaviour change. ADR-0138
(decision 3's note and decision 5's amendment) and BL-914 are the reference.

- `GopherSelectorFaults`, class remarks: they say only a fitting `Error` rule is applied and any other kind is noted
  as not applied. False: `delay` and `drop` are applied, through `CommandStageFaults.FindAndApplyAsync`.
- All three class summaries leave out part of the point each builds: `DictCommandFaults` and `GopherSelectorFaults`
  do not name the listen URL or the exchange ID; `TelnetLineFaults` says "the listener and the scheme only" and does
  not name the exchange ID.

## Acceptance criteria

- [x] `GopherSelectorFaults`'s remarks say a `delay` rule holds the selector, a `drop` rule aborts the connection and a fitting `error` rule is answered, and that only another kind, or a reply that does not fit, is noted as not applied.
- [x] The class summaries of `DictCommandFaults`, `GopherSelectorFaults` and `TelnetLineFaults` each name the listen URL and the exchange ID as part of the fault point.
- [x] The diff to the three `.cs` files changes comment lines only.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-28: Doc comments edited by `align-and-document`. The Gopher class summary now also names hold and drop, and
  the Gopher and DICT remarks say a hold ends early when the client closes or resets the connection
  (`CommandStageFaults`, ADR-0141 amendment 2).
- Verified: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 30655
  passed, 0 failed, 3 skipped; a diff filter found no changed non-comment line.
- Related false or incomplete statements outside these criteria (including the TELNET `CLAUDE.md` "listener and scheme
  only") are filed as BL-2065 and BL-2066.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The DICT, Gopher and TELNET fault classes' doc comments name the whole fault point and the applied delay and drop
