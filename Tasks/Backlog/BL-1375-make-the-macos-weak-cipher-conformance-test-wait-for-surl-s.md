---
id: BL-1375
title: Make the macOS weak-cipher conformance test wait for surl's negotiated-algorithms log note
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.cs, Conformance/Surl.Conformance.UnitTests/SshLogNotes.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-1375 — Make the macOS weak-cipher conformance test wait for surl's negotiated-algorithms log note

## Goal

`UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes`
passes reliably on both the osx-arm64 and osx-x64 published-binary conformance jobs, so the `v0.1.1` Release run can pass.

## Context

- Release run 44438549943 (tag `v0.1.1`, 2026-11-19) failed "Conformance against the published osx-x64" on
  `("cast128-cbc")` only: exit code and empty stdout matched, but
  `StringAssert.Contains(NegotiatedAlgorithms(surl), "cipher cast128-cbc/cast128-cbc,")` saw `""`. 1,516 passed,
  1 failed. The same test passed on osx-arm64 in that run and in PR #108's run.
- `Conformance/Surl.Conformance.UnitTests/UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.cs:73-82` reads
  `surl.Log` once, right after curl exits. Curl dies with SIGSEGV just after NEWKEYS, so surl can still be
  writing its `SSH negotiated kex ...` note (`SshLogNotes.cs:55`) when the test reads the log: a race,
  likelier on the slower Intel runner.
- The tag `v0.1.1` was deleted after the failure; it is re-tagged once this merges green.

## Acceptance criteria

- [ ] The test waits (bounded, e.g. up to 10 s, polling the log through the injected or test-side clock - no
      `Thread.Sleep`; `Task.Delay` in test code is fine) until a `SSH negotiated` note appears before asserting
      on it, or fails with the log in the message after the bound.
- [ ] The same wait is applied to the other `NegotiatedAlgorithms(surl)` assertions in that file, through one
      shared helper, so none of them reads the log once.
- [ ] Assertions are unchanged otherwise (exit code, empty stdout, `cipher <c>/<c>,`).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

- The test is `OSCondition(OSX)` and an integration-style conformance test, so a Windows lane cannot run it; the
  change must be correct by reading. The next Release run proves it.

## Log

- 2026-11-18: Created.
