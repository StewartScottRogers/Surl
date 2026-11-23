---
id: BL-1607
title: Make every SSH conformance test wait for surl's negotiated-algorithms note through one helper, as BL-1375 did for the weak-cipher test
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/SshLogNotes.cs, Conformance/Surl.Conformance.UnitTests/UpstreamCurlLogsInToSurlOverSshTests.cs, Conformance/Surl.Conformance.UnitTests/UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverSshTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttpTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-1607 — Make every SSH conformance test wait for surl's negotiated-algorithms note through one helper, as BL-1375 did for the weak-cipher test

## Goal

No SSH conformance test can read surl's `SSH negotiated ...` note before surl has written it, on any platform.
`UpstreamCurlLogsInToSurlOverSshTests.HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile`
passes every run.

## Context

- Release run 45088453115 (tag `v0.1.6`, 2026-11-23), attempt 1, "Conformance against the published osx-x64":
  `HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile("ssh-ed25519")` failed at
  `Conformance/Surl.Conformance.UnitTests/UpstreamCurlLogsInToSurlOverSshTests.cs:225`. curl exited 0 and the file
  arrived, but `WriteNegotiatedAlgorithms(surl)` returned `""`: the note wasn't written yet when the test read the log
  once. The rerun passed, and the release published on attempt 2.
- BL-1375 fixed exactly this for `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` with a bounded wait
  (`NegotiatedAlgorithmsAsync`, `:100-103`). The other callers of `SshLogNotes.NegotiatedAlgorithms` still read once:
  - `UpstreamCurlLogsInToSurlOverSshTests` (`WriteNegotiatedAlgorithms`)
  - `CurlPortBesideUpstreamCurlOverSshTests.cs:676`
  - `CurlPortBesideUpstreamCurlOverHttpTests.cs:718`

## Acceptance criteria

- [ ] `SshLogNotes` gains one awaitable helper that waits, bounded (e.g. 10 s, `Task.Delay` polling, no
      `Thread.Sleep`), for a `SSH negotiated` note in a `SurlOnLoopback`'s log, and fails with the log in the message
      after the bound.
- [ ] Every caller above, and BL-1375's private `NegotiatedAlgorithmsAsync`, uses it; no test reads the negotiated
      note once. A search for `SshLogNotes.NegotiatedAlgorithms(` finds only the helper.
- [ ] Assertions are otherwise unchanged.
- [ ] `dotnet build` clean and the fast tests green.

## Notes

- Conformance tests run on Macs only in the Release workflow; this change must be right by reading, and the next
  Release run proves it.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
