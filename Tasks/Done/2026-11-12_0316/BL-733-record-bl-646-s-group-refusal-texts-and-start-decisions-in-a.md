---
id: BL-733
title: Record BL-646's --group refusal texts and start decisions in ADR-0101 and ADR-0102
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-646, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-733 — Record BL-646's --group refusal texts and start decisions in ADR-0101 and ADR-0102

## Goal

ADR-0101 decision 5 row 18 and ADR-0102 decisions 4 and 5 state exactly what BL-646 built for `--group`.

## Context

- BL-646 could not edit `Documentation/Planning/Decisions` while BL-503 (in Doing) held that folder, so its
  small decisions are in BL-646's Notes. ADR-0102's Consequences say ADR-0101 row 18 "gains the ... `@everyone`
  texts of decision 4.2 ... each added by the task that builds its option".
- Code: `Application/Surl.Composition.UnitLibrary/AccountGroupExpansion.cs`,
  `Application/Surl.Cli.UnitLibrary/ConfiguredGroupReader.cs`.

## Acceptance criteria

- [x] ADR-0101 decision 5 row 18 lists `(2) option --group: group everyone is reserved` and
      `(2) option --<option>: member @everyone is not allowed here` beside its two texts.
- [x] ADR-0102 decision 4.1 (or an "Amended by BL-646" note) states: the `everyone` checks are made at start
      with row 18's `(2)` form, not while parsing; refusals are checked in the order everyone, unknown member,
      cycle, each at its first instance in command-line order; a cycle names the first group, in command-line
      order, whose expansion meets itself; names compare ordinally.
- [x] ADR-0102 decision 5's note for `--group` reads `* Group <name>: <n> accounts`, with the `* ` verbose
      prefix the other start notes use, as `AccountGroupExpansion.WriteStartLines` writes it.

## Notes

- Made the edits directly rather than through `align-and-document`: three small ADR edits, each checked
  against `AccountGroupExpansion.cs` (texts at lines 65-78, ordinal comparers, `* Group` note at line 123).
- ADR-0102 4.1 gained an "Amended by BL-646" bullet rather than a rewrite, so the original decision stays readable.
- No `.cs` or project file changed.
- 2026-11-11: the three ADR edits were made and checked, but `dotnet build` cannot run: `global.json` pins SDK
  10.0.401, which is not installed (BL-1035). Returned to Backlog behind BL-1035, as BL-824 and BL-970 were; the
  edits are left uncommitted for the shift to stash. On resume: re-apply them if lost, build, run the fast tests, finish.

- 2026-11-12: the stashed edits were gone, so re-applied all three against `AccountGroupExpansion.cs`. Only Markdown changed, so no build was needed.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; ADR edits made, uncommitted
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ADR-0101 row 18 and ADR-0102 decisions 4.1 and 5 now state BL-646's --group refusal texts, check order and start note
