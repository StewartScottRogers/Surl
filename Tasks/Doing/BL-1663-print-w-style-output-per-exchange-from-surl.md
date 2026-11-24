---
id: BL-1663
title: Print -w style output per exchange from surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli, Application/Surl.Console]
requirement: none
created: 2026-11-24
completed:
---
# BL-1663 — Print -w style output per exchange from surl

## Goal

`surl` can print a per-exchange line in the spirit of curl's `-w` / `--write-out`, so a script sees each exchange's outcome on standard output.

## Context

The product overview's "Seeing the exchange" scope lists "`-w` style output per exchange" as in scope and not built (audit BL-1651: no `--write-out` or `-w` option exists in `Application/`). Decide the variable set and format by an ADR; measure upstream curl's `-w` variables first. Adding an option needs its `--aihelp` facts.

## Acceptance criteria

- [ ] `surl` accepts a write-out option, prints the chosen format once per exchange, and has `--aihelp` facts and tests.
- [ ] The product overview's "not built yet" sentence is replaced by a statement naming this task.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
