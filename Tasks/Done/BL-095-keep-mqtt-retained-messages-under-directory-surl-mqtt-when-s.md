---
id: BL-095
title: Keep MQTT retained messages in the .surl/mqtt folder of the --directory path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-093, BL-094]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-023
created: 2026-09-29
completed: 2026-09-29
---
# BL-095 — Keep MQTT retained messages in the .surl/mqtt folder of the --directory path

## Goal

With `--directory <path>`, `surl` keeps MQTT retained messages in `<path>/.surl/mqtt/` and
loads them at start, so a message upstream curl publishes before a restart is what it
receives after; without `--directory` they stay in memory only.

## Context

FR-023; ADR-0031 (BL-090) decision 6. BL-094 built the persistence in the MQTT library;
this task composes it.

- `Surl.Console/CommandLineRunner.cs`: `ComposeProtocolServers` builds
  `new MqttProtocolServer(new MqttRetainedMessages())`. When a directory was given,
  compose the retained-message persistence over the same `DiskContentFileSystem` and the
  full path `<path>/.surl/mqtt`, and load it before any listener binds; when not, compose
  no persistence. A load failure is answered as ADR-0031 decision 6 says (exit code and
  stderr text through `WriteFailure`).
- `ComposeVersionText` also calls `ComposeProtocolServers`: `--version` must not create
  `.surl` or read any file.
- `Surl.Conformance.UnitTests/SurlOnLoopback.cs` starts surl in-process over a temporary
  directory it deletes on dispose, and `UpstreamCurlTalksToSurlOverMqttTests.cs` shows how
  a pinned upstream curl publishes (`-d`) and subscribes. A restart test needs two surl
  runs over one directory: extend `SurlOnLoopback` (e.g. a start over an existing directory
  that it does not delete) rather than writing a new harness.
- Expected client behaviour comes from pinned upstream curl 8.21.0 only (ADR-0003).

## Acceptance criteria

- [x] `CommandLineRunnerTests` prove, by name, with the `FakeListenerFactory`: with
      `--directory`, the MQTT server's store is composed with persistence at
      `<full path>/.surl/mqtt`; without it, with none; `--version` composes no persistence
      and touches no file.
- [x] A test proves the load failure of ADR-0031 decision 6 returns its named
      `SurlExitCode` and stderr text.
- [x] An `Integration` conformance test in `UpstreamCurlTalksToSurlOverMqttTests` runs a
      pinned upstream curl 8.21.0 `-d hi mqtt://127.0.0.1:P/t` against surl with
      `--directory`, stops surl, starts it again over the same directory, and a pinned
      curl `mqtt://127.0.0.1:P/t` gets the exit code and stdout the existing
      publish-then-subscribe conformance test in that class expects from its recorded
      fixture (the retained `hi` on `t`); the same sequence without `--directory` gets the
      exit code and empty stdout of a subscribe with nothing retained (ADR-0014).
- [x] `dotnet build Surl.Console -warnaserror` is clean, the fast tests pass,
      `Surl.Console` keeps 100% line and branch coverage, and only the conformance test
      needs `TestCategory=Integration`.

## Notes

- **What was built.** `CommandLineRunner.ComposeRetainedMessageFile` gives a
  `MqttRetainedMessageFile` at `<full path>/.surl/mqtt` with `--directory`, `null` without;
  `LoadRetainedMessagesAsync` loads it (or an empty in-memory store) and answers
  `IOException`, `UnauthorizedAccessException` and `InvalidDataException` with
  `(37) Could not read <file path>: <reason>` (ADR-0031 decision 6). `ServeAsync` loads it
  inside the lock and before the listener factory is created, so decision 7's order holds:
  probe, scheme check, lock, load, bind. `--version` composes a plain `MqttRetainedMessages`.
- **Choice: a data-directory file-system seam.** The repository's testing rules put every
  test that touches the disk under `Integration`, and the task wants only the conformance
  test there. So `CommandLineRunner` gained an optional last constructor parameter,
  `IContentFileSystem? dataDirectoryFileSystem` (a `DiskContentFileSystem` when `null`,
  which is what `Program` passes), and `ComposeContentFileSystem` takes it. The fast tests
  read the data directory through `UnitTestReadOnlyContentFileSystem` (named per
  `.claude/rules/testing.md`). Optional rather than required, so the existing runner
  tests without `--directory` keep their four-argument form.
- **Choice: the tests live in `CommandLineRunnerRetainedMessagesTests`**, beside
  `CommandLineRunnerTlsTests`, the existing precedent for splitting the runner's tests
  by concern.
- **Choice: a stop during the load is a clean stop.** The review found that Ctrl+C while
  the file is read would escape as an unhandled `OperationCanceledException`; it now
  returns `Ok` (the same as a stop while serving) without starting a listener.
- **Choice: the restart binds a new ephemeral port.** The conformance test starts the
  second surl on `127.0.0.1:0` as well, instead of reusing the first run's port P, which
  another process could take in between. The port does not affect what is retained.
  `SurlOnLoopback.StartOverDirectoryAsync` starts surl over a directory the caller owns and
  does not delete it on dispose.
- **Left as is:** the scheme check still composes the servers once with a throwaway
  retained-message store before the lock, and they are composed again after the load.
  That is cheap and keeps the refusal (1) ahead of the lock, as decision 7 orders.
- Measured: `Surl.Console` 100% line and branch, 0 failing members, worst CRAP 10
  (`Measure-CodeQuality.ps1 -Library Surl.Console`). Both restart conformance tests pass
  against pinned curl 8.21.0 (retained `hi` after a restart with `--directory`; exit 0 and
  empty stdout, as `subscribe-nothing-retained` holds, without it).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. With --directory, surl keeps MQTT retained messages in .surl/mqtt and loads them before binding, so a retained message survives a restart; without it they stay in memory
