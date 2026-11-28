---
id: BL-1989
title: Correct the POP3 library's CAPA reply, session end and project notes statements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1967]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1989 — Correct the POP3 library's CAPA reply, session end and project notes statements

## Goal

The POP3 library's comments and `CLAUDE.md` say what the code does about multi-line replies, how a session ends and
what the library holds.

## Context

Found while working BL-1967 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change.
In `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary`:

- The comment above `WriteMultiLineAsync` in `Pop3Session.cs` calls it a reply built only from numbers and fixed words.
  Under an upstream test case, `CAPA` lists the case's capabilities.
- The `Pop3Session` summary and the `Pop3ProtocolServer.ServeAsync` summary list how a session ends and omit a `Drop`
  fault and the client leaving during a `Delay` hold (ADR-0141).
- `CLAUDE.md` "What it holds" does not list `Pop3UpstreamTestCaseServer` or `Pop3Session.UpstreamTestCase.cs`, and
  gives the `+OK surl ready` greeting without saying that `REPLY welcome` replaces it.

## Acceptance criteria

- [x] The `WriteMultiLineAsync` comment names the upstream test case's capabilities as an exception.
- [x] Both session-end summaries include a `Drop` fault and the client leaving during a `Delay` hold.
- [x] `CLAUDE.md` "What it holds" lists the two upstream test case types or files and says `REPLY welcome` replaces the greeting.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Made the three-file text change in the lane session itself instead of delegating to `align-and-document`:
  the task names each statement to correct, so a second agent would only have re-read the same files.
- The `CLAUDE.md` text says `REPLY welcome` replaces the greeting on the case's first connection only, which is
  what `MailUpstreamTestCaseReplies.TakeWelcome` documents.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast
  tests 30380 passed, 0 failed, 3 skipped. No integration run: only comments and Markdown changed. No behaviour
  or design decision, so no ADR.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The POP3 library's comments and CLAUDE.md now state the multi-line reply exception, every way a session ends and the upstream test case types
