---
id: BL-1056
title: Cover the two Surl.Composition gaps the per-project coverage merge shows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1054]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1056 — Cover the two Surl.Composition gaps the per-project coverage merge shows

## Goal

`Measure-CodeQuality.ps1 -Library 'Surl.Composition.UnitLibrary'` reports `Surl.Composition.UnitLibrary` at
100% line and branch with no failing member.

## Context

- Found by BL-1021 (2026-11-11) on two runs; neither member was changed by BL-1021. Like BL-1055's four
  Surl.Cli gaps, they show since BL-1054 merges each test project's Cobertura report in the script:
  - `SurlServerComposition.ComposeServing(...)` - `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:347`,
    86.96% line, 83.33% branch: lines 364 to 366, the `CoapsDtlsCertificate.FindRefusal` refusal (`(58)` for a
    `coaps` listen URL whose certificate cannot be served over DTLS) is never reached from a fast test.
  - `ComposedServing.WriteHttp3Note(ListenUrl, TextWriter)` - `ComposedServing.cs:189`, 83.33% branch.
- Cover each from `Surl.Composition.UnitTests` (over `StartTestSeams` and `FakeListenerFactory`), or, where a branch is
  the compiler's cached-delegate null check, reshape the code as BL-1055 decides for its Surl.Cli twin.

## Acceptance criteria

- [x] A named `Surl.Composition.UnitTests` test reaches the `CoapsDtlsCertificate.FindRefusal` refusal in `ComposeServing`.
- [x] `WriteHttp3Note` has every branch covered by a named test.
- [x] `Measure-CodeQuality.ps1 -Library 'Surl.Composition.UnitLibrary'` reports no failing member, and the fast tests are green.

## Notes

- Tests only; no production code changed.
- `SurlServerCompositionTests.ComposeAsync_CoapsWithACertificateDtlsCannotServe_FailsWithCertificateProblemAndReleasesTheLock`
  (new `SurlServerCompositionTests.Coaps.cs`): a P-521 ECDSA `--cert`/`--key` served through the `ReadStartFile` seam, so no
  file is written; the start is refused `(58) Could not load the server certificate: ...` and the lock is released.
- `WriteHttp3Note`'s missing branch was the `BoundPort ?? Port` fallback, not the scheme check.
  `ComposedServingTests.ServeAsync_Http3DatagramListenerReportingNoBoundPort_NamesItsListenUrlsPortInTheHttp3Note` covers it
  over a new `FakeListenerFactory.ReportsDatagramBoundPort` switch (default `true`). The shared-port pair hands the datagram
  listener the connection listener's bound port as `Port`, so the fallback still names the right port.
  `ServeAsync_VerboseDnsOnASharedPort_WritesNoHttp3Note` pins the non-https side (a verbose dns start writes no HTTP/3 note).
- `Measure-CodeQuality.ps1 -Library 'Surl.Composition.UnitLibrary'`: 100% line, 100% branch, 0 failing members, worst CRAP 8.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Composition.UnitLibrary measures 100% line and branch with no failing member
