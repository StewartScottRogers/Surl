---
id: BL-286
title: Decide how the RTSP server answers upstream curl
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-281, BL-285]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1, Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-286 — Decide how the RTSP server answers upstream curl

## Goal

An accepted ADR decides, from measurement of pinned upstream curl 8.21.0, every RTSP/1.0 response
`Surl.Protocol.Rtsp` sends to the requests upstream curl can make - `OPTIONS`, `DESCRIBE`,
`ANNOUNCE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`, `GET_PARAMETER`, `SET_PARAMETER`, `RECORD` and
the interleaved RTP receive - what media it describes and streams, and its challenges, so the RTSP
server and registration tasks can be built without a question.

## Context

- **What upstream curl 8.21.0 does** (tag `curl-8_21_0`, read 2026-09-30 - confirm by
  measurement): `lib/rtsp.c` sends only the request `CURLOPT_RTSP_REQUEST` names ("Since all RTSP
  requests are included here, there is no need to support custom requests like HTTP"), and
  `lib/url.c` defaults it to `OPTIONS`. The `curl` tool has no option that sets it: no
  `docs/cmdline-opts` file names RTSP, and `src/config2setopts.c` only adds `User-Agent` and
  `Referer` for RTSP. So the tool sends `OPTIONS` (with `-H` headers, `-u` credentials, `-T` a
  body); every other request and the interleaved RTP receive (`RTSPREQ_RECEIVE`,
  `CURLOPT_INTERLEAVEFUNCTION`) is libcurl's API only. curl fails a response whose `CSeq` does not
  match with `CURLE_RTSP_CSEQ_ERROR` (85) and a changed or blank `Session` with
  `CURLE_RTSP_SESSION_ERROR` (86); it refuses `SETUP` without `Transport` and requests after
  `SETUP` without a session ID.
- **The scaffold is wrong.** `Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md` says the requests come
  from "upstream curl's `--rtsp-request`": that is libcurl's `CURLOPT_RTSP_REQUEST`, not a curl
  8.21.0 command-line option. Correct it in this task.
- **libcurl.** BL-285's ADR decides whether the reference build's `libcurl-4.dll` is pinned to
  measure what the tool cannot send. RTSP needs it more than WebSocket does - all but `OPTIONS` is
  libcurl-only. Adopt BL-285's answer; if it pinned nothing, decide it again for RTSP and, if
  pinning, have `task-planner` file the pin and measurement tasks before BL-318.
- **Measure first (ADR-0003)** with the Windows reference build (`C:\Program
  Files\Git\mingw64\bin\curl.exe`, SHA-256 `0E773709...8778`); the macOS pin has no `rtsp`
  (ADR-0026), the Linux reference build has. Extend `Record-CurlExchange.ps1` where the default
  HTTP-style mode cannot answer RTSP (the status line, `CSeq` echo). At least: `curl rtsp://host/`;
  a path; `-H` extra fields; `-u` with `--basic` and `--digest` against a `401`; `-T` with
  `OPTIONS`; a mismatched `CSeq` (85); a `Session` field on the answer; `-v`, `-i`, `-A`.
- **Decide:**
  - request head reading through BL-281's library (`--max-request-head`, head timeout, ADR-0006
    NFR-013), the response head (`RTSP/1.0`, `CSeq` echoed, `Server: surl`, `Date` from
    `TimeProvider` or none, `Public` for `OPTIONS`), and every refusal's status (`400`, `404`, `405`,
    `454 Session Not Found`, `455`, `459`, `461 Unsupported Transport`, `501`, `505`);
  - what `DESCRIBE` answers (e.g. an SDP file from the content store, or SDP surl writes for a
    served media file) and what `PLAY` streams over interleaved RTP (`$`, channel, length, per RFC
    2326 section 10.12) - and what a UDP `Transport` gets, since curl's receive reads only
    interleaved data;
  - sessions: ID generation (random, bounded count), timeout from `TimeProvider`, `TEARDOWN`;
  - `ANNOUNCE` and `RECORD` as uploads under `--allow-uploads` and `--max-filesize` (ADR-0006,
    ADR-0015), `GET_PARAMETER` and `SET_PARAMETER` bodies;
  - authentication: HTTP's `Basic` and `Digest` challenges through `IHttpAuthenticationSession`
    (ADR-0032 section 6), `Basic` over `rtsp://` (no TLS scheme exists) refused without
    `--allow-plaintext-auth`, anonymous reads as HTTP has them (ADR-0032 decision 2);
  - ADR-0006's RTSP rows (idle timeout, maximum duration, connection limits) and what each sends;
  - the verbose and trace notes (ADR-0033), the help category (curl 8.21.0's help categories -
    ADR-0034 decision 1 - have none for RTSP) and the `--aihelp` topic.
- The command lines (and libcurl cases, if pinned) BL-318's conformance tests must prove on Windows
  and Linux, with the expected exit code for each; macOS reports Inconclusive per ADR-0026.
- Inputs: RFC 2326, RFC 4566 (SDP), RFC 3550 (RTP), ADR-0006, ADR-0019, ADR-0026, ADR-0032,
  ADR-0033, ADR-0034, ADR-0046, and BL-281's ADR.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude under
      Stewart's delegation", records each measurement (build path, SHA-256, arguments, date,
      transcript excerpt) and decides every point in Context.
- [ ] It lists the curl 8.21.0 cases the RTSP conformance task must prove, with the expected exit
      code for each.
- [ ] `Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md` no longer names `--rtsp-request` as a curl option.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
