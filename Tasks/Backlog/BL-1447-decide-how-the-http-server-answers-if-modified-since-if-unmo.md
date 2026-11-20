---
id: BL-1447
title: Decide how the HTTP server answers If-Modified-Since, If-Unmodified-Since, If-None-Match and If-Match, and what ETag it sends
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1446]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed:
---
# BL-1447 — Decide how the HTTP server answers If-Modified-Since, If-Unmodified-Since, If-None-Match and If-Match, and what ETag it sends

## Goal

An ADR, "Decided by Claude under Stewart's delegation", fixes how the HTTP server evaluates
`If-Modified-Since`, `If-Unmodified-Since`, `If-None-Match`, `If-Match` and an entity-tag `If-Range`,
what `ETag` it sends and how it is made, and the exact `304` and `412` responses. It rests on BL-1446's
measurements, so upstream curl's `-z`, `--etag-compare` and `--etag-save` get a correct server.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 12; BL-1446 recorded what curl 8.21.0 sends
  and how it treats each answer.
- RFC 9110 section 13 (preconditions, evaluation order in 13.2.2, weak and strong comparison in 8.8.3),
  section 15.4.5 (`304` carries the fields a `200` would have had: `ETag`, `Last-Modified`, `Date`, no
  content) and 15.5.13 (`412`).
- Today: `Last-Modified` is sent at second precision (`HttpRequestAnswerer.cs:310`), `If-Range` matches
  only it (`:336-341`, ADR-0133). The object-storage servers already keep ETags in
  `ObjectMetadataIndex` (`Foundation/Surl.Content.UnitLibrary`, an MD5 cached on first read; BL-636). The
  ADR says whether HTTP shares those or derives its own (for example from length and modification time),
  and whether an ETag is strong or weak.
- The same file served over HTTP/1.1, HTTP/2 and HTTP/3 gets the same validators, and a write through
  any protocol changes them.
- Method scope: `GET` and `HEAD`, and whether a `PUT` under `--allow-uploads` honours `If-Match` and
  `If-None-Match: *` (a client may send them with `-H`).

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions` (the next free number), marked "Decided by Claude
      under Stewart's delegation", gives the evaluation order and every outcome as a table (status, the
      fields sent, body or none, whether the connection persists), citing RFC 9110 sections.
- [ ] It fixes the ETag: its exact form, how it is made, when it changes, and whether it comes from
      `ObjectMetadataIndex`.
- [ ] Each row cites the BL-1446 recording it rests on where curl is involved (`-z`, `-z -`,
      `--etag-compare`, `--etag-save`).
- [ ] It amends ADR-0133's entity-tag `If-Range` sentence, and says what an invalid date
      (`If-Modified-Since: nonsense`) gets.
- [ ] It names the work split as BL-1448 (date conditions) and BL-1449 (ETag conditions); if that work
      needs a `Surl.Content` change, it files that task and adds it to BL-1449's `depends-on`.
- [ ] `Documentation/Planning/Decisions/README.md` lists the ADR.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 12.
