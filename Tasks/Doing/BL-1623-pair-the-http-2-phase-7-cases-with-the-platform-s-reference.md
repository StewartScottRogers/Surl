---
id: BL-1623
title: Pair the HTTP/2 Phase 7 cases with the platform's reference build off Windows, as the mirrored HTTP/2 test does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-24
completed:
---
# BL-1623 — Pair the HTTP/2 Phase 7 cases with the platform's reference build off Windows, as the mirrored HTTP/2 test does

## Goal

Off Windows, every case of `CurlPortBesideUpstreamCurlOverHttp2Tests` runs upstream curl through the platform's reference build, the same build its mirrored `UpstreamCurlFetchesFromSurlOverHttp2Tests` case runs, so the HTTP/2 family is compared on linux-x64 and osx-arm64 instead of being Inconclusive.

## Context

`Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp2Tests.cs`, `MeasureAsync`, always sets `new CurlPortRunSetup { UpstreamBuildSha256 = Http2WindowsBuildSha256 }` (`589C8E4D…2648`, the static OpenSSL win-x64 build). The mirrored test picks that build only on Windows (`RunUpstreamCurlAsync`: `OperatingSystem.IsWindows() ? RunSupplementaryBuildAsync(…, Http2WindowsBuildSha256, …) : RunAsync(…)`), because the Windows reference build has no HTTP2 feature, while the linux-x64 and osx-arm64 reference builds in `UpstreamCurlBuilds.json` list `HTTP2`. `CurlPortBesideUpstreamCurlOverHttp3Tests` already does it right: `UpstreamBuildSha256 = OperatingSystem.IsWindows() ? Http3WindowsBuildSha256 : null`. Result: in linux-x64 run 44044472038 all 15 HTTP/2 cases were "not executed" (`Documentation/Conformance/CurlPort/Http2.md`, linux-x64 section), so the HTTP/2 family has never been measured off Windows.

The other hard-coded Windows hashes were checked and are fine: `CurlPortBesideUpstreamCurlOverHttpTests`' three uses of `StaticCurlWindowsBuildSha256` sit in `[OSCondition(OperatingSystems.Windows)]` tests, and `CurlPortBesideUpstreamCurlOverSshTests` already chooses by `OperatingSystem.IsWindows()`.

ADR-0089 decision 2 (each case paired with the build its mirrored test runs). The Curl port is never an oracle (ADR-0003). This is a test-only change; nothing in `Surl.Conformance.UnitLibrary` changes.

## Acceptance criteria

- [ ] `CurlPortBesideUpstreamCurlOverHttp2Tests.MeasureAsync` sets `UpstreamBuildSha256` to `Http2WindowsBuildSha256` on Windows and to `null` (the platform's reference build) elsewhere, in the form `CurlPortBesideUpstreamCurlOverHttp3Tests` uses, and the class summary says so.
- [ ] On Windows the HTTP/2 cases still pair with `589C8E4D…2648`: `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~CurlPortBesideUpstreamCurlOverHttp2Tests"` gives the same outcome on a lane as before the change (Inconclusive where the pinned port or build is absent, otherwise the findings `Http2.md` lists for win-x64).
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests (`TestCategory!=Integration`) pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
