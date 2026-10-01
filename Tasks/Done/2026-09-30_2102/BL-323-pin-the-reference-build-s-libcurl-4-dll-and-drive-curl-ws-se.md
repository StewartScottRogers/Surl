---
id: BL-323
title: Pin the reference build's libcurl-4.dll and drive curl_ws_send and curl_ws_recv against a recorder
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-285]
touches: [UpstreamCurlBuilds.json, Record-CurlExchange.ps1, Run-LibcurlWebSocketScript.cs, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
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

- [x] `UpstreamCurlBuilds.json` has the library entry; `UpstreamCurlBuildPins` and
      `UpstreamCurlLocator` read it with tests in `Surl.Conformance.UnitTests`; a file whose
      SHA-256 differs is refused.
- [x] The driver sends a scripted text message to a `-Raw` recorder answering `101` with
      `{WS_ACCEPT}`, and `request.bin` holds the masked frame; the script's help describes it.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `touches` gained `Run-LibcurlWebSocketScript.cs`: the driver is a new file-based app at the
  repository root, beside `Run-KerberosTestKdc.cs`; no task in Doing names it.
- Pin shape: a `kind` field, `curl` (default, so every existing entry is unchanged) or
  `library`. The library entry is win-x64, role reference, protocols `ws wss`.
  `Locate`, `LocateForProtocol` and `RequirePinned` only answer with curls;
  `LocateLibrary` and `RequirePinnedLibrary` only with libraries; `UpstreamCurlRunner`
  refuses a library; `Assert-PinnedUpstreamCurl -Kind library` matches only library pins, so
  `-Curl libcurl-4.dll` and `-Libcurl curl.exe` are both refused (checked by hand).
- Driver CLI (my choice, the simplest that covers the API): `<url> <step>...`, a step being
  `send:<FLAGS>:<payload>` (FLAGS '+'-joined TEXT/BINARY/CONT/CLOSE/PING/PONG, payload with
  \xHH escapes) or `recv` (retried on CURLE_AGAIN up to `--recv-timeout`, default 5000 ms).
  Exit codes 0 ran, 1 perform failed, 2 bad command line, 3 not pinned, 4 inconclusive (no
  library pinned or installed for the platform). It reuses `Surl.Conformance.UnitLibrary` for
  the pin check, so there is one implementation of it in C#.
- Recorder: `-LibcurlWebSocket` runs the driver (`dotnet run --file`) in place of curl with
  CurlArgs as its arguments; `-Libcurl` names the library (default `libcurl-4.dll` beside
  the reference curl.exe). Refused with `-Curl` and outside Windows.
- Measured 2026-09-30 (for BL-322 to pin properly): `send:TEXT:hello` against `-Raw` with a
  `{WS_ACCEPT}` 101 recorded the upgrade request (no User-Agent: the driver sets none) then
  `81 85` + 4-byte mask + masked `hello`. `send:TEXT+CONT:hel`, `send:CONT:lo`,
  `send:PING:p`, `send:CLOSE:\x03\xE8bye` gave first bytes `01 83`, `00 82`, `89 81`,
  `88 85`: a CONT-only send is still not final (FIN clear); BL-322 should measure which
  flags make libcurl send the final fragment. `recv` read a server
  `81 02 hi` as `flags TEXT, offset 0, bytesleft 0, 2 bytes "hi"`. `curl_easy_cleanup` sends
  no CLOSE frame.
- Invoke the recorder as `& .\Record-CurlExchange.ps1` from PowerShell 7; going through
  `powershell -File` re-quotes CurlArgs elements (an existing quirk, not this task's).
## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. libcurl-4.dll is pinned as kind library and Run-LibcurlWebSocketScript.cs drives curl_ws_send/curl_ws_recv against Record-CurlExchange.ps1 -Raw -LibcurlWebSocket
