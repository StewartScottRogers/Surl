---
id: BL-1421
title: Add an HTTP/1.x head redactor for Authorization and Proxy-Authorization to Surl.HttpMessage
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1418]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1421 — Add an HTTP/1.x head redactor for Authorization and Proxy-Authorization to Surl.HttpMessage

## Goal

`Surl.HttpMessage` has one HTTP/1.x request-head redactor, built on BL-1418's line redactor. It
rewrites the value of `Authorization`, `Proxy-Authorization` and any other field BL-1417's ADR names,
keeping the auth scheme word (`Basic`, `Bearer`), so every server that reads HTTP/1.x heads can use
it.

## Context

- Bug report item: `-v` showed `Authorization: Basic ...` and `Bearer ...` values.
- `Protocols/Http/Surl.HttpMessage.UnitLibrary` is shared by the http, proxy, rtsp and ws servers and
  by the extended HTTP servers through `Surl.HttpServing`, so the redactor lives here once. BL-1422,
  BL-1425, BL-1426 and BL-1427 use it; the ADR files the extended servers' tasks.
- Field names are case-insensitive (RFC 9110 section 5.1). The redactor must leave a request body
  alone: it stops at the blank line that ends the head and passes body bytes through. A chunked or
  `Content-Length` body is followed by the next request's head on a persistent connection.

## Acceptance criteria

- [x] A public redactor type (the ADR's name) in `Protocols/Http/Surl.HttpMessage.UnitLibrary`, with
      XML doc comments.
- [x] Fast tests in `Protocols/Http/Surl.HttpMessage.UnitTests` show `Authorization: Basic
      YWxpY2U6czNjcmV0` and `authorization: Bearer abc` logged in the ADR's form, and
      `Proxy-Authorization` the same way.
- [x] A body that contains `Authorization: x` is passed through unchanged (`Content-Length` and chunked
      cases), and the second request's head on the same connection is redacted.
- [x] A head split across reads at any byte is redacted the same (a test over every split point of one
      sample head).
- [x] `dotnet build Protocols/Http/Surl.HttpMessage.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

- Decided (within ADR-0182): `HttpRequestHeadBytesRedactor` takes optional extra secret field names (for BL-1465 API-key headers). A scheme-less Authorization value is masked whole; a folded continuation of a secret field is masked; unreadable framing is followed by another head so nothing stays unredacted. Body framing reuses `HttpRequestBodyFraming.Of`.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. HttpRequestHeadBytesRedactor masks Authorization and Proxy-Authorization, passes bodies through, handles any read split
