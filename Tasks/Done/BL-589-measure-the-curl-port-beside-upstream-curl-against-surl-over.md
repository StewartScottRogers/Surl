---
id: BL-589
title: Measure the Curl port beside upstream curl against surl over HTTP/2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579, BL-371]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp2Tests.cs, Documentation/Conformance/CurlPort/Http2.md, Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-589 — Measure the Curl port beside upstream curl against surl over HTTP/2

## Goal

`Integration` tests run every HTTP/2 conversation that BL-371 proved with pinned upstream curl against a live
`surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper. These are prior
knowledge, `h2c` upgrade and `h2` by ALPN. `Documentation/Conformance/CurlPort/Http2.md` lists every port
finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror the HTTP/2 conformance class BL-371 adds to `Conformance/Surl.Conformance.UnitTests` (find
  it by its `Done` task), one Phase 7 test per upstream test, each naming the test it mirrors. Do not edit it.
- The upstream side is the stunnel/static-curl 8.21.0 build that ADR-0081 and FR-054 name, since the
  reference build has no `HTTP2`. The pairing follows BL-576's ADR decision 2.
- `h2` over TLS is encrypted, and HPACK output can differ legitimately (Huffman coding, indexing choices).
  Compare what the ADR's decision 4 says is comparable for HTTP/2. Planning assumption: the decoded header
  list and frame sequence, not raw bytes.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [x] The test class exists with one test per test in BL-371's class, each `[TestCategory("Integration")]`,
      naming the mirrored test in its doc comment, and Inconclusive when either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Http2.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Plan: copied the SMB and HTTP Phase 7 classes. One case per test of `UpstreamCurlFetchesFromSurlOverHttp2Tests`
  (15), each with `[Description("Mirrors ...")]` naming it, paired with the static 8.21.0 build that class runs on
  Windows (`CurlPortRunSetup.UpstreamBuildSha256`), held to `Http2.md` by decision 5.
- Measured three times, stable: the 6 `h2c` cases agree; the 9 `https` cases are one port defect: the port
  offers only `http/1.1` by ALPN, so it never speaks HTTP/2 over TLS (`Http2.md` finding 1). No Surl defect.
- Decision (ADR-0089 amendment 2, added to this task's `touches`; no task in Doing touches it): raw `h2c`
  bytes cannot be compared, because HPACK Huffman-codes `:authority`, which holds each run's relay port. The
  test relays `h2c` cases itself and compares decoded frames (Surl.Hpack's `HpackDecoder`, reached through the
  test project's existing reference to Surl.Console), leaving out every `WINDOW_UPDATE` but the connection's
  first: the port and upstream placed the `SETTINGS` ACK differently among them, which is timing.
- `Get_TwoUrls_ReuseOneHttp2Connection` compares only the `* Reusing existing` stderr line: the
  `Established connection` line carries the client's ephemeral port, which the normaliser leaves. Filed BL-911.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. 15 HTTP/2 Phase 7 cases run beside upstream curl: h2c agrees, h2 by ALPN is one port finding in Http2.md
