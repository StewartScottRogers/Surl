---
id: BL-597
title: Drive the Curl port's managed Libcurl through the WebSocket and RTSP libcurl scripts beside the pinned upstream libcurl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, CurlPortBuilds.json, Documentation/Conformance/CurlPort/Libcurl.md]
requirement: FR-057
created: 2026-11-04
completed:
---
# BL-597 — Drive the Curl port's managed Libcurl through the WebSocket and RTSP libcurl scripts beside the pinned upstream libcurl

## Goal

The two pinned-libcurl conversations (`PinnedLibcurlTalksToSurlOverWebSocketTests`,
`PinnedLibcurlTalksToSurlOverRtspTests`) have a Phase 7 counterpart: the same steps run through the Curl
port's managed `Libcurl` API, at the commit `CurlPortBuilds.json` pins, against a fresh `surl` beside the
pinned upstream `libcurl-4.dll`, and every disagreement is a Phase 7 finding in
`Documentation/Conformance/CurlPort/Libcurl.md`.

## Context

- Decided by ADR-0089 decision 1(c). The port's `Libcurl` (folder `Libcurl` in `Z:\repos\Curl`) is a managed
  .NET API, not a native `libcurl-4.dll`, so `PinnedLibcurlDriver`'s file-based apps
  (`Run-LibcurlWebSocketScript.cs`, `Run-LibcurlRtspScript.cs`) cannot load it. Write port-side twins of the
  two drivers that take the same arguments and print the same one-line-per-call report, calling the port's
  `Libcurl` assembly published from the pinned commit.
- Pin that assembly as ADR-0083 pins the port's `curl`: by path, SHA-256 and commit in `CurlPortBuilds.json`,
  a new `kind: "library"` entry beside the `curl` executable (mirroring `UpstreamCurlBuilds.json`'s `kind`),
  refused when unpinned, Inconclusive when missing. Never listed in `UpstreamCurlBuilds.json`.
- Compare the two reports line by line with ADR-0089 decision 4's rules (BL-578's comparison), and judge with
  decision 5's verdicts: upstream's driver report must be what the existing test asserts; a port difference
  not listed in `Libcurl.md` fails, and so does a listed one that stops appearing.
- Windows only, as the upstream libcurl pin is (ADR-0071 decision 10, ADR-0074 decision 12).
- The port is the thing measured. Its output never becomes an expected value (ADR-0003 decision 6).

## Acceptance criteria

- [ ] `CurlPortBuilds.json` pins the port's `Libcurl` assembly at the same commit as its `curl` on `win-x64`,
      and a unit test shows an unpinned library is refused.
- [ ] Two `[TestCategory("Integration")]` test classes, `CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests`
      and `CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests`, run each step list of the existing libcurl test
      classes through both drivers against a fresh `surl` and reach a verdict, Inconclusive when a pinned file
      is missing or off Windows.
- [ ] `Documentation/Conformance/CurlPort/Libcurl.md` lists each finding in ADR-0089 decision 6's form, or
      states that there are none, naming the port commit and the upstream library's SHA-256.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Blocked. Stewart: Surl dark factory timed out after 120 min; see Z:\repos\Surl.logs\BL-597-20261106-033059-L2.jsonl
- 2026-11-06: Blocked -> Deferred. Split into BL-933, BL-934, BL-935, BL-936, BL-937 (timed out after 120 min; Stewart asked for the split 2026-11-06)
