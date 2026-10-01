---
id: BL-332
title: Drive libcurl's RTSP requests and interleaved receive through the pinned libcurl-4.dll
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-323, BL-286]
touches: [Record-CurlExchange.ps1, Run-LibcurlRtspScript.cs, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-051
created: 2026-09-30
completed: 2026-09-30
---
# BL-332 — Drive libcurl's RTSP requests and interleaved receive through the pinned libcurl-4.dll

## Goal

BL-323's driver for the pinned `libcurl-4.dll` also runs a scripted sequence of RTSP requests
(`CURLOPT_RTSP_REQUEST`) and the interleaved receive, printing each result, so the RTSP requests only
libcurl's API sends can be measured (BL-333) and proved (BL-318) - ADR-0074 decision 12.

## Context

- ADR-0074 decisions 11 and 12; ADR-0071 decision 10 (the pin); BL-323 (the driver, its location, and
  `Record-CurlExchange.ps1`'s mode that runs it in place of `curl.exe`).
- The options to drive (libcurl 8.21.0, `include/curl/curl.h` at tag `curl-8_21_0`):
  `CURLOPT_RTSP_REQUEST` (`RTSPREQ_OPTIONS` ... `RTSPREQ_RECEIVE`), `CURLOPT_RTSP_STREAM_URI`,
  `CURLOPT_RTSP_TRANSPORT`, `CURLOPT_RTSP_SESSION_ID`, `CURLOPT_RTSP_CLIENT_CSEQ`, the upload body for
  `ANNOUNCE` and the parameter bodies (`CURLOPT_UPLOAD`/`CURLOPT_READFUNCTION` or
  `CURLOPT_POSTFIELDS`, whichever `lib/rtsp.c` reads), `CURLOPT_INTERLEAVEFUNCTION` and
  `CURLOPT_INTERLEAVEDATA`, and `curl_easy_getinfo` for `CURLINFO_RESPONSE_CODE`,
  `CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CSEQ_RECV`.
- One easy handle reused across the script, so the requests share a connection as a libcurl RTSP
  client's do. Each step prints its `CURLcode`, the status, the session ID, and the bytes the
  interleave callback received (as `\xHH` or a SHA-256 when long).
- `Record-CurlExchange.ps1 -Raw` with `{CSEQ}` (BL-286) is the server side for measuring.
- Linux and macOS static builds carry no shared library: the driver reports Inconclusive there.

## Acceptance criteria

- [x] The driver runs a script of `OPTIONS`, `DESCRIBE`, `SETUP`, `PLAY`, `RECEIVE`, `PAUSE`,
      `GET_PARAMETER`, `TEARDOWN`, `ANNOUNCE` and `RECORD` steps against a `-Raw` recorder answering
      with `{CSEQ}`, and `request.bin` holds each request; the script's help describes how.
- [x] A test in `Surl.Conformance.UnitTests` covers the driver's script parsing and output; the
      library is never loaded unless its SHA-256 matches the pin.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `touches` gained `Run-LibcurlRtspScript.cs`: the RTSP driver is its own file-based app beside
  `Run-LibcurlWebSocketScript.cs` rather than a mode of it - one driver per API keeps each name
  saying what it drives, and `Run-LibcurlWebSocketScript.cs` is not in this task's `touches`. No
  task in Doing names it.
- Testability: the parsing (`LibcurlRtspScript`), the output lines (`LibcurlRtspReport`,
  `LibcurlRtspOutcome`), the byte escapes (`LibcurlBytes`) and the pin decision
  (`PinnedLibcurlChoice`: requested file or the platform's pin, refused with exit 3 unless its
  SHA-256 matches a library pin, exit 4 inconclusive when none is pinned or installed) live in
  `Surl.Conformance.UnitLibrary` with tests; the driver only calls libcurl and loads nothing but
  `PinnedLibcurlChoice.LibraryPath`.
- Step grammar (my choice, the simplest covering the options): a request name runs
  `CURLOPT_RTSP_REQUEST` + `curl_easy_perform`; `stream-uri:`, `transport:`, `session-id:`,
  `client-cseq:` set their option; `body:` uses `CURLOPT_COPYPOSTFIELDS`, `upload:` uses
  `CURLOPT_UPLOAD` + a read callback (`lib/rtsp.c` reads both), `no-body` clears both. Options
  stay set across steps, as libcurl's do. A failed request does not stop the script. A write
  callback captures response bodies so nothing leaks onto stdout.
- Recorder: `-LibcurlRtsp` runs the driver like `-LibcurlWebSocket`, sharing `-Libcurl`;
  combining the two, or with `-Curl`, is refused.
- Measured 2026-09-30 against `-Raw` with `{CSEQ}` replies (for BL-333): all ten requests went out
  on one connection, CSeq 1-9 in order. Without `stream-uri:` every request line is
  `<METHOD> * RTSP/1.0` (libcurl's default stream URI is `*`). DESCRIBE adds
  `Accept: application/sdp`; SETUP sends `Transport:` and libcurl keeps the `Session` the reply
  names, sending it on PLAY, PAUSE, GET_PARAMETER, TEARDOWN and, still, on ANNOUNCE and RECORD after
  TEARDOWN. ANNOUNCE's `body:` went out with `Content-Length: 5` and `Content-Type: application/sdp`;
  SET_PARAMETER's `upload:` with `Content-Type: text/parameters`. SET_PARAMETER with no session ID
  is refused before sending (CURLcode 43). RECEIVE read a `$\x00\x00\x04abcd` packet as one
  interleave call, status 0. A body-less GET_PARAMETER discards the response body (0 bytes
  reported for a `Content-Length: 2` reply); with `body:` the body arrives. `client-cseq:40` made
  the next request carry `CSeq: 40`. `--library curl.exe` exits 3 (not a pinned libcurl).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Run-LibcurlRtspScript.cs drives the pinned libcurl's RTSP requests and interleaved receive on one handle; Record-CurlExchange.ps1 -LibcurlRtsp records them against -Raw
