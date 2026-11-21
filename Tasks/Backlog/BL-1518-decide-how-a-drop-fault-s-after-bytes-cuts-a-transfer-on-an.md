---
id: BL-1518
title: Decide how a drop fault's after-bytes cuts a transfer on an exchange's data connection
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1518 — Decide how a drop fault's after-bytes cuts a transfer on an exchange's data connection

## Goal

ADR-0124 gains an amendment, decided by Claude under Stewart's delegation, that says which bytes a `drop;after-bytes=N` rule counts on an exchange that opens data connections (FTP's), so a test can cut a download partway through the file.

## Context

Reported by the Surly cookbook (batch 7, 2026-11-20, item 1, against Surl `9ad60d80c`; still so on `factory/phase-1` at `299737d24`): `--fault "drop;after-bytes=N"` on an `ftp://` listener counts only control-connection bytes, so a test of curl's `-C -` resume cannot cut a `RETR` mid-file. A stand-in exists for exactly that kind of test (root `CLAUDE.md`, "Fault-injectable").

Where it happens today:
- `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:785-789` (`FaultThenSecureThenServeAsync`) wraps only the control connection in `DroppingAfterBytesConnection`.
- `ServingEngine.cs:753` builds the exchange's `ExchangeDataConnections` before that, and `Foundation/Surl.Core.UnitLibrary/ExchangeDataConnections.cs:75-80` (`Opened`) wraps each data connection in `RecordingConnection` and `IdleClockRestartingConnection` only, never in the drop.
- ADR-0124 decision 4 defines `after-bytes` as "0 to 2^31-1 bytes sent" (key table, line ~340) and the `drop` row as "after `after-bytes` sent"; it never says which connection. ADR-0141 says `after-bytes` is meaningless at a command (unchanged here).

Options to weigh (the ADR picks one and says why):
1. `after-bytes` counts every byte the exchange sends, control and data connections together, and the drop aborts the data connection being written and the control connection. Adds no key; changes nothing for a protocol with one connection; for FTP it changes the count only by adding data bytes.
2. A new key (e.g. `connection=data`) that counts data-connection bytes only, leaving today's rules as they are. Configuration only grows (root `CLAUDE.md`, "Configuration as data"), so this is allowed but needs the key in `Surl.Cli` (`ConfiguredFaultReader`), `Surl.Composition` (`FaultItemText`), `Surl.Api` (`SurlFault`), `Surl.Mcp.UnitLibrary` (`AddFaultTool`), `--aihelp` and the manual.

Whichever is chosen, the ADR states: what the dropped data connection does (reset, as `IConnection.Abort`), whether the control connection is aborted too or answered (pinned upstream curl 8.21.0 exits 56 when the control connection closes at `RETR`, per the FTP library's `CLAUDE.md`; a data reset with the control connection kept would get a `426`/`451` reply - say which, and that measuring it is the implementation's conformance stage), the log note and the one `FaultAppliedEvent` it publishes, and that the `FaultRule.AfterBytes` contract in `Surl.Protocol.Abstractions` is unchanged or how it changes.

Stand-in: no store is involved; a test observes the drop as the `FaultAppliedEvent` on the instance's event stream and the partial file curl kept, then resumes with `-C -` against the same listener once `times` is spent.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md` has a dated amendment, marked "Decided by Claude under Stewart's delegation", stating which bytes `after-bytes` counts when an exchange opens data connections, and why the other option was not taken.
- [ ] The amendment states what is aborted when the count is reached (the data connection, the control connection, or both), the exchange-log note's text and the `FaultAppliedEvent` published.
- [ ] The amendment names every project the implementation changes; if option 2 is chosen, the follow-up tasks for `Surl.Cli`, `Surl.Composition`, `Surl.Api`, `Surl.Mcp.UnitLibrary` and the help text are filed and BL-1519 depends on the ones it needs.
- [ ] `Documentation/Planning/Decisions/README.md`'s ADR-0124 row mentions the amendment.

## Notes

## Log

- 2026-11-20: Created.
