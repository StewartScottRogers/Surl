---
id: BL-590
title: Measure the Curl port beside upstream curl against surl over HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579, BL-385]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp3Tests.cs, Documentation/Conformance/CurlPort/Http3.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-590 — Measure the Curl port beside upstream curl against surl over HTTP/3

## Goal

`Integration` tests run every HTTP/3 conversation that BL-385 proved with pinned upstream curl against a live
`surl --http3`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper. These are
`--http3-only`, `--http3` and Alt-Svc. `Documentation/Conformance/CurlPort/Http3.md` lists every port finding
in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror the HTTP/3 conformance class BL-385 adds to `Conformance/Surl.Conformance.UnitTests` (find
  it by its `Done` task), one Phase 7 test per upstream test, each naming the test it mirrors. Do not edit it.
- The upstream side is the stunnel/static-curl 8.21.0 build that ADR-0082 and FR-055 name. The pairing
  follows BL-576's ADR decision 2.
- QUIC is UDP and encrypted, so BL-579's TCP relay does not apply. Compare what the ADR's decision 4 says is
  comparable for HTTP/3. Planning assumption: exit code, output, and the QUIC Initial ClientHello as
  `Record-CurlExchange.ps1`'s QUIC Initial mode (BL-363) decodes it for upstream curl. That mode refuses any
  binary but pinned upstream curl, so the port's Initial is decoded by the same code in the test, not by the
  script.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [x] The test class exists with one test per test in BL-385's class, each `[TestCategory("Integration")]`,
      naming the mirrored test in its doc comment, and Inconclusive when either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Http3.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Dropped the plan to decode and compare the QUIC Initial ClientHello. ADR-0089 decision 4 already leaves
  request bytes over UDP and inside TLS uncompared, because the ClientHello comes from the TLS library, not
  from curl. No new ADR was needed. These carry the comparison instead: exit code, standard output, the
  stderr lines the mirrored test pins, the stored upload, and surl's `-v` notes (without TLS endings, per
  Amendment 1).
- Pairing: on Windows every case runs the static 8.21.0 build (SHA-256 `589C8E4D…`) through
  `CurlPortRunSetup.UpstreamBuildSha256`, as the mirrored test does. Elsewhere it runs the build paired by protocol.
- Measured 2026-11-05: 13 cases, all 13 port defects, in two findings listed in `Http3.md`:
  1. The port build has no `HTTP3` feature and exits 2 on `--http3` and `--http3-only`.
  2. It has no `HTTP2` feature and speaks HTTP/1.1 over HTTPS, where upstream negotiates h2.
- Upstream's run met every mirrored assertion, so there is no Surl defect and nothing was filed.
- The pinned port reports curl 8.22.0. Neither finding is a release difference: both are missing build features.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Phase 7 HTTP/3: 13 Integration cases run the Curl port beside upstream curl against surl --http3; Http3.md lists 2 port findings (no HTTP/3, no HTTP/2)
