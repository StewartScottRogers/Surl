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
completed: 2026-09-28
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

- [x] The seven fixture folders and their `README.md` exist as described, recorded from
      the pinned build, and are embedded resources of `Surl.Protocol.Http.UnitTests`.
- [x] A request-head parser in `Surl.Protocol.Http.UnitLibrary` reads from the
      connection type and returns method, request target (raw, not decoded), version,
      and header fields in order with case-insensitive lookup. Bytes after the head stay
      unread for the caller.
- [x] One fast test per fixture replays its `request.bin` through the in-memory
      connection, once whole and once one byte per read, and asserts the parsed fields.
      Case 7 replays both recorded heads back to back on one in-memory connection and
      parses them in sequence, the way a persistent HTTP/1.1 connection delivers them.
- [x] Fast tests cover each named failure: malformed request line, unsupported version
      (for example `HTTP/2.0` in a request line), whitespace before a header colon
      (RFC 9112 section 5.1), head exceeding the maximum size, and connection closed
      before the head ended.
- [x] `Surl.Protocol.Http.UnitLibrary.csproj` still references only
      `Surl.Protocol.Abstractions.UnitLibrary`, and `ProtocolIsolationTests` pass.
- [x] `dotnet build Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `Integration` test in `Surl.Protocol.Http.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Http.UnitLibrary`.

## Notes

- **Plan (run by the lane session itself).** The `/feature` stages were compressed for
  this unattended run: plan, fixtures, implementation and tests were done in-session,
  then the `code-reviewer` agent reviewed the diff. The tests were written after the
  code, not first; every fixture test and failure test was checked against the
  implementation and `Measure-CodeQuality.ps1` reports 100% line and branch coverage.
- **Fixtures.** Recorded 2026-09-28 with `Record-CurlExchange.ps1 -Port 18017` against
  the pinned win-x64 curl 8.21.0 (the script refuses any other binary). Folder names:
  `default-get`, `head`, `http10`, `custom-header`, `path-as-is`, `query-string`,
  `two-urls`. All seven exited 0. They are embedded with the logical name
  `Fixtures/<case>/<file>`. `Fixtures/.gitattributes` sets `-text`, because the root
  `* text=auto` would otherwise let git rewrite the recorded CRLFs to LF on a Linux or
  macOS checkout and break the tests there.
- **Design.** `HttpConnectionReader` wraps an `IConnection` and buffers reads.
  `ReadRequestHeadAsync` returns an `HttpRequestHeadReadResult`: an `HttpRequestHead`
  (method, raw request target, `Version`, fields in order, case-insensitive
  `GetFieldValues`) or an `HttpRequestHeadReadOutcome` failure. Bytes read past the
  head stay buffered. The next `ReadRequestHeadAsync` or `ReadAsync` returns them
  first, which is how a persistent connection's next head or a body is read.
- **Default taken: the maximum head size is 307,200 bytes (300 KiB).** That is the same
  limit upstream curl puts on a response head it accepts. It counts every byte from
  the first byte of the head, including skipped empty lines, through the final LF.
  `ReadRequestHeadAsync_HeadOfExactlyTheMaximum_ReadsIt` and
  `..._HeadOneByteOverTheMaximum_ReturnsHeadTooLarge` pin it. A line with no LF fails
  as soon as the limit is reached; the reader does not wait for more bytes.
- **Defaults taken on RFC 9112 latitude.** A bare LF ends a line (section 2.2 MAY).
  Empty lines before the request line are skipped (section 2.2 SHOULD). An obsolete
  line folding, including whitespace before the first field, is
  `MalformedHeaderField`; section 5.2 lets a server reject it with 400. Whitespace
  before a colon is `WhitespaceBeforeColon` (section 5.1 MUST reject). A well-formed
  `HTTP/x.y` whose major version is not 1 (`HTTP/2.0`, `HTTP/0.9`) is
  `UnsupportedVersion`. A higher 1.x such as `HTTP/1.2` is read as 1.1, per RFC 9110
  section 2.5 (SHOULD). `ConnectionClosed` is a close before any byte, or after only
  empty lines, and is the normal end of a persistent connection. A close part way
  through a head is `ConnectionClosedBeforeHeadEnded`. Which status code answers each
  outcome is BL-018's to decide.
- **Review (`code-reviewer`).** Nothing under Must fix. Should-fix items done:
  - `HTTP/1.x` read as 1.1.
  - The XML doc says the reader is unusable after any outcome other than `HeadRead`,
    or after an exception.
  - Per-class test classes added: `HttpRequestLineParserTests`,
    `HttpFieldLineParserTests`, `HttpSyntaxTests` and `HttpRequestHeadLineReaderTests`.

  Considerations taken: a stray CRLF followed by a close is `ConnectionClosed`, and
  `Fields` is returned read-only.

  Considerations left:
  - The buffer does not shrink after a large head. It is at most 300 KiB per
    connection.
  - Host-field validation (RFC 9112 section 3.2: 400 for an HTTP/1.1 request with no
    `Host` or more than one) belongs to answering the request, BL-018.
  - Glossary entries are filed as BL-058, because `Documentation` is outside this
    task's `touches`.
- **Result.** 122 fast tests in `Surl.Protocol.Http.UnitTests`, and 426 fast tests
  across the solution, all green. `Measure-CodeQuality.ps1 -Library
  Surl.Protocol.Http.UnitLibrary` reports 100% line, 100% branch, 32 members, 0
  failing, worst CRAP 10. `dotnet build -warnaserror` and `dotnet format
  --verify-no-changes` are clean.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Protocol.Http reads HTTP/1.x request heads from a connection, proven against seven recorded upstream curl 8.21.0 heads
