---
id: BL-1311
title: Run the Debug-feature http/2 and http/3 upstream test cases with the Debug build (ADR-0173)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1310]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-1311 — Run the Debug-feature http/2 and http/3 upstream test cases with the Debug build (ADR-0173)


## Goal

On `linux-x64`, the upstream test cases 2400, 2401, 2406, 2501, 358 and 359 run with the Debug build and each either passes or is a recorded finding.

## Context

ADR-0173 decisions 6 and 7. `debug-build` is the skip reason today (ADR-0080). The runner picks the pinned Debug build only for a case whose `<features>` contain `Debug` and only when the platform pins one.

## Acceptance criteria

- [ ] The suite chooses the supplementary Debug build for a case listing `Debug` on a platform that pins one, and still skips `debug-build` elsewhere; a fast test pins both branches.
- [ ] 2400, 2401, 2406, 2501, 358 and 359 run on Linux; each passes, or has a task filed for its first differing section.
- [ ] Passing cases are added to `upstream-test-cases-passing.linux-x64.txt`.
- [ ] No case that passed before fails, and no case outside the six is moved off its reference build.

## Notes

## Log

- 2026-11-16: Created by BL-1303 (ADR-0173).
