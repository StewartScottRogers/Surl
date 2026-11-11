---
id: BL-945
title: Record the Linux and macOS build option probes from CI and correct any option they disprove
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-598]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
## Goal

The test log of `UpstreamCurlShowsItsBuildOptionsTests.Probe_EveryOptionTheReferencePinClaims_ShowsTheOptionBuiltIn`
from CI's `Conformance tests` step on the `ubuntu-latest` and `macos-latest` legs is recorded in this
task's Notes, and any option it disproved is corrected in `UpstreamCurlBuildFeatures`.

## Context

- BL-598 wrote the probe test and ran it green against the Windows reference pin. Lanes cannot push,
  so its first Linux and macOS runs happen in CI after the shift integrates it; the `CI` workflow's
  `Conformance tests` step (filter `FullyQualifiedName~Surl.Conformance&TestCategory!=UpstreamTestCases`)
  already runs it with each leg's reference pin.
- Find the run with `gh run list --workflow CI --branch master` and read the step's log
  (`gh run view <id> --log`), looking for the test's per-probe `curl ...: upstream curl <sha> exited <n>`
  lines and any `<option>: expected ...` disagreement.
- The probes least certain off Windows: `xattr` (reads `user.xdg.origin.url` through `getxattr`),
  `cert-status` (the `status_request` extension in OpenSSL 4.0.1's ClientHello), `sha512-256`
  (Digest answered with `algorithm=SHA-512-256`), `DoH` (a TLS connection to the DoH URL), `manual`.
- A disproved option is corrected in `UpstreamCurlBuildFeatures` (and its `UpstreamCurlBuildFeaturesTests`
  expectations), never by weakening the probe (ADR-0080 decision 3).

## Acceptance criteria

- [ ] This task's Notes hold, for linux-x64 and osx-arm64, each probed option with the command and exit
      code the CI log shows, and each option the test logged as not probed.
- [ ] `UpstreamCurlShowsItsBuildOptionsTests` passes on both CI legs on `master`; any option a probe
      disproved is removed from what `UpstreamCurlBuildFeatures.For` claims for that pin.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
