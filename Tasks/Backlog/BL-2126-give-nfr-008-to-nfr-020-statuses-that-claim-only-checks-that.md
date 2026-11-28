---
id: BL-2126
title: Give NFR-008 to NFR-020 statuses that claim only checks that exist
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2126 — Give NFR-008 to NFR-020 statuses that claim only checks that exist

## Goal

Each of NFR-008 to NFR-020 in `Documentation/Product/Requirements.md` has a status the non-functional section's preamble allows: `Enforced by` an existing gate, or `Draft` with what it waits on in brackets.

## Context

Split from BL-2120 (2026-11-28), whose run timed out. NFR-008 to NFR-020 say `Draft` with no bracketed reason. BL-2120's run wrote all thirteen rows, saved in the stash named `darkfactory BL-2120 20261128-060304`; find its index N with `git stash list` (match the message, not the number). Read them with `git show "stash@{N}:Documentation/Product/Requirements.md"` and copy rows NFR-008 to NFR-020 in with the Edit tool, then update `Last updated:` to the day you finish. Do not `git apply` the whole file: NFR-007's row and the `CodeMetricsConfig.txt` comment belong to the sibling task, which may have landed first.

The stash's statuses: NFR-013, NFR-018 and NFR-020 `Enforced by` the `ci.yml` fast-test job; the rest `Draft` with what each waits on. Before keeping a row, check every test class and type it names exists (`Grep` for `ConnectionLimitsTests`, `ExchangeLimitsTests`, `RequestHeadLimitTests`, `RtspRefusalTests`, `ServerTlsSettingsTests`, `TlsSrpStreamTests`, `VerboseLogEscapingTests`, `ExchangeLogEscapingTests`, `ComposedServing`, `ServingEngine.RefuseAsync` and the others), and that each `Enforced by` row's tests do hold the default, the option and 0. ADR-0006 sections 1 to 5 are the source of the targets. The follow-up tasks for what the `Draft` rows wait on are already filed (the `--max-line`/`--max-message`/`--max-filesize` 0 fixes, the head-timeout decision, the refusal write deadline fixes, the `ComposedServing` test, the banner-version decision); several depend on this one. Lanes may not run git checkout or git stash. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] Each of NFR-008 to NFR-020 has a status that the section preamble allows: `Enforced by` an existing gate, or `Draft` with what it waits on in brackets.

## Notes

## Log

- 2026-11-28: Created.
