---
id: BL-1489
title: Update --aihelp and --manual text for the protocols= no-login refusal
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1483, BL-1488]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1489 — Update --aihelp and --manual text for the protocols= no-login refusal

## Goal

`--aihelp` and `--manual` say that `protocols=` may not name a scheme with no login, with the refusal text and exit code 2.

## Context

ADR-0102 section 4.3 amendment of 2026-11-20 (BL-1483). `AiHelpProse.cs:228` currently says only "a scheme no server speaks". Depends on BL-1488, which builds the behaviour the text describes.

## Acceptance criteria

- [x] `AiHelpProse` paragraph for `--mailbox`/`protocols=` names `scheme <scheme> has no login` and the schemes `gopher`, `tftp`, `dict`, `telnet` as examples.
- [x] The `--manual` text agrees.
- [x] Completeness and example tests in `Surl.Cli.UnitTests` pass; build clean.

## Notes

Also updated the pinned full-text in ManualTextTests (Text_IsTheWholeManual).

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. aihelp and manual text name the no-login refusal
