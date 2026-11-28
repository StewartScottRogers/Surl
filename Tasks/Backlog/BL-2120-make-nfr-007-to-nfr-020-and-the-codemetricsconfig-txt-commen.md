---
id: BL-2120
title: Make NFR-007 to NFR-020 and the CodeMetricsConfig.txt comment claim only checks that exist
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md, CodeMetricsConfig.txt]
requirement: none
created: 2026-11-28
completed:
---
# BL-2120 — Make NFR-007 to NFR-020 and the CodeMetricsConfig.txt comment claim only checks that exist

## Goal

The statuses of NFR-007 to NFR-020 follow the non-functional section's own rule and claim only checks that exist, and the CRAP comment in `CodeMetricsConfig.txt` says what the `coverage-auditor` agent does.

## Context

Found while working BL-2119 (2026-11-28). NFR-007 in `Documentation/Product/Requirements.md` says it is enforced by `Directory.Packages.props` and the `ci.yml` build, but no check that would fail on a second package and its reference was found in `Directory.Build.props` or a test. NFR-008 to NFR-020 say `Draft` with no bracketed reason, which the section preamble asks for. The `CodeMetricsConfig.txt` comment says the `coverage-auditor` agent "enforces" the CRAP limit of thirty; the agent reports gaps and files tasks. Adding a check is not part of this task, and no threshold changes. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-007's status names a check that fails when a second package is added, or is `Draft` with what it waits on in brackets.
- [ ] Each of NFR-008 to NFR-020 has a status that the section preamble allows: `Enforced by` an existing gate, or `Draft` with what it waits on in brackets.
- [ ] The `CodeMetricsConfig.txt` comment says the `coverage-auditor` agent reports the CRAP limit's gaps, and every threshold line in the file is unchanged.

## Notes

## Log

- 2026-11-28: Created.
