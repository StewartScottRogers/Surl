---
id: BL-043
title: Serve tftp from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-037, BL-031, BL-032, BL-020]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-043 — Serve tftp from surl and prove it with pinned upstream curl

## Goal

`surl tftp://127.0.0.1:<port>/` serves the served directory over TFTP. It uses the UDP
listener (BL-031), the engine's datagram dispatch (BL-032) and the TFTP server (BL-037).
Integration tests prove that the pinned upstream curl 8.21.0 build downloads from it as
BL-037's recordings predict.

## Context

- BL-037's TFTP server and its fixtures in `Surl.Protocol.Tftp.UnitTests/Fixtures/` hold
  each case's command line and expected result.
- Register the UDP listener factory from `Surl.Networking` and the TFTP server for
  `tftp` in `Surl.Console`'s explicit composition, with the same content store as `http`.
- The status line (BL-016) must report the UDP port bound for `tftp://127.0.0.1:0/`, so
  the test can find it. If BL-016's format cannot say UDP, file a `Surl.Output` task
  and make this one depend on it.
- BL-020's process runner, in-process `surl` start and `Assert.Inconclusive` rule are
  reused.

## Acceptance criteria

- [x] `Surl.Console` registers the UDP listener factory and the TFTP server for `tftp`. A
      fast test in `Surl.Console.UnitTests` proves a `tftp://` listen URL starts a
      datagram listener with it.
- [x] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build against a live `surl` for: a default read, `--tftp-blksize 1024`,
      `--tftp-no-options`, a 512-byte file, and a missing file. Each asserts exit code
      and stdout equal BL-037's recordings.
- [x] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [x] Any disagreement with the pinned build is fixed in `Surl.Protocol.Tftp` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

- 2026-09-28, from BL-032: the serving engine now starts a datagram listener for a scheme
  whose server is an `IDatagramProtocolServer`. Two doc comments in `Surl.Console` still
  say the engine refuses such a scheme before any listener starts (BL-032): the remarks on
  `TcpListenerFactory` and the `<exception>` on
  `ListenerStartReporter.StartDatagramListenerAsync`. Registering TFTP here has to correct
  both.
- 2026-09-29, delivered: `CommandLineRunner` registers `TftpProtocolServer` over the one
  content store, and the scheme check now counts datagram servers too (it only counted
  `IConnectionProtocolServer`s). `ListenerStartReporter.StartDatagramListenerAsync` now
  starts through the inner factory and reports like a connection start, so the status line
  `Listening on tftp://127.0.0.1:<port>/` carries the UDP port: BL-016's format needed no
  change and no `Surl.Output` task was filed. `TcpListenerFactory` no longer exists, so only
  the reporter's stale `<exception>` needed correcting; it is gone with the throw.
- The conformance tests serve each read case's recorded `stdout.bin` as the file, because
  the recorder answered curl with exactly those bytes as the file's contents; all five pass
  against the pinned win-x64 8.21.0 build, so no disagreement task was filed.
- `dotnet format --verify-no-changes` still reports line endings in
  `Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs`, untouched here and outside this task.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl tftp:// serves the served directory over UDP; pinned upstream curl 8.21.0 reads from it as BL-037's recordings predict
