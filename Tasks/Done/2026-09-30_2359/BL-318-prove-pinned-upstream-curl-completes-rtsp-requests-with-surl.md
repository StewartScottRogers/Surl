---
id: BL-318
title: Prove pinned upstream curl completes RTSP requests with surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-317, BL-333, BL-337]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-051
created: 2026-09-30
completed: 2026-09-30
---
# BL-318 — Prove pinned upstream curl completes RTSP requests with surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned upstream
curl 8.21.0 builds complete RTSP requests against a live `surl` in every case BL-286's ADR lists, with
the exit codes it expects, on Windows and Linux; on macOS they report Inconclusive (ADR-0026).

## Context

- The cases and expected exit codes: BL-286's ADR's list (at least `OPTIONS` on a path, extra `-H`
  fields, `-u` with `--basic` refused over `rtsp://` and accepted with `--allow-plaintext-auth`, `-u`
  with `--digest`, `-T` with `OPTIONS`, `-v`, `-i`).
- If BL-285's or BL-286's ADR pinned the reference build's `libcurl-4.dll`, the libcurl-driven cases
  (`DESCRIBE`, `SETUP`, `PLAY` with the interleaved receive, `PAUSE`, `TEARDOWN`, the parameters,
  `ANNOUNCE`, `RECORD`) are proved here too through the driver the pin task built, reporting
  Inconclusive where the library is absent.
- ADR-0026 decision 2: the check is the pin's protocol list, written once beside
  `PinnedUpstreamCurl.RunAsync`; this is the first RTSP conformance task, so add it unless an earlier
  conformance task (BL-300 or BL-311) already did.
- Harness: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`, `DigestChallengeRelay.cs`.
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case of BL-286's ADR and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] Where the platform's pin lists no `rtsp`, the tests report Inconclusive with the pin named.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.
- The libcurl cases and their expected results are ADR-0074 Amendment 1's last table (BL-333); every libcurl case sets `stream-uri:`. BL-337 builds the amended decision 5 those cases rely on.
- Delivered: `UpstreamCurlTalksToSurlOverRtspTests` (decision 11's 16 tool cases, through
  `PinnedUpstreamCurl.RunForProtocolAsync(..., "rtsp", ...)`, the pin's protocol list check BL-300
  already added, so macOS is Inconclusive with the pin named) and
  `PinnedLibcurlTalksToSurlOverRtspTests` (amendment 1's 12 libcurl cases plus one, through the
  pinned `libcurl-4.dll`; Inconclusive off Windows). `PinnedLibcurlWebSocketDriver` became
  `PinnedLibcurlDriver`, taking the script, so both drivers share it; `LibcurlRtspRequestLine`
  reads the RTSP driver's request lines back. All green on Windows (2026-09-30): 548 conformance
  tests, 531 passed, 17 skipped/inconclusive by design.
- Decided (Claude, by ADR-0074 decisions 2 and 7 as written): amendment 1's `ANNOUNCE` with uploads
  off (403) and `RECORD` on a play session (455) were listed with `-d P` and no account, but both
  are writes, and a write needs a login, judged at check 4 before the method's own checks: surl
  answers `401` there, which is the decisions' own result. So those two cases run with an account,
  `--allow-plaintext-auth` and the login in the URL (libcurl sends it as Basic), and a further case
  pins the `401` without a login. No server change: the table's set-up, not surl, disagreed with
  the decisions. ADR-0074 itself was not amended because `Documentation/Planning/Decisions` is in
  BL-327's `touches` (in Doing); the record is here.
- Decided: the `PLAY` case runs `RECEIVE` three times. surl writes the RTCP frame after the last RTP
  frame, and one `RECEIVE` returns with what one read brought (measured: the first `RECEIVE` got
  the RTP frame only), so a libcurl client loops, as libcurl's own RTSP example does; the later
  `RECEIVE`s' codes are not checked (the last waits out the 3 s timeout). `clip.bin` is 100 bytes,
  one RTP frame, because the driver shows a frame past 256 bytes only as its SHA-256.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl's tool and libcurl complete every ADR-0074 RTSP case against surl; Inconclusive where no pin lists rtsp
