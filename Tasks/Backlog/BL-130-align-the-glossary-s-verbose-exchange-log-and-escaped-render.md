---
id: BL-130
title: Align the glossary's verbose exchange log and escaped rendering rows with the code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-130 — Align the glossary's verbose exchange log and escaped rendering rows with the code

## Goal

The glossary's "verbose exchange log" and "escaped rendering" rows name the code that
implements them now, not "not yet".

## Context

Found while writing ADR-0038 (BL-129). `Documentation/Wiki/Glossary.md` says the verbose
exchange log is implemented in `Surl.Output` "(not yet)", but
`Surl.Output.UnitLibrary/VerboseExchangeLog.cs` exists; and it says escaped rendering has
no name in code yet, but the code uses `ExchangeLogEscaping.AppendEscaped`. A document that
says something false of the code is a defect (CLAUDE.md, "Say what it does").

## Acceptance criteria

- [ ] The "verbose exchange log" row names `VerboseExchangeLog` in `Surl.Output.UnitLibrary`
      and no longer says "not yet".
- [ ] The "escaped rendering" row names `ExchangeLogEscaping.AppendEscaped` and no longer
      says "not yet".
- [ ] No other glossary row says "not yet" of a type that exists in a `*.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
