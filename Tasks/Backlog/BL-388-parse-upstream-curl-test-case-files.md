---
id: BL-388
title: Parse upstream curl test case files
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-386, BL-387]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-388 — Parse upstream curl test case files

## Goal

`Surl.Conformance.UnitLibrary` reads an upstream curl test case file into a typed `UpstreamTestCase`
(planning name). It applies the preprocessing that FILEFORMAT.md defines and substitutes the variables
for given ports and paths. Every file imported by BL-387 either parses or is reported with the line
that cannot be parsed.

## Context

- Decision: BL-386's ADR (names, and what is in scope).
- Format: `docs/tests/FILEFORMAT.md` at tag `curl-8_21_0`. It covers "Preprocessing" (`%b64[...]b64%`,
  `%hex[...]hex%`, `%repeat[...]%`, whitespace, `%CR`/`%SP`/`%TAB` and the other special characters,
  `%days[]`, `%include`, and `%if`/`%else`/`%endif` on features), "Variables" (the list from
  `%CLIENT6IP` to `%VERSION`), and the `<testcase>` sections with their attributes (`nonewline`,
  `crlf="yes|headers"`, `base64` and so on). The files resemble XML but are not parsed as XML
  documents: "lack of support for character entities is a big difference".
- Base class library only. No XML or parser package.
- Unit tests use inline snippets, with no network and no process. Test projects are exempt from the
  coverage gates, but the library is held to 100% line and branch coverage.

## Acceptance criteria

- [ ] Named tests cover each preprocessing feature and each section attribute that FILEFORMAT.md
      lists, on small inline cases. Each test checks the exact resulting bytes. `crlf="headers"`
      converts only the header part.
- [ ] Variables are substituted from a supplied map. An unknown `%VARIABLE` is left as written, as
      upstream's preprocessor does (check `tests/testutil.pm` at the tag and cite it in a comment).
- [ ] A test in `Surl.Conformance.UnitTests` reads every imported `test*` file. It passes when each
      file parses or appears on an explicit list of files with a named reason, and Notes record that
      list.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
