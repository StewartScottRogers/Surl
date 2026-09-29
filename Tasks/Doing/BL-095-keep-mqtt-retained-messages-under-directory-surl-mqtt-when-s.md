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
completed:
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

- [ ] `CommandLineRunnerTests` prove, by name, with the `FakeListenerFactory`: with
      `--directory`, the MQTT server's store is composed with persistence at
      `<full path>/.surl/mqtt`; without it, with none; `--version` composes no persistence
      and touches no file.
- [ ] A test proves the load failure of ADR-0031 decision 6 returns its named
      `SurlExitCode` and stderr text.
- [ ] An `Integration` conformance test in `UpstreamCurlTalksToSurlOverMqttTests` runs a
      pinned upstream curl 8.21.0 `-d hi mqtt://127.0.0.1:P/t` against surl with
      `--directory`, stops surl, starts it again over the same directory, and a pinned
      curl `mqtt://127.0.0.1:P/t` gets the exit code and stdout the existing
      publish-then-subscribe conformance test in that class expects from its recorded
      fixture (the retained `hi` on `t`); the same sequence without `--directory` gets the
      exit code and empty stdout of a subscribe with nothing retained (ADR-0014).
- [ ] `dotnet build Surl.Console -warnaserror` is clean, the fast tests pass,
      `Surl.Console` keeps 100% line and branch coverage, and only the conformance test
      needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
