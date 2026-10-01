---
id: BL-323
title: Pin the reference build's libcurl-4.dll and drive curl_ws_send and curl_ws_recv against a recorder
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-285]
touches: [UpstreamCurlBuilds.json, Record-CurlExchange.ps1, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-323 — Pin the reference build's libcurl-4.dll and drive curl_ws_send and curl_ws_recv against a recorder

## Goal

The Windows reference build's `libcurl-4.dll` is pinned by SHA-256 in `UpstreamCurlBuilds.json`
as a library, and a C# file-based app drives it (`curl_ws_send`, `curl_ws_recv`) so the WebSocket
client frames only libcurl's API sends can be measured and proved (ADR-0071 decision 10).

## Context

- ADR-0071 decision 10 decided the pin: `C:\Program Files\Git\mingw64\bin\libcurl-4.dll`, 898732
  bytes, product version 8.21.0, SHA-256
  `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574`, beside the reference
  `curl.exe` (already on disk: no download, no question for Stewart).
- An entry that is a library, not a curl build: `Surl.Conformance.UnitLibrary`'s
  `UpstreamCurlBuildPins` and `UpstreamCurlLocator` and `Record-CurlExchange.ps1`'s pin check must
  know the kind, so a library is never run as a curl and a curl never loaded as the library.
- The driver: a C# file-based app (`dotnet run <file>.cs`, BCL only, `DllImport`/`LibraryImport`
  of the pinned file by full path after its SHA-256 is checked), `curl_easy_init`,
  `CURLOPT_URL`, `CURLOPT_CONNECT_ONLY` 2, `curl_easy_perform`, then a script of `curl_ws_send`
  (text, binary, fragments with `CURLWS_CONT`, `CURLWS_PING`, `CURLWS_CLOSE` with a code) and
  `curl_ws_recv` calls, printing each result (`CURLcode`, `curl_ws_frame` flags, bytes).
- `Record-CurlExchange.ps1 -Raw` with `{WS_ACCEPT}` (BL-285) is the server side for measuring;
  add a mode or parameter that runs the driver in place of `curl.exe`, described in the help.
- Linux and macOS static builds have no shared library: the driver reports Inconclusive there.

## Acceptance criteria

- [ ] `UpstreamCurlBuilds.json` has the library entry; `UpstreamCurlBuildPins` and
      `UpstreamCurlLocator` read it with tests in `Surl.Conformance.UnitTests`; a file whose
      SHA-256 differs is refused.
- [ ] The driver sends a scripted text message to a `-Raw` recorder answering `101` with
      `{WS_ACCEPT}`, and `request.bin` holds the masked frame; the script's help describes it.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
