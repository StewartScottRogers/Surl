---
id: BL-477
title: Run the Curl port's coap client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-476, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-477 — Run the Curl port's coap client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl coap://` and `coaps://`: a `GET`, a
block-wise `GET` of a large file, a `PUT` under `--allow-uploads`, an observation, and a DTLS handshake refused
for a bad identity. Each assertion comes from the RFCs, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help coap` and its cases in `Curl.Conformance.Extended.UnitTests/Coap/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the exchanges above, each passing on Windows with the pinned client present (7 tests).
- [x] `Documentation/Conformance/Extended/Coap.md`: 114 rows now name tests; 17 rows name BL-1048 (no test exists yet, filed, not absorbed here); a "Live client" section maps the live tests.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- 2026-11-11 (lane 4): `PinnedCurlPortClientTalksToSurlOverCoapTests` has 7 Integration tests: GET, block-wise GET (5000 bytes), PUT with and without `--allow-uploads`, observe plus a second client PUT, coaps GET (`--self-signed`, `-k`), and coaps refused when the server has only a PSK account. The port client exits 0 on a CoAP error response, so the refused PUT is asserted from `-i` output (4.05). The port only does certificate-mode DTLS, so the "bad identity" case is a server with only a PSK account. No port defect found.
- The matrix trace left 17 rows with no unit test; filed as BL-1048 rather than widening this task. Row 5.4.4-1 may show a real conflict (Max-Age 60 sent explicitly although it is the default); BL-1048 decides.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Port coap client runs live against surl: GET, block-wise GET, PUT, observe, coaps accepted and refused
