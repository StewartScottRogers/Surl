---
id: BL-388
title: Parse upstream curl test case files
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-386, BL-387]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] Named tests cover each preprocessing feature and each section attribute that FILEFORMAT.md
      lists, on small inline cases. Each test checks the exact resulting bytes. `crlf="headers"`
      converts only the header part.
- [x] Variables are substituted from a supplied map. An unknown `%VARIABLE` is left as written, as
      upstream's preprocessor does (check `tests/testutil.pm` at the tag and cite it in a comment).
- [x] A test in `Surl.Conformance.UnitTests` reads every imported `test*` file. It passes when each
      file parses or appears on an explicit list of files with a named reason, and Notes record that
      list.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- **Sources read at tag `curl-8_21_0`** (raw GitHub, 2026-11-04): `docs/tests/FILEFORMAT.md`,
  `tests/testutil.pm`, `tests/runner.pm` (`prepro`), `tests/servers.pm` (`subvariables`),
  `tests/getpart.pm`, `tests/runtests.pl` (how each verify part applies `nonewline`/`crlf`).
  Variable substitution lives in `servers.pm`'s `subvariables`, not `testutil.pm`: it runs one
  `s/%NAME/value/g` per name it knows, so an unknown `%WORD` stays as written. Both files are cited
  in `UpstreamTestCaseVariables`' remarks.
- **What was built** (`Surl.Conformance.UnitLibrary`): `UpstreamTestCasePreprocessor`,
  `UpstreamTestCaseConditionals`, `UpstreamTestCaseVariables`, `UpstreamTestCaseInstructions`,
  `UpstreamTestCaseNewlines`, `UpstreamTestCaseParser`, and the typed result `UpstreamTestCase` /
  `UpstreamTestCaseSection` / `UpstreamTestCasePart`, with `UpstreamTestCaseEnvironment`,
  `IUpstreamTestCaseFileReader` (+ `FileSystemUpstreamTestCaseFileReader`) and
  `UpstreamTestCaseFormatException` (source line number + problem). The library's `CLAUDE.md`
  describes them.
- **Choices taken (defaults, inside ADR-0080 decision 2; no ADR because they decide no behaviour
  Surl shows a client, and `Documentation/Planning/Decisions` is held by BL-403):**
  - Faithful to upstream's order and regexes, including its quirks: `crlf=` in preprocessing
    matches only double-quoted values; `<data` is a prefix match, so `<datacheck crlf=...>` is
    rewritten too; `crlf="yes"` rewrites only a line's first newline; `%days[]` yields
    `%alternatives[a,b]` for the comparer.
  - Text is Latin-1 throughout so every byte survives; `Regex` is interpreted (not
    `[GeneratedRegex]`), which is AOT-safe and keeps the generated matchers out of the coverage
    gate.
  - Stricter than `getpart` only where it would silently lose part of a case: a part outside every
    section, a part or section left open, a stray closing tag, text outside `<testcase>`. Text after
    a tag on its line is ignored, as `getpart`'s prefix match ignores it (tests 324, 1337, 2070);
    loose text inside a section is kept as the section's `Lines` (`<disable>` items, test 1206's
    note).
  - A file `%include`/`%includetext` names that cannot be read includes as nothing, as upstream's
    `includefile` (no `or die`); a file `%sha256b64file[]`/`%strippemfile[]` names that cannot be
    read is an `UpstreamTestCaseFormatException` naming the line, as upstream `die`s. The corpus
    test's reader returns empty for files the import does not hold (generated certificates,
    `src/tool_listhelp.c`).
  - `nonewline` and `crlf` count as set by Perl truthiness (present, not empty, not `0`), and
    `ContentWithNewlineAttributes` takes an `UpstreamTestCaseNewlineRule`, because runtests applies
    them three ways: cut then `crlf` (stdout, stderr, protocol, proxy, reply data, datacheck); cut
    then force CRLF whatever the value (client `<file>`, `<stdin>`); `crlf` then cut (verify
    `<file>`). `%repeat[]` is capped at 64 MiB per expansion so a malformed count is a format error.
    `%days[]` reads a leading decimal number as Perl numifies it.
  - Section attributes other than `nonewline`/`crlf` (`nocheck`, `sendzero`, `mode`, `option`,
    `timeout`, `delay`, `type`, `name`, `loadfile`, `host`) are read as written by
    `ParseAttributes`; their meaning is applied by later tasks. `mode="text"` and `<upload>`'s
    double `nonewline` cut are verification rules for BL-390. FILEFORMAT.md at the tag lists no
    `base64` attribute (only the `%b64[]` instruction).
  - A `code-reviewer` pass compared the code with the upstream Perl; its four byte-level findings
    (newline rule per part, missing include, truthiness, unwrapped exceptions) and the `\S`/0xA0
    and `%days[]` decimal points are fixed above, each with a test.
- **Unparsable cases at the tag** (listed in `UpstreamTestCaseCorpusTests`, 4 of 2,013): each has a
  part outside every section, which upstream's `getpart` never reads:
  - `test758` - `<features>` after `</reply>`, outside `<client>` (upstream never requires OpenSSL);
  - `test1569`, `test1570` - `<stdout>` after `</verify>` (never checked);
  - `test1614` - `<errorcode>` after `</client>`, outside `<verify>` (never checked).
- **Touches widened:** `Documentation/Wiki/Glossary.md`, whose `upstream test case` row named
  `UpstreamTestCase` as a planning name of BL-388; no task in `Doing` names it.
- **Verified:** library 100% line and branch, worst CRAP 10 (`Measure-CodeQuality.ps1 -SkipTestRun
  -Library Surl.Conformance.UnitLibrary` over the project's fast run); fast tests and
  `dotnet build -warnaserror` below in the Log.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. UpstreamTestCaseParser preprocesses and parses upstream curl test cases; 2,009 of 2,013 imported cases parse, 4 listed with reasons
