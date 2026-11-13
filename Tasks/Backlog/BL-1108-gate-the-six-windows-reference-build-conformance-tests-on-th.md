---
id: BL-1108
title: Gate the six Windows reference-build conformance tests on the pinned build, not on Windows, so win-arm64 passes
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Conformance/Surl.Conformance.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-1108 — Gate the six Windows reference-build conformance tests on the pinned build, not on Windows, so win-arm64 passes

## Goal

The Release workflow's "Conformance against the published win-arm64" job passes, because each
test that pins a quirk of the Windows x64 reference build (Schannel, WinCNG libssh2, SSPI) runs
only where that build is pinned, and the win-arm64 build's own answers are pinned where they
differ.

## Context

Release run 43443817332 (PR #32, commit 810f09e): `Conformance against the published win-arm64`
failed 6 of 1437 tests (1352 passed, 79 skipped). linux-arm64, linux-x64, osx-x64 and osx-arm64
all passed. Every failure is a test gated `[OSCondition(OperatingSystems.Windows)]` that pins
the Windows x64 reference build's behaviour:

| Test | File | Expected | win-arm64 gave |
| --- | --- | --- | --- |
| `Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel` | `UpstreamCurlFetchesFromSurlOverHttpsTests.cs:51` | exit 60 | exit 0 |
| `Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild` | `UpstreamCurlFetchesFromSurlOverHttpsTests.cs:124` | HTTP/1.1 | another version (byte 16) |
| `NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused` | `UpstreamCurlLogsInToSurlOverHttpTests.cs:309` | exit 22 | exit 0 |
| `HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm` ("ecdsa-sha2-nistp256", "ssh-ed25519") | `UpstreamCurlLogsInToSurlOverSshTests.cs:234` | exit 2 | exit 0 |
| `KexInit_WindowsBuild_ListsTheWinCngAlgorithms` | `UpstreamCurlOffersSshAlgorithmsTests.cs:57` | WinCNG list | another list |

All files are in `Conformance/Surl.Conformance.UnitTests/`. The cause is in
`UpstreamCurlBuilds.json`: `win-arm64` pins the stunnel/static-curl build, statically linked
against OpenSSL 4.0.1 with libssh2 on OpenSSL (BL-361, ADR-0151). That's the same kind of build
as the Linux and macOS pins, not the Schannel and WinCNG build pinned for win-x64.
`UpstreamCurlLocator.CurrentPlatform` (`Conformance/Surl.Conformance.UnitLibrary/UpstreamCurlLocator.cs:28`)
already names the platform, e.g. `win-arm64`.

## Acceptance criteria

- [ ] Each of the 6 tests above runs only when the pinned build is the Windows x64 reference build, decided from the pin (its platform or its `version`/`features` in `UpstreamCurlBuilds.json`), not from `OperatingSystem.IsWindows()` or `OSCondition` alone. On win-x64 each still runs and passes.
- [ ] Where the win-arm64 build's answer is the OpenSSL builds' answer already pinned for Linux and macOS (e.g. a sibling test excluded on Windows), that sibling test also runs on win-arm64. No answer is pinned from anything but a pinned upstream curl run, either this Release run's log or a new one.
- [ ] The `CurlPortBeside*` mirrors of these tests (`CurlPortBesideUpstreamCurlOverHttpTests.cs`, `CurlPortBesideUpstreamCurlOverSshTests.cs`) follow the same gate.
- [ ] The Log lists, per test, which gate it now uses and which win-arm64 answer, if any, it pins. Each test's comment names the build it is about.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green on Windows x64.

## Notes

Filed by an interactive session on 2026-11-13. The next frozen pull request's Release run
proves the win-arm64 leg; BL-1074 records that run's URL.

## Log

- 2026-11-13: Created.
