---
id: BL-331
title: Align ADR-0071's --ws-echo help line with the option table
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-303]
touches: [Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-331 — Align ADR-0071's --ws-echo help line with the option table

## Goal

ADR-0071 decision 9 names the `--ws-echo` help line the code actually shows,
`Echo client messages, not the path`.

## Context

- ADR-0071 decision 9 gives `--ws-echo` the description `Echo every client message instead of
  serving the path` (52 characters). BL-303 registered the option with
  `Echo client messages, not the path` instead: `HelpLayout` (ADR-0034 decision 3, curl's
  measured column rule) narrows the description column of `surl --help all` for every option once
  one description reaches 39 characters, so the ADR's wording would have moved the column of the
  whole list from 38.
- BL-303 could not edit the ADR: BL-286 held `Documentation/Planning/Decisions` at the time.
- Code: `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (the `ws-echo` row).

## Acceptance criteria

- [x] ADR-0071 decision 9 names `--ws-echo`'s description as `Echo client messages, not the path`,
      with one sentence saying why it is shorter (the `--help all` column).
- [x] No other document names the 52-character wording (`Grep` for `Echo every client message`
      finds nothing outside `Tasks`).

## Notes

- Docs-only: replaced the wording in decision 9's Help paragraph and added the column-rule sentence. Grep for `Echo every client message` outside `Tasks` now finds nothing. No `.cs` touched.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0071 decision 9 names --ws-echo's help line as the code shows it, with the --help all column reason
