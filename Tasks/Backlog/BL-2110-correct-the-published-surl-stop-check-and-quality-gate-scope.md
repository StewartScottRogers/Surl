---
id: BL-2110
title: Correct the published-surl stop check and quality gate scope statements in Requirements.md, the glossary, README.md and coverage-auditor
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md, .claude/agents/coverage-auditor.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2110 — Correct the published-surl stop check and quality gate scope statements in Requirements.md, the glossary, README.md and coverage-auditor

## Goal

The statements listed in Context are true of the scripts as they are now. No behaviour changes.

## Context

Found while working BL-2109 (2026-11-28):

- NFR-006 in `Documentation/Product/Requirements.md` and the "published-binary smoke test" row of `Documentation/Wiki/Glossary.md` say every leg proves a clean stop on SIGTERM. `Test-PublishedSurl.ps1` (check 4) requires exit 0 on SIGTERM on Linux and macOS only; on Windows it uses `Stop-Process` and requires the process to be gone.
- The "native publish" row of the glossary says the publish produces one native file. The native symbol file may sit beside it, and `.github/workflows/release.yml` passes the switches that keep library symbol and XML documentation files out.
- `README.md` ("Code coverage" caption and the "Gate" step) and the description in `.claude/agents/coverage-auditor.md` name only libraries as gated; `Measure-CodeQuality.ps1` also gates `Surl.Console` and `Surl.Api`.

Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-006 and the glossary's smoke test row say which stop `Test-PublishedSurl.ps1` checks on Windows and which on Linux and macOS.
- [ ] The glossary's native publish row allows the symbol file, as `Test-PublishedSurl.ps1` check 1 does.
- [ ] `README.md` and the `coverage-auditor` description name `Surl.Console` and `Surl.Api` as gated.

## Notes

## Log

- 2026-11-28: Created.
