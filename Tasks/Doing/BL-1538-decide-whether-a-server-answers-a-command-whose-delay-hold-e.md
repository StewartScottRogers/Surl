---
id: BL-1538
title: Decide whether a server answers a command whose delay hold ended because the client left
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1532]
touches: [Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-21
completed:
---
# BL-1538 — Decide whether a server answers a command whose delay hold ended because the client left

## Goal

ADR-0141 says, in a new Amendment 3, whether a server still writes its answer to a command whose `delay` hold ended because the client closed or reset the connection, and the tasks that carry the answer out are filed.

## Context

The Surly cookbook bug report of 2026-11-21 (batch 8, item 2; Surl `9ad60d80c`, the Curl port as client, so a lead and not evidence) ran `surl --directory site --fault "delay;path=/report.txt;ms=3000" http://127.0.0.1:8080/` and `curl -s -m 1 http://127.0.0.1:8080/report.txt` (exit 28), and found the exchange record (`--trace-ascii`, `get_exchanges`) showing `200 OK` sent about 3 s later. It expected the record to show the client gone and nothing sent.

Half of that is already decided and in hand. `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` Amendment 2 (BL-1520) ends a hold as soon as the peer half-closes or resets; BL-1529, BL-1530, BL-1531 and BL-1521 are Done, and BL-1532 (in Doing) hands the connection to the helper for HTTP/1.x, RTSP and WebSocket, so after it the 200 no longer waits 3 s. The other half is not: Amendment 2 decision 4 says that at a command stage "the helper returns the rule as for a completed delay and the server's next read sees the half-close or reset itself". For HTTP, SMTP, IMAP and POP3 the server's next act after the hold is a write, not a read, so the answer is still written, and `RecordingConnection.WriteAsync` (`Foundation/Surl.Core.UnitLibrary/RecordingConnection.cs:58`) records it as sent once the transport write returns. A write after the peer's FIN usually succeeds, so the record shows the answer sent right after the note `Exchange <id>: the client closed the connection during the hold by fault rule <rule id>.`

The code that decides it: `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CommandStageFaults.cs`, `WaitForDelayOrPeerAsync` (lines 103 to 136) returns after the early end, and `FindAndApplyAsync` (line 32) returns the same `FaultRule?` whether the delay ran out or the client left, so no caller can tell the two apart. `Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs:44` turns a `Delay` into `null`, "the request then goes on as if no rule matched".

Decide by the standing rules (root `CLAUDE.md`, "Decisions"; record "Decided by Claude under Stewart's delegation"). Questions the amendment answers:
1. After a hold ended by `PeerClosed` or `PeerReset`, does the server write its answer, or end the exchange without writing (as after a `drop`, but with no abort for a half-close)? Amendment 2 decision 1 already treats a half-close as the client leaving "even if the client still waits for an answer".
2. If it stops, how the helper tells the caller: a new return shape or member on `CommandStageFaults` (a contract change in `Surl.Protocol.Abstractions.UnitLibrary`, which runs apart from every protocol task), keeping the existing overloads' behaviour for servers that do not opt in, and which servers change (the connection-overload users: SMTP, IMAP, POP3; HTTP/1.x, RTSP and WebSocket through `HttpCommandFaults`; HTTP/2 and HTTP/3 stay out, per Amendment 2 decision 5).
3. What the exchange log, `ExchangeEndedEvent` and the `Surl.Api`/`surl-mcp` exchange record show, so a stand-in test can assert "the client left during the hold and nothing was sent".
4. Whether any byte a client that stays receives changes (it must not; say so, as Amendment 2 decision 7 does).

Stand-in: no store. The point of the decision is what a test observes on the event stream and in the exchange record when its client times out under a `delay` rule; the amendment names it.

## Acceptance criteria

- [ ] `ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` has an `## Amendment 3` marked "Decided by Claude under Stewart's delegation", answering questions 1 to 4 above, with its reasons.
- [ ] The amendment names the tasks that act on it, and each is filed on the board with `task-board.ps1 new` (contract first, then one task per server family, each with exact `touches`), or it states that no code changes and why.
- [ ] The ADR index `Documentation/Planning/Decisions/README.md` row for ADR-0141 mentions Amendment 3.
- [ ] No `.cs` file changes in this task.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
