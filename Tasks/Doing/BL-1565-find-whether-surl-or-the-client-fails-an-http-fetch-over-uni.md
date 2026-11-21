---
id: BL-1565
title: Find whether surl or the client fails an HTTP fetch over --unix-socket on macOS, with pinned upstream curl, and fix surl's side
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1565 — Find whether surl or the client fails an HTTP fetch over --unix-socket on macOS, with pinned upstream curl, and fix surl's side

## Goal

Pinned upstream curl fetches a file over HTTP from `surl --unix-socket <relative path>` on macOS as it does on
Windows and Linux. A conformance test proves it on osx-arm64 and osx-x64. If surl is at fault, it is fixed.

## Context

- Surly cookbook report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-21T0725-Surl-bug-report.md` (Surl
  `9ad60d80c`, client the Curl port `7d167fa10a`, so not evidence; the same report went to the Curl repo). On
  `macos-latest`, in a folder holding `site/hello.txt`:
  `surl --directory site --unix-socket surl.sock http://localhost:8080/` prints
  `Listening on http://localhost:8080/ over unix://surl.sock`, then
  `curl -s --unix-socket surl.sock http://localhost:8080/hello.txt` prints nothing and exits 55 (send failure).
  Windows and Linux pass. The path is short and relative, well under macOS's 103-byte `sun_path`.
- Surl's Unix-socket listener: `UnixSocketConnectionListener` and `UnixSocketBindRules`
  (`Foundation/Surl.Networking.UnitLibrary`, ADR-0094 decisions 3, 5 and 6, BL-547). The socket file is made `0600`
  between bind and listen off Windows, a relative path is resolved somehow, and there's no `NoDelay` on AF_UNIX.
  Plausible macOS-only causes: the relative path resolved against a different directory, a socket option or
  lingering-close step that fails on macOS AF_UNIX, or `StreamConnection`'s FIN and lingering read (ADR-0021)
  behaving differently on macOS Unix sockets.
- Conformance has an FTP-over-Unix-socket class (`UpstreamCurlUsesSurlOverFtpOnAUnixSocketTests.cs`) but no HTTP
  one. Check whether the FTP one runs, or is skipped, on macOS in the Release workflow's conformance jobs.

## Acceptance criteria

- [x] A conformance test (e.g. `UpstreamCurlFetchesFromSurlOverHttpOnAUnixSocketTests`) runs pinned upstream curl
      `--unix-socket <relative path> http://localhost/hello.txt` against the published surl, with a relative socket
      path as in the report, plus an absolute-path case, and asserts exit 0 and the file's bytes. It runs on every
      platform that has Unix sockets.
- [ ] A Release dry run (`dispatch-workflow.ps1`; park as Blocked "Waiting for run:<id>") shows the result on
      osx-arm64 and osx-x64; its run id and, if it fails, curl's `-v` output and surl's `-v` log are in the Log.
- [ ] If it fails with pinned upstream curl, the cause in surl is named with file:line and fixed, with a named fast
      test in `Surl.Networking.UnitTests`, and a second Release dry run passes on both Macs.
- [ ] If it passes with pinned upstream curl, the Log says so, and that the fault is on the client's side (the Curl
      port), which the Curl repo tracks; nothing in surl changes.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- 2026-11-21: Added `UpstreamCurlFetchesFromSurlOverHttpOnAUnixSocketTests` (relative and absolute socket path); both pass on Windows with the pinned curl. The Release dry run cannot be dispatched yet: `dispatch-workflow.ps1` refuses an unpushed ref and a lane does not push, so it needs this commit integrated first. Next run: dispatch `release.yml` on the shift branch, read the osx-arm64 and osx-x64 conformance results, then follow the remaining criteria.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Conformance test written and green on Windows; the Release dry run needs this commit pushed first (dispatch refuses an unpushed ref), so re-run after the shift integrates it
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Release dry run still cannot be dispatched: factory/lane-1 is not pushed (the shift pushes on integration); re-run after the shift integrates 4670a104c
- 2026-11-21: Backlog -> Doing.
