---
id: BL-391
title: Play the server half of upstream http and https test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-388]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-391 — Play the server half of upstream http and https test cases

## Goal

Surl plays the server half of upstream test cases whose `<server>` is `http` or `https`, as BL-386's
ADR decision 2 defines it. It answers each request with the case's `<reply>` (`<data>`, `<dataNUM>`,
`<connect>`) and the `<servercmd>` instructions that the ADR keeps, and it records the bytes it
received for `<protocol>`.

## Context

- Decision: BL-386's ADR, decision 2. It fixes the meaning, and where the code lives. `touches`
  assumes `Surl.Conformance` and `Surl.Protocol.Http`. If the ADR puts the code somewhere else, work
  there and say so in Notes.
- How upstream's HTTP server chooses a reply: FILEFORMAT.md (tag `curl-8_21_0`) `<data>`,
  `<dataNUM>` ("the server picks this part when the request path ends in a NUM"), `<connect>`, and the
  HTTP `<servercmd>` list (lines 397 to 414). The overview's "Constraints" say that Surl is written from
  documentation and measurement, not by translating upstream's test servers. Read FILEFORMAT.md and
  the protocol RFCs, not `tests/server/sws.c`.
- The record of received bytes is what BL-390's `<protocol>` check compares. Its shape is the one
  BL-390 consumes.
- No network in unit tests: the server half is driven through `InMemoryConnection`.

## Acceptance criteria

- [x] Unit tests drive the server half with inline cases: a single `<data>` reply, a `<dataNUM>`
      chosen by the request path's number, `swsclose`, `idle`, `writedelay` with a fake
      `TimeProvider`, `skip`, and `auth_required`. The tests check the bytes sent and the bytes
      recorded.
- [x] Each `<servercmd>` the ADR excludes produces the named skip reason, not a silent pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

- Where the code lives, as ADR-0080 decision 2 says: `Surl.Protocol.Http` holds the seam
  (`HttpUpstreamTestCaseServer`, `HttpUpstreamTestCaseReplies`, `HttpUpstreamTestCasePartNumber`,
  `HttpUpstreamTestCaseServerCommands`, `HttpUpstreamTestCaseRequestBody`,
  `ReceivedBytesRecordingConnection`); `Surl.Conformance` holds `UpstreamTestCaseServerCommands`,
  the per-server table `ForBuild` now uses. No contract outside `touches` changed. The seam is a
  separate `IConnectionProtocolServer` rather than an `IHttpRequestHandler` behind
  `Http1ConnectionServer`, because that loop writes its own status line before any takeover and
  answers 400 for heads FILEFORMAT's server answers; heads are still read by `Surl.HttpMessage`'s
  reader. `ReceivedBytes` is `byte[]`, the shape `UpstreamTestCaseRunRecord.ServerReceived` takes.
- Sources: FILEFORMAT.md at tag `curl-8_21_0` (`<data>`, `<dataNUM>`, `<connect>`, `swsbounce`,
  `swsclose`, the HTTP `<servercmd>` list), read 2026-11-04, and the imported corpus. Not sws.c.
- Choices FILEFORMAT leaves open, decided by Claude under Stewart's delegation (implementation
  detail inside ADR-0080 decision 2, which gives the protocol library FILEFORMAT's rules to
  interpret; recorded here and in the library's CLAUDE.md and XML docs rather than a new ADR):
  - The test number is the run of digits the path ends in (query and fragment cut, absolute-form
    scheme and authority cut); for `CONNECT`, the run the host ends in. The corpus' request lines
    are `/N`, `/we/want/N`, `http://host/path/N` and `CONNECT test.remote.example.com.N:port`.
  - Credentials are the `Authorization` field's, else `Proxy-Authorization`'s; schemes match
    case-insensitively; the NTLM type is read from the base64 message's offset 8.
  - `swsbounce` state and the Negotiate count belong to the case (one `HttpUpstreamTestCaseReplies`
    per case), not a connection: FILEFORMAT says "a subsequent request made by the same test".
  - No automatic `100 Continue`: test 1129 (`skip: 1053700`, `--expect100-timeout 99`) expects the
    final reply with no 100 in `<stdout>`, and the cases that want one carry it in `<data>`. The
    body is read as framed; curl sends it when its expect timeout runs out.
  - `skip: N` reads `Content-Length - N` bytes (at least 0) of a `POST`/`PUT` body, as test 1129
    needs; it does not apply to chunked bodies. `auth_required` leaves a `POST`/`PUT` body without
    `Authorization` unread; `no-expect` leaves any body with `Expect` unread.
  - `writedelay` packets are the reply's lines (each up to and including its LF), the pause on the
    exchange's `TimeProvider` between them.
  - `idle` reads the request, sends nothing and waits for the client to close (test 2101 has no
    `<data>`). A part the case lacks closes the connection with no bytes and a log note.
  - Implemented: `Testnum`, `auth_required`, `idle`, `no-expect`, `skip:`, `swsclose`,
    `writedelay:`. Skipped `servercmd-unsupported:<word>`: `upgrade` (29 http cases),
    `connection-monitor` (11), `delay:`, `stream`, `rtp:` and anything else; a `<postcmd>` skips
    `servercmd-unsupported:postcmd` (6 http cases), since no server half implements one.
- Coverage (Measure-CodeQuality, -SkipTestRun over the two test projects' Cobertura):
  `Surl.Protocol.Http.UnitLibrary` 100% line, 100% branch, 0 failing members.
  `Surl.Conformance.UnitLibrary` 100% line, 100% branch; its 8 members over 10 in Cobertura's
  complexity (`UpstreamTestCaseStripLine`, `UpstreamTestCaseStripCondition`,
  `UpstreamTestCaseVerifyChecker`) predate this task and pass CA1502 at build time.
- FILEFORMAT.md was fetched for reading with the machine's WinGet curl (a document download, not a
  run against Surl); the oracle rule is about test clients, but WebFetch would have been the
  cleaner tool.
- Follow-up stays with the planned tasks: BL-392 composes this server on the loopback listener
  (and over TLS for `https`) and runs the cases.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Protocol.Http plays the server half of upstream http and https test cases (data, dataNUM, connect, swsbounce, swsclose, idle, writedelay, skip, auth_required, no-expect) and records the bytes received; unimplemented server commands skip by name
