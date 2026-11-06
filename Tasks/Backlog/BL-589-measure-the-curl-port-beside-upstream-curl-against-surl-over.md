---
id: BL-589
title: Measure the Curl port beside upstream curl against surl over HTTP/2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579, BL-371]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp2Tests.cs, Documentation/Conformance/CurlPort/Http2.md]
requirement: none
created: 2026-11-04
completed:
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

- [ ] The test class exists with one test per test in BL-371's class, each `[TestCategory("Integration")]`,
      naming the mirrored test in its doc comment, and Inconclusive when either pinned binary is absent.
- [ ] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [ ] `Documentation/Conformance/CurlPort/Http2.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [ ] Any Surl defect found is filed as a task and named in Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
