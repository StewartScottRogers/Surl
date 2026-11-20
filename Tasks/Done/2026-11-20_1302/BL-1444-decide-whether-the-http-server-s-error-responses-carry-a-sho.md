---
id: BL-1444
title: Decide whether the HTTP server's error responses carry a short text body for curl --fail-with-body
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1444 — Decide whether the HTTP server's error responses carry a short text body for curl --fail-with-body

## Goal

An ADR, "Decided by Claude under Stewart's delegation", settles whether the HTTP server's 4xx and 5xx
responses keep their empty bodies or carry a short fixed text body that `curl --fail-with-body` and
`curl -i` can show. If it decides on a body, it files the tasks that build it.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 7, Low (Surl `763754785`, unchanged
  since): `curl -i .../nope.txt` gives `404` with `Content-Length: 0`, so `--fail-with-body` keeps
  nothing.
- ADR-0008's table answers every refusal with an "empty body", and its Consequences say: "Error bodies
  are empty; a later task may add a short fixed text body, measured against curl first." ADR-0019
  section 1 repeats "Every error response has an empty body". So today's behaviour is decided, with
  this question left open.
- Upstream curl 8.21.0's `--manual` on `--fail-with-body`: "In normal cases when an HTTP server fails to
  deliver a document, it returns an HTML document stating so (which often also describes why and more).
  This option allows curl to output and save that content but also to return error 22."
- What a body would touch: every refusal written through `HttpAnswer` in
  `Protocols/Http/Surl.Protocol.Http.UnitLibrary` (and the proxy, RTSP and extended servers that share
  `Surl.HttpServing`), the recorded fixtures under
  `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures`, upstream test cases that pin
  `Content-Length: 0`, `HEAD` (never a body), `304`, and refusals that close the connection.
- A stand-in may also want to choose the body. `--fault error;reply=...` already chooses the status
  (ADR-0124 decision 4); the ADR says whether a body belongs there too.

## Acceptance criteria

- [x] An ADR in `Documentation/Planning/Decisions` (the next free number), marked "Decided by Claude
      under Stewart's delegation", decides empty or a fixed body for each status family, with the exact
      bytes and `Content-Type` if a body.
- [x] It records a measurement with `Record-CurlExchange.ps1` and the pinned upstream curl 8.21.0
      (`-sS --fail-with-body` and `-i` against a 404 with a body and with none), giving exit codes and
      stdout.
- [x] It lists the upstream test cases and fixtures a body would change, and says why each stays right.
- [x] If it decides on a body, the implementation tasks are filed with `task-board.ps1 new` (one per
      server, depending on this task) and named in the ADR; if it keeps empty bodies, ADR-0008's
      Consequences line is updated to say the question is closed.
- [x] `Documentation/Planning/Decisions/README.md` lists the ADR.

## Notes

- Decided: a fixed `<code> <reason>
` text/plain body for the HTTP server's own 4xx/5xx, none on HEAD/1xx/204/304, extended/proxy servers left empty (ADR-0186). Filed BL-1467 (refusals) and BL-1468 (fault `body=`).

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 7.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. ADR-0186 decides a fixed text body for HTTP 4xx/5xx, measured with curl 8.21.0; BL-1467 and BL-1468 filed
