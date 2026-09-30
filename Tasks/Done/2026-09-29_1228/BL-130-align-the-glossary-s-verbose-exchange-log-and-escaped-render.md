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
completed: 2026-09-29
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

- [x] The "verbose exchange log" row names `VerboseExchangeLog` in `Surl.Output.UnitLibrary`
      and no longer says "not yet".
- [x] The "escaped rendering" row names `ExchangeLogEscaping.AppendEscaped` and no longer
      says "not yet".
- [x] No other glossary row says "not yet" of a type that exists in a `*.UnitLibrary`.

## Notes

- Criterion 3 found eight more stale rows, all fixed: listener status line (`ListenerStatusLine`), negatable option (`CommandLineOption.Negatable`), hardening limit (`ConnectionLimits` for the connection-level rest), idle timeout (`ConnectionLimits.IdleTimeout`), head timeout and connection refusal (dropped "(not yet)" from names that exist), exposure default (`ContentExposureOptions`), answered as absent (`ContentPathMapping.IsAnsweredAsAbsent`, set by `ContentStore`).
- Also restored the missing column separator before `ContentStore.ServedRoot` in the "served root" row.
- Line 5's explanation of "Not yet" is kept: it defines the marker for future rows. No `.cs` changed; build clean, fast tests green.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Glossary rows name VerboseExchangeLog, ExchangeLogEscaping.AppendEscaped and eight other existing types instead of 'not yet'
