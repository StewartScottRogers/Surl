---
id: BL-1524
title: Decide how a file server's refusal names the out-of-space fault rule instead of the upload limit
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0129-what-the-content-store-publishes-and-how-it-runs-out-of-space.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1524 — Decide how a file server's refusal names the out-of-space fault rule instead of the upload limit

## Goal

An amendment to ADR-0129, decided by Claude under Stewart's delegation, says how a file server's exchange-log note tells an upload refused by an `out-of-space` fault rule from one that really grew past `--max-filesize`, so the note names the rule rather than stating a limit that was not reached.

## Context

Surly cookbook bug report 2026-11-20, item 4, second half (Surl `9ad60d80c`; unchanged at `299737d24`). On FTP an upload refused by `out-of-space` is noted as `The upload grew past the limit of <n> bytes; the partial upload was deleted and the data connection was reset.` (`Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary/FtpCommandResponder.cs:765`), which is false - the upload grew past nothing - and names no rule. TFTP's note is the same shape (`Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary/TftpWriteTransfer.cs:68`), and HTTP's `PUT` note likewise, `the body went past the upload limit of <n> bytes` (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.Uploads.cs:104-107`); SMB (`SmbSession.cs:565`), CoAP, S3, Azure, GCS and the extended MCP server's `write_file` map the same result. Root `CLAUDE.md`, "Say what it does": a log line that states something untrue is a defect.

Why: ADR-0129 decision 3 has the store answer an `OutOfSpace` rule with the existing `ContentUploadResult.TooLarge` and says "No distinct out-of-space result is added, so no file server changes". `Foundation/Surl.Content.UnitLibrary/ContentStoreEventsAndFaults.cs:35-46` (`RunsOutOfSpaceAsync`) publishes the `FaultAppliedEvent` but has no exchange log, so nothing in the log names the rule. Keep that decision's wire answers: the status or reply each server sends for `TooLarge` (HTTP `413`, FTP `552`, TFTP `ERROR 3`) does not change - only the note does. (The report's other half, HTTP `507` in place of `413`, is answered by ADR-0129 decision 3 as it stands: the outcome a full store already has.)

Options to weigh: a distinct `ContentUploadResult` member (every file server's `switch` over the result then needs a case - list them with `grep -rn "ContentUploadResult.TooLarge" --include=*.cs Protocols`); the rule's ID carried beside `TooLarge` (e.g. on the result or the `ContentCaller`); or the store writing the note itself through the exchange's log. Prefer the one that changes the fewest servers while keeping every note true.

Stand-in: tests already observe the fault through the `FaultAppliedEvent` on the event stream; the note is for a person reading the log.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0129-what-the-content-store-publishes-and-how-it-runs-out-of-space.md` has a dated amendment, marked "Decided by Claude under Stewart's delegation", stating how a server learns that a fault rule (and which) refused the upload, and the note's text for FTP, TFTP and HTTP.
- [ ] The amendment states that the bytes each server sends for the refusal do not change.
- [ ] The implementation tasks are filed, one per library and its `.UnitTests` twin (the store's contract first, then each server family), each naming the note text it pins.
- [ ] `Documentation/Planning/Decisions/README.md`'s ADR-0129 row mentions the amendment.

## Notes

## Log

- 2026-11-20: Created.
