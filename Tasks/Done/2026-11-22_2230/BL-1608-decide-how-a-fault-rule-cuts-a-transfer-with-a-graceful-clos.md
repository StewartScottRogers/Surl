---
id: BL-1608
title: Decide how a fault rule cuts a transfer with a graceful close after N bytes so curl exits 18, beside drop's reset that exits 56
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-062
created: 2026-11-22
completed: 2026-11-22
---
# BL-1608 — Decide how a fault rule cuts a transfer with a graceful close after N bytes so curl exits 18, beside drop's reset that exits 56

## Goal

An ADR, either an amendment to ADR-0124 decision 4 or a new ADR that it links, decides how a test configures a fault that sends exactly N bytes and then closes the connection gracefully (FIN, no reset). With it, a stand-in can produce both a cut-off download (upstream curl exit 18, `CURLE_PARTIAL_FILE`) and a vanished connection (exit 56, `CURLE_RECV_ERROR`). The ADR also files the implementation tasks.

## Context

This comes from item 1 of the Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T2157-Surl-bug-report.md`. The item is a question. It was tested against Surl v0.1.5 with the Curl port as client, so it is a lead and never evidence (ADR-0003). The repro is `surl --directory site --fault "drop;after-bytes=4000000" http://127.0.0.1:8080/` with a 5,000,001-byte `site/big.txt`. The client exits 56 and keeps somewhere between 1 and 4 MB, because the reset discards bytes the client had not read yet. The recipe wanted exit 18 and exactly 4,000,000 bytes kept.

This is a new decision, not a bug. What is already decided:
- In `Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md`, decision 4's kind table (line 238) says the serving engine applies `drop` as "abort the connection". The BL-1518 amendment (from line 551) says it again: at `after-bytes` the write is cut at N and every joined connection is aborted (`IConnection.Abort`, a reset), "because a drop is a connection that vanishes and `error` is the kind that answers". So `drop` stays a reset. This task must not change what an existing `drop` rule does ("configuration only grows", root `CLAUDE.md`).
- No existing kind or key gives a graceful cut:
  - The kinds are a closed set. `FaultKind` in `Foundation/Surl.Protocol.Abstractions.UnitLibrary/FaultKind.cs` has `Delay`, `Drop`, `RefuseLogin`, `Error` and `OutOfSpace`, and decision 4 says "A new kind needs an ADR".
  - None of `--fault`'s keys (ADR-0124, lines 335-345) sets how the connection is closed.
  - ADR-0141's command-stage drop also aborts.
- Where the code resets:
  - `Foundation/Surl.Core.UnitLibrary/ExchangeDropBudget.cs:94` calls `connection.Abort()` in `Drop()`.
  - `Foundation/Surl.Core.UnitLibrary/DroppingAfterBytesConnection.cs:108` forwards `Abort()`.
  - `Foundation/Surl.Networking.UnitLibrary/StreamConnection.cs:295-302`: `Abort()` calls `transportControl.ResetAndClose()`.
- A graceful close already exists: ADR-0021's lingering close, on `StreamConnection`'s `DisposeAsync` path (`LingeringCloseTime`).
- Upstream curl's documented meaning, from https://curl.se/libcurl/c/libcurl-errors.html (checked 2026-11-22 against the error set of the reference release, curl 8.21.0):
  - `CURLE_PARTIAL_FILE (18)`: "A file transfer was shorter or larger than expected. This happens when the server first reports an expected transfer size, and then delivers data that does not match the previously given size."
  - `CURLE_RECV_ERROR (56)`: "Failure with receiving network data."
  - So exit 18 needs a declared size (HTTP `Content-Length`, or FTP's `150 ... (n bytes)` or `SIZE`) followed by an orderly close before that size is reached.
  - The ADR states this as documented meaning only. The exit code for each protocol is measured against pinned upstream curl 8.21.0 in the implementation's conformance task, never assumed.

Options the ADR weighs, deciding by the standing rules (root `CLAUDE.md`, "Decisions"):
1. A new key on `drop`, such as `close=reset|graceful`, defaulting to `reset` (today's behaviour). The ADR decides whether it is allowed only with `after-bytes` or at every stage.
2. A new kind, such as `cut`, that requires `after-bytes`.
3. Anything simpler that the analysis finds.

For each option, the ADR covers:
- HTTP/2 and HTTP/3, where the unit is a stream rather than a connection. ADR-0141 amendment 1 resets the stream; the graceful version is an early `END_STREAM` before `content-length` is reached.
- FTP: whether only the data connection closes or control too, given BL-1518's count shared across connections.
- Datagram flows (`SilencedAfterBytesDatagramFlow`), which have no FIN.
- TLS: whether `close_notify` is sent, since curl treats a missing `close_notify` differently from one that is sent.
- How the N bytes are flushed before the FIN, so the client keeps exactly N.

The ADR also states the stand-in plan:
- No store is involved.
- The fault publishes the same single `FaultAppliedEvent` and exchange-log note as a drop. The ADR says whether the note's text tells a graceful close from a reset.
- It is configured the same way through `--fault`, the `-K` file, `Surl.Api` (a `SurlFault` factory or property), `Surl.Mcp`'s fault tool and the control endpoint's `/v1/faults`.
- A test resets it by removing the rule.
- The cross-protocol scenario it opens: an FTP `RETR` is cut, then `curl -C -` resumes the file over FTP or HTTP from the same content store.

## Acceptance criteria

- [x] An ADR under `Documentation/Planning/Decisions/`, either an amendment section in `ADR-0124-surl-is-a-stand-in-for-tests.md` or a new ADR linked from ADR-0124's header, is marked "Decided by Claude under Stewart's delegation". It states the chosen spelling (kind or key, its values and default) and that every existing `drop` rule still resets.
- [x] For each connection type (a single TCP connection, FTP control plus data, HTTP/2 and HTTP/3 streams, TLS, datagram flows), the ADR states exactly what is sent and what is closed when the count reaches N, and that the client receives exactly N bytes.
- [x] The ADR quotes `CURLE_PARTIAL_FILE` and `CURLE_RECV_ERROR` from libcurl-errors with the curl version checked. It names the cases the conformance task measures against pinned upstream curl 8.21.0, at least HTTP/1.1 with `Content-Length` and an FTP `RETR`. It cites no Curl port output.
- [x] The ADR states the stand-in plan: the event and log note; the `--fault`, `-K`, `Surl.Api`, `Surl.Mcp` and control-endpoint spellings; the `--aihelp` and `--help testing` wording that changes; and the validation error texts.
- [x] Implementation tasks are filed with `task-board.ps1 new`, each depending on this one and sized by the task-planner rules (at most two projects each). They cover the contract and `--fault` reader, the engine, each host, the help text, and one `Integration` conformance task that measures curl's exit code and the number of bytes kept. Their IDs are listed in this task's Log.
- [x] If a new ADR is created, `Documentation/Planning/Decisions/README.md` lists it.

## Notes

The report's client was the Curl port, so the report is only a lead. Any exit code a test pins comes from pinned upstream curl 8.21.0.

## Log

- 2026-11-22: Decided in ADR-0188 (new, linked from ADR-0124's header and the README): a close=reset|graceful key on drop, only with fter-bytes; default eset.
- 2026-11-22: Filed BL-1611 (contract), BL-1612 (--fault reader), BL-1613 (engine), BL-1614 (Composition), BL-1615 (Surl.Api), BL-1616 (Surl.Mcp), BL-1617 (help text), BL-1618 (Integration conformance).

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. ADR-0188 decides close=reset|graceful on drop; BL-1611..BL-1618 filed
