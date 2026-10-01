---
id: BL-316
title: Accept RTSP ANNOUNCE and RECORD under --allow-uploads in Surl.Protocol.Rtsp
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-315]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests, Surl.HttpMessage.UnitLibrary, Surl.HttpMessage.UnitTests]
requirement: FR-051
created: 2026-09-30
completed: 2026-09-30
---
# BL-316 — Accept RTSP ANNOUNCE and RECORD under --allow-uploads in Surl.Protocol.Rtsp

## Goal

`RtspProtocolServer` answers `ANNOUNCE` and `RECORD` as BL-286's ADR decides - storing the announced
description and the recorded interleaved media through the content store only with
`--allow-uploads`, bounded by `--max-filesize` - and refuses them without it.

## Context

- Decisions: BL-286's ADR (what `ANNOUNCE` stores and where, what `RECORD` receives and stores, the
  answers with and without `--allow-uploads`, past `--max-filesize`); ADR-0006 section 2 (uploads off
  by default); ADR-0015 (refused or capped, the partial file deleted).
- Code: BL-315's sessions; `ContentStore`'s upload session (`ContentUploadSession`,
  `ContentUploadOpening`, `ContentUploadResult`). The body of an upload arrives with `Content-Length`
  read through `Surl.HttpMessage`, bounded before it is read.
- Fixtures: the tool's `-T` with `OPTIONS` case recorded by BL-286 (what curl does with an upload body
  on RTSP), and the libcurl `ANNOUNCE` and `RECORD` cases if pinned and recorded by then; otherwise RFC
  2326 section 14's examples.

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Rtsp.UnitTests` show an `ANNOUNCE` stored byte for byte with uploads
      allowed and refused without; a `RECORD` session's media stored as the ADR says; a body past
      `--max-filesize` refused before it is read and nothing left stored; no test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

**What was built (ADR-0074 decision 6).** `RtspRequestResponder.Uploads.cs`: `ANNOUNCE` reads its
body straight into a `ContentUploadSession` of `<path>.sdp` (opened only when the login lets the
request in, it has a body and an `rtsp://` Request-URI) and commits it; `RECORD` opens the
session's upload on the first call and resumes after `PAUSE`; `TEARDOWN` commits; every frame
the client sends is read whole, and a recording session's RTP-channel packets have their
payload (after header, CSRCs, extension and padding; `RtspInterleavedFrame.RtpPayload`)
appended. `SETUP` with `mode=record` is `403` without `--allow-uploads` and answers
`Transport: ...;mode=record`. A recording past the store's upload limit is discarded, the
session ended and the connection closed with no answer through the refusal deadline and
drain. A session ended any other way discards its recording (`RtspSession.EndAsync`); the
server ends it after the exchange without an `await` in `finally` (captured failure,
rethrown), so no compiler branch is left uncovered.

**Fixtures.** ADR-0074 amendment 1's libcurl rows (BL-333): the `ANNOUNCE` with
`Content-Length: 5`, `Content-Type: application/sdp` and `v=0\r\n`, the `SETUP` with
`RTP/AVP/TCP;unicast;interleaved=0-1;mode=record`, then `RECORD` and `TEARDOWN` carrying only
`CSeq` and `Session` (the empty-file case). Upstream libcurl sends no RTP, so the frames in
the tests are RFC 3550 packets built by hand.

**Touches widened (rule 3).** `Surl.HttpMessage.UnitLibrary` and `Surl.HttpMessage.UnitTests`
were added: the RTSP server must tell a client's `$` frame from a request head before reading,
and `HttpConnectionReader` had no way to look at the next byte without taking it. It gains
`PeekByteAsync`, additive and tested. No task in `Doing` named either project.

**Choices made where decision 6 is silent** (Decided by Claude under Stewart's delegation;
Documentation/Planning/Decisions was held by BL-287, so BL-339 folds them into ADR-0074):
- A `SETUP` naming the session cannot switch it between play and record: `455`. The mode is
  what the session's upload, or its file, was made for.
- A path the store refuses outright (`..` and the like) is `403` for `ANNOUNCE` and for a
  `SETUP` to record - decision 6's one answer for refused uploads - where `DESCRIBE` and a play
  `SETUP` keep `404`.
- A `TEARDOWN` whose commit the store refuses (a directory appeared at the path) answers `403`;
  the session is still ended and the recording gone, noted as discarded.
- An `ANNOUNCE` within `--max-filesize` but past the content store's own upload limit is `403`
  `could not be stored (TooLarge)`, the body already read so the connection stays in step.
- A session does not time out while recording (as it does not while playing); a paused one
  does, and its recording is discarded. The timeout is now checked as each request arrives,
  before it is judged, so the async discard never runs inside a synchronous check.
- Frames are told from heads only after the first head, so the first head keeps its timed
  wait; a `$` first on the connection is a malformed head, `400`, closing.

**Follow-up filed:** BL-339 (fold the choices above into ADR-0074 decision 6).

**Measured.** RTSP tests 163 -> 199, HttpMessage 189 -> 191; `Measure-CodeQuality.ps1`: both
libraries 100% line, 100% branch, 0 failing members (its 2 failures are in
`Surl.Conformance.UnitLibrary`'s `LibcurlRtspScript.ParseStep`, outside this task).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. RTSP ANNOUNCE stores <path>.sdp and SETUP mode=record/RECORD/TEARDOWN store interleaved RTP payloads under --allow-uploads, refused without, bounded by --max-filesize
