---
id: BL-342
title: Bring LibcurlBytes.Unescape and LibcurlRtspScript.ParseStep in Surl.Conformance under the complexity limit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-342 — Bring LibcurlBytes.Unescape and LibcurlRtspScript.ParseStep in Surl.Conformance under the complexity limit

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

- [ ] `LibcurlBytes.Unescape` and `LibcurlRtspScript.ParseStep` each have a cyclomatic complexity of
      at most 10, split into named private methods.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary`
      reports 100% line and branch coverage and no failing member.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
