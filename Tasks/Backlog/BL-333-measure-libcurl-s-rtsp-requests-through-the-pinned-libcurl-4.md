---
id: BL-333
title: Measure libcurl's RTSP requests through the pinned libcurl-4.dll and amend ADR-0074
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-332]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-333 — Measure libcurl's RTSP requests through the pinned libcurl-4.dll and amend ADR-0074

## Goal

ADR-0074 records, from measurement of the pinned `libcurl-4.dll` through BL-332's driver, the exact
requests libcurl 8.21.0 sends for `DESCRIBE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`,
`GET_PARAMETER`, `SET_PARAMETER`, `ANNOUNCE` and `RECORD`, and what it does with ADR-0074's answers
(the SDP, the `Transport`, the interleaved RTP and RTCP frames, `454`, `455`, `461`), so BL-318
proves the libcurl cases of decision 11 with expected results from upstream.

## Context

- ADR-0074 decisions 4 to 6, 11 and 12. Measure with `Record-CurlExchange.ps1 -Raw` and `{CSEQ}`,
  the replies written as ADR-0074 decides them (decision 5's `$` frames in `\xHH`).
- Record especially: the Request-URI libcurl sends with and without `CURLOPT_RTSP_STREAM_URI`;
  whether it sends `Accept: application/sdp` on `DESCRIBE`; the `Transport` it sends; how the
  interleave callback receives frames that arrive with the `PLAY` response; the `ANNOUNCE` and
  parameter bodies and their `Content-Type`; what `RECORD` sends.
- These fixtures also replace RFC 2326 section 14's examples in BL-313 to BL-316's tests where those
  tasks are not yet done; where they are done, file a task to re-pin the fixtures.
- If a measurement contradicts a decision, amend the decision in ADR-0074 (an Amendment section),
  marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] ADR-0074 has an amendment recording each measurement (driver script, library SHA-256, date,
      bytes) and the expected libcurl result for each libcurl case in decision 11.
- [ ] `Documentation/Planning/Decisions/README.md`'s ADR-0074 row mentions the amendment.

## Notes

## Log

- 2026-09-30: Created.
