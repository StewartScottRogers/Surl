---
id: BL-1143
title: Give upstream test cases' stdin to curl as a seekable file so 1072 and 1073 exit 25 on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1141]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1143 — Give upstream test cases' stdin to curl as a seekable file so 1072 and 1073 exit 25 on Linux and macOS

## Goal

Upstream tests 1072 and 1073 (`-T -` with `--anyauth`, HTTP/1.0 reply) end with curl exit 25 on Linux and macOS, as they already do on Windows.

## Context

- CI run 43637240745 (head `ca55e264`): on `linux-x64` and `osx-arm64`, curl exits 65 (`curl: (65) seek callback returned error 2`, `CURLE_SEND_FAIL_REWIND`), expected 25.
- Cause: the runner (`UpstreamCurlRunner`) gives curl's stdin as a pipe. After the 401 or 301, curl's tool rewinds the stdin upload with `lseek`; on a pipe that fails on Linux and macOS (`CURL_SEEKFUNC_CANTSEEK` = 2), so curl stops at 65 before it reaches the 25 the HTTP/1.0 chunked case gives. Upstream's own test runner redirects stdin from a file, which is seekable. Windows passes because its C runtime's `lseek` does not fail on a pipe.
- Fix: write a case's `<stdin>` bytes to a temp file and start curl with that file as stdin (a shell-free redirect: `ProcessStartInfo.RedirectStandardInput = false` and open the file handle, or run with the file as inherited stdin), so the exchange matches upstream's. Keep the pipe path for callers that need it (`Record-CurlExchange.ps1` parity is by measurement; do not change it).
- Verify against a pinned upstream build with `Record-CurlExchange.ps1` or the suite on Linux/macOS CI (dispatch `gh workflow run CI --ref factory/phase-1`).

## Acceptance criteria

- [ ] A unit test pins that the runner gives curl's stdin as a seekable file for a case with `<stdin>` bytes.
- [ ] A dispatched CI run shows `test1072` and `test1073` passing on `linux-x64` and `osx-arm64`, and their numbers are added to the two `upstream-test-cases-passing.<platform>.txt` lists.
- [ ] Windows still passes both.

## Notes

## Log

- 2026-11-14: Created.
