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
completed:
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

- [ ] The test waits, bounded (e.g. 5 s, through the test's own polling with `Task.Delay`, no `Thread.Sleep`), for
      the note to appear, and fails with the notes seen if it never does. Or, if the handler should write the note
      before answering 200, it is changed to do so, with the order pinned by a test.
- [ ] Every other test in `Surl.Extended.Protocol.WebTransport.UnitTests` that asserts a note right after a client
      sees a response uses the same wait, through one shared helper; the Log lists them.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
