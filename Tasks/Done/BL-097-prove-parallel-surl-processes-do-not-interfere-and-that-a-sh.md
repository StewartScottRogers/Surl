---
id: BL-097
title: Prove parallel surl processes do not interfere, and that a shared directory is refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-092, BL-095, BL-096]
touches: [Surl.Conformance.UnitTests]
requirement: FR-025
created: 2026-09-29
completed: 2026-09-29
---
# BL-097 — Prove parallel surl processes do not interfere, and that a shared directory is refused

## Goal

`Integration` tests start real, separate `surl` processes and prove that processes with
different directories, and in-memory processes, never see each other's files, and that a
second process on a directory in use is refused while the first keeps serving.

## Context

FR-025; ADR-0031 (BL-090) decisions 7 and 8. Everything under test is already built:
in-memory mode (BL-093), `.surl` hidden (BL-092), MQTT state under `.surl/mqtt` (BL-095),
the lock (BL-096).

- New file `Surl.Conformance.UnitTests/ParallelSurlProcessesTests.cs`, every test
  `[TestCategory("Integration")]`. `SurlOnLoopback` runs surl in-process, which cannot
  prove process isolation, so these tests start separate processes instead:
  `Surl.Conformance.UnitTests` references `Surl.Console`, so `surl.dll` and
  `surl.runtimeconfig.json` are in the test output folder. Start each with
  `ProcessStartInfo` running the `dotnet` host (`DOTNET_HOST_PATH` when set, else
  `dotnet`) with `exec <output folder>/surl.dll` and `ArgumentList`, a fresh temporary
  `WorkingDirectory`, and redirected stdout and stderr; read the bound port from each
  `Listening on ` status line. Kill each process tree in `finally` so a failure never
  leaks one. If `surl.runtimeconfig.json` is not copied, say so in the Log and fix it in
  `Surl.Conformance.UnitTests.csproj` only.
- The only servers that accept uploads today are TFTP (`TftpWriteTransfer`); the HTTP
  server refuses `PUT` with `405`. So each process listens on `tftp://127.0.0.1:0/`, and
  the client is the pinned upstream curl 8.21.0 through `PinnedUpstreamCurl.RunAsync`
  (`-T <file> tftp://127.0.0.1:P/x` to upload, `tftp://127.0.0.1:P/x` to fetch). Never the
  Curl port (ADR-0003). Read curl's TFTP not-found exit code from the first run of the
  test, do not assume it.
- Test-owned temporary directories come from `Directory.CreateTempSubdirectory` (test code
  may; production code may not, decision 8) and are deleted in `finally`.
- Platform-neutral: no drive letters, no Windows-only text (root `CLAUDE.md`).

## Acceptance criteria

- [x] `TwoProcesses_DifferentDirectories_EachServesOnlyItsOwnUploads`: A and B with
      `--allow-uploads --directory <dirA|dirB> tftp://127.0.0.1:0/`; curl uploads `x` with
      different bytes to each; curl fetches `x` from each and gets that process's own
      bytes; `dirA` holds `x` and `.surl` only, and so does `dirB`.
- [x] `TwoProcesses_InMemory_ShareNothing`: A and B with `--allow-uploads
      tftp://127.0.0.1:0/` and no `--directory`; curl uploads `x` to A; curl's fetch of `x`
      from B fails with the not-found exit code; both working directories stay empty.
- [x] `SecondProcess_SameDirectory_IsRefusedAndFirstKeepsServing`: B started on A's
      directory exits within 30 s with ADR-0031's `SurlExitCode` and stderr text, writes no
      `Listening on` line, and curl still fetches `x` from A afterwards.
- [x] `Restart_SameDirectory_ServesTheEarlierUpload`: after A is stopped, C on the same
      directory starts (not refused by the lock file A left) and curl fetches A's `x`.
- [x] `rg -n "new Mutex|new Semaphore|Semaphore.OpenExisting|Mutex.OpenExisting|GetTempPath|GetTempFileName|CreateTempSubdirectory" --glob "*.cs" --glob "!*UnitTests/**" --glob "!.github/**"`
      finds nothing (ADR-0031 decision 8); the command and its empty result are written in
      this task's Log.
- [x] Each test finishes within 60 s and passes on Windows; `dotnet test --filter
      "TestCategory!=Integration"` is unaffected and green; `dotnet build
      Surl.Conformance.UnitTests -warnaserror` is clean.

## Notes

- Test-only task, so the `/feature` stages collapsed to: write the four `Integration`
  tests, run them against pinned upstream curl 8.21.0, verify. No production code changed
  and no ADR was needed: ADR-0031 decisions 7 and 8 already fix every byte asserted.
- `surl.runtimeconfig.json` is copied to the test output by the existing `Surl.Console`
  project reference: the processes start with `dotnet exec surl.dll`, so the csproj is
  unchanged.
- TFTP not-found exit code: taken from the pinned build's recording in
  `Surl.Protocol.Tftp.UnitTests/Fixtures/missing-file/exitcode.txt` (68), and the first run
  of `TwoProcesses_InMemory_ShareNothing` confirmed curl returns exactly that against
  process B.
- Stopping A in the restart test kills its process tree, so C proves the lock file a
  killed holder leaves never refuses the next start (ADR-0031 decision 7). The test first
  asserts `.surl/lock` exists, so the restart is not vacuous.
- Each test carries `[Timeout(60_000, CooperativeCancellation = true)]`; the refused
  process gets 30 s to exit. All four ran in about 1 s each.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: `rg -n "new Mutex|new Semaphore|Semaphore.OpenExisting|Mutex.OpenExisting|GetTempPath|GetTempFileName|CreateTempSubdirectory" --glob "*.cs" --glob "!*UnitTests/**" --glob "!.github/**"` printed nothing (exit 1, no matches).
- 2026-09-29: ParallelSurlProcessesTests: 4 of 4 passed on Windows against pinned curl 8.21.0; fast tests green; `dotnet build Surl.Conformance.UnitTests -warnaserror` clean.
- 2026-09-29: Doing -> Done. Integration tests prove separate surl processes with different or no directories never share files, and a second process on a held directory exits 124 while the first keeps serving
