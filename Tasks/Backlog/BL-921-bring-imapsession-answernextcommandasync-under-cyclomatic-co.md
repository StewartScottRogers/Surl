---
id: BL-921
title: Bring ImapSession.AnswerNextCommandAsync under cyclomatic complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-921 — Bring ImapSession.AnswerNextCommandAsync under cyclomatic complexity 10

## Goal

`powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports 0 failing
members.

## Context

- On 2026-11-06 (BL-901) the measurement reports `ImapSession.AnswerNextCommandAsync (async)`
  (`Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.cs`) at cyclomatic complexity 16, CRAP 16, against
  the gate of 10 (root `CLAUDE.md`, "Quality gates"). Line and branch coverage are 100%.
- The upstream-test-case branches (BL-878: `caseReplies is not null && ...`) added to the outcome chain push it
  over. Splitting the upstream-test-case dispatch, or the outcome dispatch, into its own method is enough.
- Behaviour must not change; the existing `ImapSession` tests are the proof.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports 0 failing members, 100% line and
      branch coverage.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Filed by BL-901 (2026-11-06, dark factory lane 9).

## Log

- 2026-11-06: Created.
