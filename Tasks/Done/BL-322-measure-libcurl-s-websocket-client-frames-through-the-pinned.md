---
id: BL-322
title: Measure libcurl's WebSocket client frames through the pinned libcurl-4.dll and amend ADR-0071
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-323]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1, Run-LibcurlWebSocketScript.cs]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-322 — Measure libcurl's WebSocket client frames through the pinned libcurl-4.dll and amend ADR-0071

## Goal

ADR-0071 records, from measurement of the pinned `libcurl-4.dll` through BL-323's driver, the
exact frames libcurl 8.21.0 sends for a text, a binary, a fragmented message, a `PING` and a
`CLOSE` with a code, and what `curl_ws_recv` returns for each server answer ADR-0071 decision 6
gives, so BL-304 proves those cases with expected results from upstream.

## Context

- ADR-0071 decisions 6, 10 and 11 (the libcurl rows of BL-304's list).
- Measure with `Record-CurlExchange.ps1 -Raw` and BL-323's driver: each frame's bytes (mask, opcode,
  FIN, length form), whether libcurl answers a server `CLOSE` through the API, what `curl_ws_recv`
  returns for a `CLOSE` 1009 and 1007, a `PONG`, and a message split in 65536-byte frames.
- If a measurement contradicts decision 6, amend the decision in the same ADR (an Amendment
  section), marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [x] ADR-0071 has an amendment recording each measurement (driver command, library SHA-256, date,
      bytes) and the expected `curl_ws_recv` result for each libcurl case in decision 11.
- [x] `Documentation/Planning/Decisions/README.md`'s ADR-0071 row mentions the amendment.

## Notes

- Measured 17 cases through `Record-CurlExchange.ps1 -Raw -LibcurlWebSocket` with the pinned
  `libcurl-4.dll` (SHA-256 799F7EEF...E574); recorded as ADR-0071 Amendment 1. Nothing contradicts
  decision 6, so it stands; the amendment says so.
- Added `Run-LibcurlWebSocketScript.cs` to `touches`: a 2 MiB message cannot be given on a command
  line, so the driver gained the step `send*<count>:<FLAGS>:<payload>`, and a send libcurl takes in
  part now goes on from where it stopped, as `curl_ws_send`'s documentation requires. No task in
  Doing names the file. `Record-CurlExchange.ps1`'s help documents the new step.
- Default taken: the 2 MiB case needs `--recv-timeout 60000`, because the recorder takes over 5 s
  to read and transcribe 2 MiB before it replies; against surl the default is enough.
- libcurl holds its automatic `PONG` until the application's next `curl_ws_send` and never answers
  a `CLOSE` itself; recorded in the amendment for BL-304.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0071 Amendment 1 records libcurl's measured client frames and the curl_ws_recv result BL-304 expects for each libcurl case
