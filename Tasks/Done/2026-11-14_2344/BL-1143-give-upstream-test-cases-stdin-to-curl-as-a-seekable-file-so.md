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
completed: 2026-11-14
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

- [x] A unit test pins that the runner gives curl's stdin as a seekable file for a case with `<stdin>` bytes.
- [x] CI confirmation and the list additions moved to BL-1148 (a lane cannot push or dispatch CI); this task's code is in place.
- [x] Windows still passes both: the Windows path is unchanged (still a pipe).

## Notes

- Decision: .NET cannot give a child a file as stdin, so on non-Windows with non-empty `<stdin>` the runner writes a temp file and starts `/bin/sh -c 'stdin_file=$1; shift; exec "$0" "$@" < "$stdin_file"' <curl> <file> <args...>`. Arguments are positional parameters, never shell-parsed. Windows keeps the pipe. `CreateStartInfo(..., standardInputFile)` is unit-tested; the process start is coverage-excluded as before.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. Runner gives curl a seekable stdin file on Linux/macOS; CI confirmation split to BL-1148
