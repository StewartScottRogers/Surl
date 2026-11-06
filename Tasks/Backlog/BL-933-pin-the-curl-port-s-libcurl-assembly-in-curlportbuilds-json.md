---
id: BL-933
title: Pin the Curl port's Libcurl assembly in CurlPortBuilds.json as kind library and choose it for a libcurl driver
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, CurlPortBuilds.json]
requirement: FR-057
created: 2026-11-06
completed:
---
# BL-933 — Pin the Curl port's Libcurl assembly in CurlPortBuilds.json as kind library and choose it for a libcurl driver

## Goal

`CurlPortBuilds.json` pins the Curl port's managed `Libcurl` assembly on `win-x64` as an entry of
`kind: "library"` at the commit its `curl` is pinned at, `Surl.Conformance` reads the kind, locates and
verifies a library pin apart from the curl pins, never runs a library as a curl, and chooses the library a
port-side libcurl driver may call, refusing an unpinned one.

## Context

- First of the five tasks BL-597 ("Drive the Curl port's managed Libcurl through the WebSocket and RTSP
  libcurl scripts beside the pinned upstream libcurl") was split into on 2026-11-06 after it timed out at
  120 minutes. This one is BL-597's acceptance criterion 1. Requirement FR-057; decision ADR-0089
  decision 1 (c); the pin file's rules are ADR-0083 decision 2. The port is the thing measured, never an
  oracle (ADR-0003 decision 6); never list it in `UpstreamCurlBuilds.json`.
- The library is already published on Stewart's machine at
  `%LOCALAPPDATA%\Surl\curl-port\b42c319ba940f66452b2b6f3e9910d46825b08fe\libcurl\Libcurl.dll`, SHA-256
  `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311`, from the port's commit `b42c319b…`
  (the commit its `curl` is pinned at) via `git archive` and `dotnet publish Libcurl -c Release`.
- The work is written. Take it from the stash `darkfactory BL-597 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it). None of these files has moved on the branch since the stash's base `6b47feae`.
  - Tracked, with `git diff "stash@{N}^" "stash@{N}" -- <paths> | git apply`: `CurlPortBuilds.json`
    (the `kind` rule in its comment and the library entry), and in `Conformance/Surl.Conformance.UnitLibrary`
    `CurlPortClientLocator.cs` (`LocateLibrary`, `RequirePinnedLibrary`, candidates filtered by kind),
    `CurlPortClientPins.cs` (`kind` parsed, `curl` when absent), `CurlPortClientRunner.cs` (refuses a
    library), `PinnedCurlPortClient.cs` (`Kind`); in `Conformance/Surl.Conformance.UnitTests`
    `CurlPortClientLocatorTests.cs`, `CurlPortClientPinsTests.cs`, `CurlPortClientRunnerTests.cs`.
    Leave `CurlPortComparison.cs`, `PinnedCurlPortBesideUpstreamCurl.cs`, `PinnedLibcurlDriver.cs` and
    `Run-LibcurlWebSocketScript.cs` for the later split tasks.
  - Untracked, each with `git checkout "stash@{N}^3" -- <path>`:
    `Conformance/Surl.Conformance.UnitLibrary/CurlPortClientKind.cs`,
    `Conformance/Surl.Conformance.UnitLibrary/CurlPortLibcurlChoice.cs`,
    `Conformance/Surl.Conformance.UnitTests/CurlPortLibcurlChoiceTests.cs`.
- `CurlPortLibcurlChoice` uses `PinnedLibcurlChoice.InconclusiveExitCode` and `NotPinnedExitCode`, which
  exist on the branch. `CurlPortClientKind`'s doc comment names the driver files
  `Run-CurlPortLibcurlWebSocketScript.cs` and `Run-CurlPortLibcurlRtspScript.cs`, which a later split
  task adds; as plain text, not a `cref`, that is fine.
- Stand-in: not applicable; test plumbing that pins a client, no server, store or event.

## Acceptance criteria

- [ ] `CurlPortBuilds.json` has a `win-x64` entry with `"kind": "library"`, SHA-256
      `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311` and the commit of the `win-x64`
      curl entry, and `CurlPortClientPinsTests.Parse_RepositoryPinFile_PinsTheLibraryAtTheCommitOfTheCurlOnWindows` passes.
- [ ] An unpinned library is refused: `CurlPortClientLocatorTests.LocateLibrary_UnpinnedLibrary_IsRefused`,
      `RequirePinnedLibrary_FileMatchesOnlyACurlPin_IsRefused`, `RequirePinnedLibrary_FileMatchesAnUpstreamPinToo_IsRefused`
      and `CurlPortLibcurlChoiceTests.Choose_RequestedFileIsUnpinned_IsRefused` pass.
- [ ] `CurlPortClientRunnerTests.Constructor_LocationHoldsTheLibrary_ThrowsArgumentExceptionAndNeverRunsIt`,
      `CurlPortClientPinsTests.Parse_NoKind_IsACurl` and `Parse_UnknownKind_ThrowsNamingIt` pass.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
