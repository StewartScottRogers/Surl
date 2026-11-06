---
id: BL-936
title: Write the Curl port's libcurl driver twins and run the WebSocket libcurl rows through them beside upstream libcurl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-942, BL-935]
touches: [Conformance/Surl.Conformance.UnitTests, Run-CurlPortLibcurlWebSocketScript.cs, Run-CurlPortLibcurlRtspScript.cs, CurlPortLibcurlReference.targets, Documentation/Conformance/CurlPort/Libcurl.md]
requirement: FR-057
created: 2026-11-06
completed:
---
# BL-936 — Write the Curl port's libcurl driver twins and run the WebSocket libcurl rows through them beside upstream libcurl

## Goal

`Run-CurlPortLibcurlWebSocketScript.cs` and `Run-CurlPortLibcurlRtspScript.cs` - the Curl port's twins of
the two upstream libcurl drivers, taking the same arguments and printing the same report through the
port's pinned managed `Libcurl` - exist, `PinnedLibcurlDriver` runs either side, and the `Integration`
class `CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests` runs every step list of
`PinnedLibcurlTalksToSurlOverWebSocketTests` through both drivers against a fresh `surl` each and reaches
a verdict, with `Documentation/Conformance/CurlPort/Libcurl.md` naming what was measured and the
WebSocket rows' result.

## Context

- Fourth of the five tasks BL-597 ("Drive the Curl port's managed Libcurl through the WebSocket and RTSP
  libcurl scripts beside the pinned upstream libcurl") was split into on 2026-11-06 after it timed out
  at 120 minutes. Needs BL-942 (the library pin, `CurlPortLibcurlChoice`) and BL-935 (the comparison;
  it needs BL-934's shared script and report). Requirement FR-057; ADR-0089 decisions 1 (c), 4, 5, 6 and 8;
  Windows only, as the upstream libcurl pin is (ADR-0071 decision 10, ADR-0074 decision 12).
- How the twins reach the port: a file-based app has no directive for an assembly reference, so each
  twin imports `CurlPortLibcurlReference.targets` (`#:property CustomAfterMicrosoftCommonTargets=...`),
  which references the `Libcurl.dll` that the environment variable `SURL_CURL_PORT_LIBCURL` names (and
  `Curl.Api.UnitLibrary.dll` beside it) without copying it; at run time the twin checks the file's
  SHA-256 against the pin through `CurlPortLibcurlChoice` before loading any of its types. Exit codes are
  the upstream drivers': 0 all steps ran, 1 perform failed, 2 malformed command line, 3 not pinned,
  4 Inconclusive.
- The work is written; the lost run measured all 7 WebSocket rows agreeing. Take it from the stash
  `darkfactory BL-597 20261106-033059` in the main checkout (`Z:\repos\Surl`; find its index N with
  `git stash list`, matching the message; never pop, apply or drop it). None of these files has moved on
  the branch since the stash's base `6b47feae`.
  - Tracked: `git diff "stash@{N}^" "stash@{N}" -- Conformance/Surl.Conformance.UnitTests/PinnedLibcurlDriver.cs Conformance/Surl.Conformance.UnitTests/PinnedCurlPortBesideUpstreamCurl.cs | git apply`
    (`RunScriptAsync` with an environment and the exit code, `CurlPortTwinOf`, `CurlPortLibcurlVariable`;
    `SettledNotesAsync` and `SurlCommitAsync` become `internal`).
  - Untracked, each with `git checkout "stash@{N}^3" -- <path>`: `Run-CurlPortLibcurlWebSocketScript.cs`,
    `Run-CurlPortLibcurlRtspScript.cs`, `CurlPortLibcurlReference.targets`,
    `Conformance/Surl.Conformance.UnitTests/PinnedCurlPortLibcurlBesideUpstreamLibcurl.cs`,
    `Conformance/Surl.Conformance.UnitTests/CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests.cs`,
    and `Documentation/Conformance/CurlPort/Libcurl.md` (only a stub heading and an empty findings table;
    fill it in as below). Leave `CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests.cs` for the next task.
- The port library is published on this machine (`CurlPortBuilds.json`'s `kind: "library"` entry, by
  BL-942). The upstream `libcurl-4.dll` is pinned in `UpstreamCurlBuilds.json`; never call the port's
  output an expected value (ADR-0003 decision 6).
- `Libcurl.md` follows the other family files under `Documentation/Conformance/CurlPort/` (see
  `WebSocket.md`): a `## Measured` table naming the port commit and the `Libcurl.dll` SHA-256, the upstream
  `libcurl-4.dll` SHA-256 and version line, Surl's commit, platform and date; a `## Result` per family;
  `## Findings` in ADR-0089 decision 6's form.
- Stand-in: not applicable; a conformance measurement, not a server feature.

## Acceptance criteria

- [ ] `Run-CurlPortLibcurlWebSocketScript.cs`, `Run-CurlPortLibcurlRtspScript.cs` and
      `CurlPortLibcurlReference.targets` exist at the repository root; `dotnet run Run-CurlPortLibcurlWebSocketScript.cs`
      with `SURL_CURL_PORT_LIBCURL` unset fails the build with the targets' "Set SURL_CURL_PORT_LIBCURL"
      error, and with `--library` naming an unpinned file exits 3.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests"`
      on Windows reaches a verdict on every row (none Inconclusive on this machine) and passes; off
      Windows, or with a pinned file missing, each row is Inconclusive.
- [ ] `Documentation/Conformance/CurlPort/Libcurl.md` has its `## Measured` table (port commit
      `b42c319ba940f66452b2b6f3e9910d46825b08fe` and `Libcurl.dll` SHA-256
      `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311`, the upstream `libcurl-4.dll`
      SHA-256 from `UpstreamCurlBuilds.json`, Surl's commit, Windows, the date) and a WebSocket result
      that lists each finding in ADR-0089 decision 6's form or states there are none.
- [ ] `PinnedLibcurlTalksToSurlOverWebSocketTests` and `PinnedLibcurlTalksToSurlOverRtspTests` still pass
      on Windows through the reshaped `PinnedLibcurlDriver.RunAsync`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
