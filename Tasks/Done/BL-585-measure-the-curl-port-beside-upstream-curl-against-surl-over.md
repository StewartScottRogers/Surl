---
id: BL-585
title: Measure the Curl port beside upstream curl against surl over WebSocket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverWebSocketTests.cs, Documentation/Conformance/CurlPort/WebSocket.md, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests/WebSocketFrameUnmaskingTests.cs, Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-585 — Measure the Curl port beside upstream curl against surl over WebSocket

## Goal

`Integration` tests run every `ws` and `wss` conversation that the pinned upstream curl tool already runs
against a live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/WebSocket.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror `UpstreamCurlTalksToSurlOverWebSocketTests` in `Conformance/Surl.Conformance.UnitTests`,
  one Phase 7 test per upstream test, each naming the test it mirrors. Do not edit it. These are the tool
  rows of ADR-0071 decision 11.
- The libcurl rows (`PinnedLibcurlTalksToSurlOverWebSocketTests`, ADR-0071 Amendment 1) are measured here
  only if BL-576's ADR decision 1 says how. The port's `Libcurl` is a managed API, not a native
  `libcurl-4.dll`. If the ADR filed a separate task for a libcurl driver, leave those rows to it.
- Client frames are masked with a random key. Compare them as the ADR's decision 4 normalises them.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [x] The test class exists with one test per test in `UpstreamCurlTalksToSurlOverWebSocketTests`, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/WebSocket.md` names the port commit and upstream build measured,
      lists each finding or states that there are none, and says where the libcurl rows are measured.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Plan: one Phase 7 test per test of `UpstreamCurlTalksToSurlOverWebSocketTests` (20), on BL-578's
  `PinnedCurlPortBesideUpstreamCurl`, in the FTP class's (BL-581) form. Refusal cases compare the
  `Refused WebSocket upgrade:` stderr line the mirrored test pins.
- First run: the 6 `wss` cases agreed, and all 14 `ws` cases differed only inside the
  `Sec-WebSocket-Key` value, which is a fresh nonce in every run (RFC 6455 4.1). ADR-0089 decision 4
  had no rule for it nor for the client's per-frame masking keys (RFC 6455 5.3). Decision recorded as
  **ADR-0089 Amendment 2** (Claude under Stewart's delegation): `Sec-WebSocket-Key:` is replaced by
  `<websocket-key>`, and every client frame after a connection's last upgrade request is unmasked
  with its key zeroed, before any other rule. Built in `Surl.Conformance.UnitLibrary`
  (`WebSocketFrameUnmasking`, `RequestBytesNormalisation`, `CurlPortResultNormaliser.WebSocketKeyPlaceholder`),
  unit tested by `WebSocketFrameUnmaskingTests` (100% line and branch on the new class).
- Touches widened to `Conformance/Surl.Conformance.UnitLibrary`, `WebSocketFrameUnmaskingTests.cs` and
  ADR-0089 for that rule; no task in Doing names any of them (checked 2026-11-05).
- Chosen default: turning the relay off for `ws` was rejected, because it would compare none of curl's
  upgrade request or frames.
- `File_WsWithOutput_...`: both runs write the same `-o` path in turn; an arrangement reads each run's
  file before `surl` stops, the mirrored assertion is applied to upstream's file, and the test also
  asserts the port's file equals upstream's.
- Echo cases run `surl` with `--directory` over an empty folder where the mirrored test starts it in
  memory (the helper always serves a folder); `--ws-echo` serves no file, so this changes nothing seen.
- Result on Windows (twice): 20 of 20 agree. No port defect, no Surl defect, so no Surl task filed.
  `WebSocket.md` names the port commit, upstream build and Surl commit, and leaves the libcurl rows to
  BL-597 (ADR-0089 decision 1 (c)).
- `dotnet build -warnaserror` clean; fast tests 20119 passed, 0 failed, 3 skipped.


## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Curl port is measured beside upstream curl over ws and wss: 20 of 20 cases agree, under ADR-0089 Amendment 2's WebSocket key and mask rule
