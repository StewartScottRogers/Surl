---
id: BL-1528
title: Make WebTransportConnectHandlerTests wait for the session-accepted note instead of reading the log at once
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1528 — Make WebTransportConnectHandlerTests wait for the session-accepted note instead of reading the log at once

## Goal

`WebTransportConnectHandlerTests.ServeSessionAsync_ConnectToTheAcceptedPath_IsAnswered200AndTheStreamStaysOpen`, and
any sibling test that asserts a log note right after a response, passes every run on every platform.

## Context

- CI run 44869580341 (PR #122, 2026-11-21, ubuntu-latest Build and test), a fast test: at
  `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests/WebTransportConnectHandlerTests.cs:30`,
  `Assert.Contains("WebTransport session 0 on /echo was accepted.", log.Notes)` failed. The 200 response had
  arrived, but the note had not been written yet. The handler answers before (or concurrently with) writing the
  note, and the test reads `log.Notes` once.
- Same family as BL-1375 (a log note read before it was written). This flake held the merge of PR #122.

## Acceptance criteria

- [x] The test waits, bounded (e.g. 5 s, through the test's own polling with `Task.Delay`, no `Thread.Sleep`), for
      the note to appear, and fails with the notes seen if it never does. Or, if the handler should write the note
      before answering 200, it is changed to do so, with the order pinned by a test.
- [x] Every other test in `Surl.Extended.Protocol.WebTransport.UnitTests` that asserts a note right after a client
      sees a response uses the same wait, through one shared helper; the Log lists them.
- [x] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Chose the test-side wait (not reordering the handler): `UnitTestWebTransport.WaitForNoteAsync(log, note, token)` polls with `Task.Delay(1)` up to 5 s and fails with the notes seen.
- Tests moved onto it (all asserted a note right after a response/event): ConnectHandlerTests (accepted, 405), ConnectAuthenticationTests (401), ConnectFaultTests (503, not-applied, drop), SessionDirectoryTests (stream 6), SessionTests (closed code 7, CONNECT stream ended, max-filesize). Sites already waiting on the note or asserting an earlier one than the awaited one were left.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Tests moved onto UnitTestWebTransport.WaitForNoteAsync: ConnectHandlerTests (accepted, 405), ConnectAuthenticationTests (401), ConnectFaultTests (503, not-applied, drop), SessionDirectoryTests (stream 6), SessionTests (closed code 7, CONNECT ended, max-filesize).
- 2026-11-21: Doing -> Done. WaitForNoteAsync helper; 10 note assertions now wait
