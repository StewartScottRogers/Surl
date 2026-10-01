---
id: BL-322
title: Measure libcurl's WebSocket client frames through the pinned libcurl-4.dll and amend ADR-0071
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-323]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-048
created: 2026-09-30
completed:
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

- [ ] ADR-0071 has an amendment recording each measurement (driver command, library SHA-256, date,
      bytes) and the expected `curl_ws_recv` result for each libcurl case in decision 11.
- [ ] `Documentation/Planning/Decisions/README.md`'s ADR-0071 row mentions the amendment.

## Notes

## Log

- 2026-09-30: Created.
