---
id: BL-580
title: Measure the Curl port beside upstream curl against surl over HTTP/1.1 and HTTPS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttpTests.cs, Documentation/Conformance/CurlPort/Http.md, Conformance/Surl.Conformance.UnitTests/CurlPortRunSetup.cs, Conformance/Surl.Conformance.UnitTests/CurlPortFindingsFile.cs, Conformance/Surl.Conformance.UnitTests/PinnedCurlPortBesideUpstreamCurl.cs, Conformance/Surl.Conformance.UnitTests/RequestBytesNormalisationTests.cs, Conformance/Surl.Conformance.UnitLibrary/CurlPortResultNormaliser.cs, Conformance/Surl.Conformance.UnitLibrary/RequestBytesNormalisation.cs, Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-580 — Measure the Curl port beside upstream curl against surl over HTTP/1.1 and HTTPS

## Goal

`Integration` tests run every HTTP/1.1 and HTTPS conversation that pinned upstream curl already runs
against a live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/Http.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror these existing classes in `Conformance/Surl.Conformance.UnitTests`, one Phase 7 test per
  upstream test, each naming the test it mirrors:
  `UpstreamCurlFetchesFromSurlOverHttp11Tests`, `UpstreamCurlFetchesFromSurlOverHttpsTests`,
  `UpstreamCurlLogsInToSurlOverHttpTests`, `UpstreamCurlTrustsSurlsHostCertificateTests` and
  `UpstreamCurlLogsInToSurlWithKerberosTests` (with `KerberosTestKdcOnLoopback`). Do not edit those classes.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose), with BL-579's relay for
  request bytes. The port binary: `CurlPortBuilds.json` (BL-577).
- If the ADR named the test class or the findings file differently, use its names and correct this task's
  `touches`.

## Acceptance criteria

- [x] The test class exists with one test per test in the five classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense: upstream's
      run is as the mirrored test asserts, and every port disagreement is listed in `Http.md`.
- [x] `Documentation/Conformance/CurlPort/Http.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Built (2026-11-05): `CurlPortBesideUpstreamCurlOverHttpTests` (57 tests with data rows, one per mirrored
  test or row; 53 run on Windows, the 4 non-Windows ones are excluded there as their mirrors are);
  `CurlPortFindingsFile` (ADR-0089 decision 5's pass rule over a findings table, and both runs written to
  the test log on a port defect); `CurlPortRunSetup` (supplementary build by SHA-256, extra environment,
  custom `surl` start, a front relay such as `DigestChallengeRelay`, per-run arguments, stored files read
  after `surl` stops, a notes filter); `PinnedCurlPortBesideUpstreamCurl.MeasureAsync` takes one.
- Result on this machine: 43 agreements, 10 port defects, 0 Surl defects; listed in
  `Documentation/Conformance/CurlPort/Http.md` (port b42c319b, upstream 0E7737…8778 and 589C8E…2648).
  The findings: no Ed25519 host key algorithm offered (1 case), SFTP channel closed before
  disconnect (1), SASL GSSAPI login fails with 67 (6), Negotiate does not fall back to NTLM inside
  SPNEGO as the unpatched 8.21.0 build does (2). Stable across three full runs.
- No Surl defect was found, so none was filed.
- Decisions, recorded as ADR-0089 amendment 1: Digest `nonce` normalised (surl's random, echoed); no
  request bytes for SigV4, Kerberos and Negotiate cases; SSH packet dumps and TLS-refusal ending notes
  left out of the notes comparison (each flipped between runs of the same client, measured).
- Sensible defaults (no ADR): each Phase 7 test is named as the test it mirrors (the Kerberos class's two
  Negotiate tests carry a `Kerberos` prefix to avoid the clash with the login class); a data row's case
  name is the method name plus `(row)` (the GSSAPI rows without `--sasl-ir` keep the bare name); the
  HTTPS cases share one certificate authority between both runs; the IMAP/POP3 GSSAPI reads use a
  message delivered once by upstream curl and copied into each run's data directory, so the stored
  trace fields (with their time stamp) are identical for both clients; the SMTP case compares each
  stored message after its trace fields, as the mirrored test does.
- Touches widened (no task in Doing names any of these): `CurlPortRunSetup.cs`, `CurlPortFindingsFile.cs`,
  `PinnedCurlPortBesideUpstreamCurl.cs` and `RequestBytesNormalisationTests.cs` in
  `Conformance/Surl.Conformance.UnitTests`; `CurlPortResultNormaliser.cs`, `RequestBytesNormalisation.cs`
  and `CLAUDE.md` in `Conformance/Surl.Conformance.UnitLibrary`; ADR-0089 for amendment 1. BL-578's helper
  could not run a supplementary build, a relay, several listeners or per-run arguments, and its
  normaliser did not cover surl's Digest nonce.
- Seen in passing: `Surl.Extended.Protocol.WebTransport.UnitTests` failed 2 tests once under the full
  parallel fast run and passed alone and in two further full runs; that project is BL-460's (in Doing).
## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Curl port runs beside pinned upstream curl in every HTTP/1.1, HTTPS, login, host-certificate and Kerberos conversation; its 10 disagreements are listed in Documentation/Conformance/CurlPort/Http.md
