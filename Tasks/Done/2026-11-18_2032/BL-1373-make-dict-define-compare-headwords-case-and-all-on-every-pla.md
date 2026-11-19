---
id: BL-1373
title: Make DICT DEFINE compare headwords case and all on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-1373 — Make DICT DEFINE compare headwords case and all on every platform

## Goal

DICT `DEFINE` finds a headword only when the client's spelling matches the file name ordinally, case and all, on
Windows as on Linux and macOS, so `DEFINE` and `MATCH ... exact` always agree.

## Context

- From the Surly cookbook bug report of 2026-11-18 (finding 2), reproduced on Windows at commit `3c5b39474`.
- Repro: folder `words/` with one file `surly` holding `bad-tempered and unfriendly\n`;
  `surl --directory words dict://127.0.0.1:2628/`, then upstream `curl -s dict://127.0.0.1:2628/d:SURLY` returns
  `150 1 definitions retrieved` and `151 "SURLY" surl "Files served by surl"` (echoing the client's spelling, not the
  stored headword), while `m:SURLY:*:exact` returns `552 no match` and `m:surly:*:exact` returns the match.
- `surl --aihelp dict` (`AiHelpProse.cs:123`) and `DictContentDictionary.cs:10` both say headwords are compared case
  and all, so `d:SURLY` must be `552 no match`.
- Cause: `DictContentDictionary.FindDefinition` (`Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictContentDictionary.cs:33`)
  maps the word to a path and asks the content store for the file's status, and the Windows file system ignores case.
  `MatchHeadwords` compares listed names ordinally, so it is right.

## Acceptance criteria

- [x] `FindDefinition` returns a file only when a listed headword equals the word ordinally (e.g. confirm the name
      against the directory listing, or look the word up in the listing), so the result does not depend on the
      file system's case rules.
- [x] A named fast test with an in-memory or temp store holding `surly` asserts `DEFINE SURLY` answers `552 no match`
      and `DEFINE surly` answers 150/151/250 with the 151 line naming `"surly"`. It is not marked `OSCondition`.
- [x] A named fast test asserts `DEFINE` and `MATCH ... exact` agree for a differently-cased word.
- [x] `dotnet build` is clean, fast tests green, Surl.Protocol.Dict stays at 100/100 coverage.

## Notes

- The 151 line echoing the client's spelling disappears once only exact-case words are found; check the 151 line
  still uses the word as asked (identical to the headword by then).

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
- 2026-11-18: Doing -> Done. DEFINE now confirms the headword against the ordinal directory listing
