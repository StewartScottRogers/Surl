---
id: BL-921
title: Bring ImapSession.AnswerNextCommandAsync under cyclomatic complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
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

- [x] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports 0 failing members, 100% line and
      branch coverage.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Filed by BL-901 (2026-11-06, dark factory lane 9).

2026-11-11 (lane 6): split `AnswerNextCommandAsync` into `AnsweredByTheUpstreamTestCaseAsync` (returns `bool?`;
null falls through) and `AnswerReadOutcomeAsync` (the outcome chain), no behaviour change. Left uncommitted for the
shift to stash: `dotnet build` cannot run because the pinned SDK 10.0.401 is not installed (BL-1035), so the
change is unverified. Returned to Backlog behind BL-1035.

2026-11-12 (lane 1): SDK now present. Split out `AnswerReadOutcomeAsync` (the outcome chain); the
upstream-test-case check stays in `AnswerNextCommandAsync`. Build clean, 753 Imap tests green, measure reports 0 failing.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ImapSession.AnswerNextCommandAsync split under complexity 10; Imap measure reports 0 failing
