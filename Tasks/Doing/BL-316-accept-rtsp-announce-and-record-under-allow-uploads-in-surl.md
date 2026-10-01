---
id: BL-316
title: Accept RTSP ANNOUNCE and RECORD under --allow-uploads in Surl.Protocol.Rtsp
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-315]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests]
requirement: FR-051
created: 2026-09-30
completed:
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

- [ ] Tests in `Surl.Protocol.Rtsp.UnitTests` show an `ANNOUNCE` stored byte for byte with uploads
      allowed and refused without; a `RECORD` session's media stored as the ADR says; a body past
      `--max-filesize` refused before it is read and nothing left stored; no test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
