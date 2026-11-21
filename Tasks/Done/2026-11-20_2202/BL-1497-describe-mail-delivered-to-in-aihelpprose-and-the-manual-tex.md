---
id: BL-1497
title: Describe --mail-delivered-to in AiHelpProse and the manual text
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1487, BL-1498]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1497 — Describe --mail-delivered-to in AiHelpProse and the manual text

## Goal

`surl --aihelp mail` describes `--mail-delivered-to`.

## Context

BL-1498 added the option; ADR-0053 amendment 1 decision 1 defines it.

## Acceptance criteria

- [x] `AiHelpProse` mail paragraphs describe `--mail-delivered-to`; Surl.Cli.UnitTests pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. AiHelpProse mail paragraph now describes --mail-delivered-to
