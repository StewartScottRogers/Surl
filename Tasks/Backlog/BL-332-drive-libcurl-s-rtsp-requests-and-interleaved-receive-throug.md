---
id: BL-332
title: Drive libcurl's RTSP requests and interleaved receive through the pinned libcurl-4.dll
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-323, BL-286]
touches: [Record-CurlExchange.ps1, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-051
created: 2026-09-30
completed:
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

- [ ] The driver runs a script of `OPTIONS`, `DESCRIBE`, `SETUP`, `PLAY`, `RECEIVE`, `PAUSE`,
      `GET_PARAMETER`, `TEARDOWN`, `ANNOUNCE` and `RECORD` steps against a `-Raw` recorder answering
      with `{CSEQ}`, and `request.bin` holds each request; the script's help describes how.
- [ ] A test in `Surl.Conformance.UnitTests` covers the driver's script parsing and output; the
      library is never loaded unless its SHA-256 matches the pin.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
