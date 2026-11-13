---
id: BL-857
title: Order a multi-server upstream test case's protocol log by arrival
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-857 — Order a multi-server upstream test case's protocol log by arrival

## Goal

A case that uses several servers has its `<protocol>` checked against one log whose lines are in
the order the server halves received them, as runtests' single `server.input` is, so upstream
tests 2002 and 2003 can pass.

## Context

- Found in BL-396's run (Windows, reference build, 2026-11-05): tests 2002 and 2003 fetch over
  HTTP, FTP, `file:` and TFTP in one command. Their `<protocol>` puts TFTP's `opcode = 1 ...` lines
  before FTP's `QUIT`, because curl keeps the FTP connection open until it exits. The run
  (`UpstreamTestCasesAgainstSurlTests`) joins each server half's whole log in the order the case
  names its servers, so FTP's `QUIT` comes before TFTP's lines and the case fails `protocol` at line 13.
- Each server half (`HttpUpstreamTestCaseReplies`, `FtpUpstreamTestCaseReplies`,
  `TftpUpstreamTestCaseReplies`, ...) keeps its own bytes. One way: the test project wraps each
  composed server so every chunk is appended to one shared, locked log as it arrives; the
  protocol libraries need not change. Decide inside `Surl.Conformance.UnitTests`.

## Acceptance criteria

- [x] A case's `<protocol>` is checked against one log in arrival order across server halves.
- [x] The Windows upstream test-case run passes 2002 and 2003 or names a different first
      differing section, and no case on the passing list stops passing.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-11 (lane 4): Design chosen and written, left uncommitted for the shift to stash:
  `ArrivalOrderedReceivedLog` (test project) reads each server half's `ReceivedBytes` and
  appends what each added since its last look. `UpstreamTestCaseServerHalves.Play` takes it and
  wraps every connection and datagram flow so it looks before each read, peek, write, receive,
  send and dispose. A half's own log already holds what it received before its next read or
  reply, so TFTP's lines land before FTP's late `QUIT`. Growth in several halves between two
  looks keeps the case's server order, which is the old behaviour. `ArrivalOrderedReceivedLogTests`
  covers it with hand-written fakes. No protocol library changes. Not yet built: the pinned
  SDK 10.0.401 is missing (BL-1035).

- 2026-11-12 (lane 1): The stashed lane-4 work was not in the checkout, so the same design was
  rewritten: `ArrivalOrderedReceivedLog` (`Look()` copies what each half added since the last look;
  `Wrap` makes every connection/flow look before and after reads, peeks, writes, sends, aborts and
  disposal) and `UpstreamTestCaseServerHalves.Play` wraps each case server in it. The run's
  `received` now comes from `arrivalLog.Bytes`. Build clean; fast tests 913 green; the Windows
  upstream run (`TestCategory=UpstreamTestCases`) passed with no regression and printed 2002 and
  2003 among its newly passing lines. Not done here (outside `touches`): adding 2002 and 2003 to
  `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` and the pass-rate table,
  which must change together (`UpstreamTestCasePassRateStatementTests`).

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, dotnet build cannot start
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. A multi-server case's protocol is checked against one log in arrival order; upstream 2002 and 2003 no longer fail on FTP QUIT order
