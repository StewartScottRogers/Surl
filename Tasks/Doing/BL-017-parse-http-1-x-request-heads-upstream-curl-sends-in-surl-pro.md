---
id: BL-017
title: Parse HTTP/1.x request heads upstream curl sends in Surl.Protocol.Http
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-005]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-017 — Parse HTTP/1.x request heads upstream curl sends in Surl.Protocol.Http

## Goal

`Surl.Protocol.Http.UnitLibrary` reads one HTTP/1.0 or HTTP/1.1 request head from a
connection and returns the method, request target, version and header fields, or a named
parse failure. It is proven against request bytes recorded from the pinned upstream curl
8.21.0 build.

## Context

- `Surl.Protocol.Http.UnitLibrary/CLAUDE.md`: HTTP/1.0 and 1.1 first. No `Socket` or
  `HttpListener`. Expected bytes come from a pinned build via `Record-CurlExchange.ps1`,
  never from the Curl port (ADR-0003).
- The connection type comes from the listener-seam ADR recorded by BL-000, added by
  BL-005. The in-memory connection BL-005 added replays the recorded bytes, including
  split across several reads.
- Measure the request bytes with `Record-CurlExchange.ps1` (HTTP mode, the default)
  against the pinned upstream curl 8.21.0 build in `UpstreamCurlBuilds.json`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, for these cases.
  `<P>` is the recorder's `-Port`:
  1. `http://127.0.0.1:<P>/` (default GET)
  2. `-I http://127.0.0.1:<P>/file.txt` (HEAD)
  3. `-0 http://127.0.0.1:<P>/file.txt` (HTTP/1.0)
  4. `-H "X-Custom: a b" http://127.0.0.1:<P>/file.txt`
  5. `--path-as-is http://127.0.0.1:<P>/a/../b`
  6. `"http://127.0.0.1:<P>/file.txt?x=1&y=%20"`
  7. `http://127.0.0.1:<P>/one http://127.0.0.1:<P>/two` with `-Connections 2`
     (two request heads; the recorder closes each connection after its response, so
     curl sends the second on a new connection, and `request.bin` holds both heads in
     order)
- Commit each case's `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` under
  `Surl.Protocol.Http.UnitTests/Fixtures/<case-name>/`, with a `README.md` there giving
  each case's exact recorder command line and the build's SHA-256. Include the files in
  the test assembly as `EmbeddedResource`, so reading them in a fast test touches no file
  system (`.claude/rules/testing.md`).
- Header-field syntax and limits: RFC 9112, section 2 (message format) and section 5
  (field syntax). Choose a maximum head size in the `/feature` plan, state it in the
  parser's XML doc, and pin it by a test.
- Answering the request is BL-018. This task only parses.

## Acceptance criteria

- [ ] The seven fixture folders and their `README.md` exist as described, recorded from
      the pinned build, and are embedded resources of `Surl.Protocol.Http.UnitTests`.
- [ ] A request-head parser in `Surl.Protocol.Http.UnitLibrary` reads from the
      connection type and returns method, request target (raw, not decoded), version,
      and header fields in order with case-insensitive lookup. Bytes after the head stay
      unread for the caller.
- [ ] One fast test per fixture replays its `request.bin` through the in-memory
      connection, once whole and once one byte per read, and asserts the parsed fields.
      Case 7 replays both recorded heads back to back on one in-memory connection and
      parses them in sequence, the way a persistent HTTP/1.1 connection delivers them.
- [ ] Fast tests cover each named failure: malformed request line, unsupported version
      (for example `HTTP/2.0` in a request line), whitespace before a header colon
      (RFC 9112 section 5.1), head exceeding the maximum size, and connection closed
      before the head ended.
- [ ] `Surl.Protocol.Http.UnitLibrary.csproj` still references only
      `Surl.Protocol.Abstractions.UnitLibrary`, and `ProtocolIsolationTests` pass.
- [ ] `dotnet build Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `Integration` test in `Surl.Protocol.Http.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
