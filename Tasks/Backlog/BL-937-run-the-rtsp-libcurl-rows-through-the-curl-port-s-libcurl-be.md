---
id: BL-937
title: Run the RTSP libcurl rows through the Curl port's Libcurl beside upstream libcurl and record the Phase 7 libcurl findings
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-936]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/CurlPort/Libcurl.md]
requirement: FR-057
created: 2026-11-06
completed:
---
# BL-937 — Run the RTSP libcurl rows through the Curl port's Libcurl beside upstream libcurl and record the Phase 7 libcurl findings

## Goal

The `Integration` class `CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests` runs every step list of
`PinnedLibcurlTalksToSurlOverRtspTests` through upstream's `Run-LibcurlRtspScript.cs` and the port's
`Run-CurlPortLibcurlRtspScript.cs` against a fresh `surl` each and reaches a verdict, and
`Documentation/Conformance/CurlPort/Libcurl.md` lists every RTSP finding in ADR-0089 decision 6's form,
completing BL-597's Phase 7 libcurl measurement.

## Context

- Last of the five tasks BL-597 ("Drive the Curl port's managed Libcurl through the WebSocket and RTSP
  libcurl scripts beside the pinned upstream libcurl") was split into on 2026-11-06 after it timed out
  at 120 minutes. Needs BL-936 (the twin drivers, `PinnedCurlPortLibcurlBesideUpstreamLibcurl`, the
  WebSocket class and `Libcurl.md`'s head). Requirement FR-057; ADR-0089 decisions 1 (c), 4, 5 and 6;
  Windows only.
- The class is written: take it from the stash `darkfactory BL-597 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it) with
  `git checkout "stash@{N}^3" -- Conformance/Surl.Conformance.UnitTests/CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests.cs`.
  It mirrors all 13 RTSP rows; the ANNOUNCE and RECORD rows compare the files `surl` stored too.
- What the lost run measured (2026-11-06, port `b42c319b…`, Surl `6b47feae`): every RTSP row agreed
  except `PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye` (command line
  `Run-LibcurlRtspScript.cs --timeout 3000 rtsp://{surl}/clip.bin stream-uri:rtsp://{surl}/clip.bin
  transport:RTP/AVP/TCP;interleaved=0-1 SETUP PLAY RECEIVE RECEIVE RECEIVE`), judged a port defect with
  the first difference on driver report line 5, the `RECEIVE` after `PLAY`. Re-measure it; the run's
  own output (full notes) is what the finding records, not this summary.
- A finding gives (ADR-0089 decision 6): the Phase 7 test and the test it mirrors; the command line;
  upstream's and the port's result for every differing item (exit code, first differing report line,
  first differing stored byte); and the specification section or upstream libcurl documentation, with the
  version checked (8.21.0), that says what is right - for `CURLOPT_RTSP_REQUEST` with
  `CURL_RTSPREQ_RECEIVE` see https://curl.se/libcurl/c/CURLOPT_RTSP_REQUEST.html and RFC 2326 section
  10.12 (interleaved binary data). Upstream is right by ADR-0003 decision 6. Per decision 5, a port
  difference not listed in `Libcurl.md` fails the test, and so does a listed one that stops appearing.
- Stand-in: not applicable; a conformance measurement.

## Acceptance criteria

- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests"`
      on Windows reaches a verdict on all 13 rows (none Inconclusive on this machine) and passes; off
      Windows, or with a pinned file missing, each row is Inconclusive.
- [ ] `Documentation/Conformance/CurlPort/Libcurl.md` has an RTSP result and lists each RTSP finding
      (including `PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye` if it still differs) in
      ADR-0089 decision 6's form, or states that there are none, under the `## Measured` head naming the
      port commit and the upstream library's SHA-256.
- [ ] `CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests` still passes on Windows.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
