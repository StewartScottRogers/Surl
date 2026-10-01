---
id: BL-343
title: Bring LibcurlBytes.Unescape and LibcurlRtspScript.ParseStep in Surl.Conformance under the complexity limit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-343 — Bring LibcurlBytes.Unescape and LibcurlRtspScript.ParseStep in Surl.Conformance under the complexity limit

## Goal

`Measure-CodeQuality.ps1` reports no failing member in `Surl.Conformance.UnitLibrary`.

## Context

- Found by BL-310 on 2026-09-30: `Measure-CodeQuality.ps1 -SkipTestRun` reports
  `LibcurlBytes.Unescape(string)` (`Surl.Conformance.UnitLibrary/LibcurlBytes.cs:28`, cyclomatic
  complexity 18) and `LibcurlRtspScript.ParseStep(string)` (`LibcurlRtspScript.cs:100`, complexity
  12) as failing the root `CLAUDE.md` gate of at most 10 per method. Both are at 100% line and
  branch coverage; only complexity fails. They came in with BL-332 (commit `1c96a50`).
- The build does not catch them, so check whether `CA1502` is applied to this project; if it is
  not, say why under Notes rather than changing `CodeMetricsConfig.txt` (Stewart's).
- A refactor only: what the conformance tests drive and assert stays the same.

## Acceptance criteria

- [x] `LibcurlBytes.Unescape` and `LibcurlRtspScript.ParseStep` each have a cyclomatic complexity of
      at most 10, split into named private methods.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary`
      reports 100% line and branch coverage and no failing member.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `CA1502` *is* applied to this project: `Directory.Build.props` links `CodeMetricsConfig.txt`
  into every project whose name does not end in `UnitTests`, and `.editorconfig` turns the rule
  on. The build stayed silent because the two gates count differently: `CA1502` counts source
  branches (Roslyn operations), while `Measure-CodeQuality.ps1` reads the Cobertura
  `complexity` attribute, which counts IL branch points - a `switch` expression over four
  characters and the `||` compile to more IL branches than the source shows. No threshold
  changed.
- `Unescape` now loops over a private `ByteAt(text, ref index)`, which reads one plain byte or
  escape; `Escaped(char)` holds the single-character escape table. `ParseStep` keeps the request
  and `no-body` cases and hands `<option>:<value>` to a private `ParseValueStep`. Behaviour is
  unchanged; the existing `Surl.Conformance.UnitTests` cover every branch (143 passed).
- Measured after: Surl.Conformance.UnitLibrary 100% line, 100% branch, 71 members, 0 failing,
  worst CRAP 10.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. LibcurlBytes.Unescape and LibcurlRtspScript.ParseStep are split under complexity 10; Surl.Conformance measures 100/100 with no failing member
