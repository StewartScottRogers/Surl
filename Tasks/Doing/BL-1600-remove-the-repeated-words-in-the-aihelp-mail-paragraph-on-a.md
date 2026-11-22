---
id: BL-1600
title: Remove the repeated words in the --aihelp mail paragraph on a mailbox file's quota and folders columns
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary]
requirement: none
created: 2026-11-22
completed:
---
# BL-1600 — Remove the repeated words in the --aihelp mail paragraph on a mailbox file's quota and folders columns

## Goal

The `--aihelp` paragraph on `quota=` and `folders=` ends with "A `--mailbox-file` takes `quota` and `folders` columns,
an empty field meaning the key was not given." instead of today's doubled "A `--mailbox-file` takes `--mailbox-file`
takes".

## Context

- Found while planning from the Surly bug report of 2026-11-22T1308 (item 1), reading the mailbox-file help.
- The text is in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:230`, the last sentence of the string that starts
  "A mailbox also takes `quota=<bytes>`". No test pins that sentence today (a repository search for
  "takes `--mailbox-file` takes" finds only that line).
- Wording only; no behaviour changes, so no store, event or fault is involved.

## Acceptance criteria

- [ ] `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` no longer contains the text "takes `--mailbox-file` takes", and
      the sentence reads "A `--mailbox-file` takes `quota` and `folders` columns, an empty field meaning the key was not
      given."
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"`
      is green (the `AiHelpTextTests` and `CommandLineRunnerAiHelpTests` suites included).

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
