---
id: BL-681
title: Read and record the PROXY protocol header in the HTTP server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Conformance/upstream-test-cases-passing.win-x64.txt, Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-681 — Read and record the PROXY protocol header in the HTTP server half

## Goal

When curl sends a PROXY protocol v1 header before its request (`--haproxy-protocol`,
`--haproxy-clientip`), the HTTP upstream-test-case server half reads that line, records it as
the first line of the received bytes, and then reads the HTTP request as usual, so upstream test
cases 1455, 3201 and 3220 pass.

## Context

- Governing decision: ADR-0080 (`Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md`),
  decision 2: the server half is Surl's own HTTP server reading every byte curl sends, and records
  what it received for `<protocol>`. Surl is written from FILEFORMAT.md, the PROXY protocol
  specification (HAProxy's `proxy-protocol.txt`, version 1 text header:
  `PROXY TCP4|TCP6|UNKNOWN <src> <dst> <sport> <dport>\r\n`, at most 107 bytes) and measurement,
  never by translating upstream's test servers (`sws.c`). Upstream curl 8.21.0 is the only oracle.
- Symptom (Windows, 2026-11-04 run of BL-392's `UpstreamTestCasesOverHttpTests`): in cases 1455,
  3201 and 3220 the `<protocol>` section expects the HTTP request line on line 2, and the server
  half recorded nothing, because line 1 is the PROXY header and the head reader rejects it.
- Code: `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpUpstreamTestCaseServer.cs` and the head
  reading it uses. Whether the PROXY header is accepted only in the upstream-test-case server half
  or in the shipped HTTP server too is part of this task; keep the shipped `surl` unchanged unless
  an ADR decides otherwise (ADR-0080: `Surl.Console` never composes the seam).
- First step: read cases `test1455`, `test3201`, `test3220` in
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/` to confirm they use
  `--haproxy-protocol` or `--haproxy-clientip`, then measure the exact bytes the pinned reference
  build sends with `Record-CurlExchange.ps1` for each option (IPv4 loopback, and the
  `--haproxy-clientip` value case 3201 or 3220 uses). Record the command lines and the measured
  header bytes in Notes.
- Integration run: `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCasesOverHttp" --logger "console;verbosity=detailed"`.

## Acceptance criteria

- [x] Notes record, for `--haproxy-protocol` and `--haproxy-clientip`, the curl 8.21.0 command line
      run through `Record-CurlExchange.ps1` and the PROXY header bytes it measured.
- [x] Unit tests in `Protocols/Http/Surl.Protocol.Http.UnitTests` (for example
      `HttpUpstreamTestCaseServerTests`) show that a request preceded by a measured PROXY v1 header
      is answered with the case's reply and that the header is recorded as the first received line,
      and that a malformed PROXY line (over 107 bytes, or no CRLF) is not taken as a request.
- [x] In the integration run above, cases 1455, 3201 and 3220 are `pass` in
      `upstream-test-case-results.win-x64.txt`, and no previously passing case now fails.
- [x] `Surl.Protocol.Http.UnitLibrary` keeps 100% line and branch coverage, and no method exceeds
      cyclomatic complexity 10.
- [x] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Cases confirmed: 1455 uses `--haproxy-protocol`, 3201 `--haproxy-clientip "192.168.1.1"`,
  3220 `--haproxy-clientip "2a04:4e42::347"`; each `<strippart>` rewrites the PROXY line to
  `proxy-line` before comparing `<protocol>`.
- Measured 2026-11-06 with the win-x64 reference build (Git for Windows curl 8.21.0, SHA-256
  `0E773709...8778`) through `Record-CurlExchange.ps1 -Port 47681 -CurlArgs <args> -OutDirectory <dir>`:
  - `-s http://127.0.0.1:47681/1455 --haproxy-protocol -H "Testno: 1455"` sent
    `PROXY TCP4 127.0.0.1 127.0.0.1 53211 47681\r\n` then the GET head; exit 0.
  - `-s http://127.0.0.1:47681/3201 --haproxy-clientip 192.168.1.1 -H "Testno: 3201"` sent
    `PROXY TCP4 192.168.1.1 192.168.1.1 53212 47681\r\n` (the client IP is both source and
    destination); exit 0.
  - `-s http://127.0.0.1:47681/3220 --haproxy-clientip 2a04:4e42::347 -H "Testno: 3220"` sent
    `PROXY TCP6 2a04:4e42::347 2a04:4e42::347 53213 47681\r\n`; exit 0.
  The three headers are pinned in `HttpUpstreamTestCaseServerTests.ProxyHeaderBeforeTheRequest_IsRecordedAsTheFirstLineAndTheRequestAnswered`.
- Decision (sensible default, no ADR needed since the task's Context already sets it): the PROXY
  header is read only by the upstream-test-case server half, by a new internal
  `HttpUpstreamTestCaseProxyHeader`, before the first request; the shipped `HttpProtocolServer` and
  `surl` are unchanged (ADR-0080: `Surl.Console` never composes the seam). Accepting PROXY in the
  shipped server would widen what a peer may do and would need its own option and ADR.
- How it reads: at most 107 bytes, only while they could still be the start of `PROXY ` or an
  unfinished header, so an ordinary request is never read further than one read; bytes read past
  the header (or every byte, when there is none) are handed back through `AlreadyReadBytesConnection`.
  Every byte goes through `ReceivedBytesRecordingConnection`, so the header is the first recorded
  line. A header with no CRLF within 107 bytes (too long, a bare LF, or the client closed first)
  ends the connection with no bytes and a log note. The `PROXY` fields themselves are not checked:
  the case's `<strippart>` and `<protocol>` judge them.
- Choice: the wait for the first bytes is bounded by the exchange's cancellation (the idle timeout)
  rather than the head timeout, which `HttpConnectionReader` applies only to the head read after
  it. Only the test seam is affected, and a silent client still ends at the idle timeout.
- Integration run (2026-11-06, `dotnet test Conformance/Surl.Conformance.UnitTests --filter
  "FullyQualifiedName~UpstreamTestCasesAgainstSurl"`; the task's `~UpstreamTestCasesOverHttp`
  filter matches no test today): ran 1124, pass 1063, fail 61, skip 889 - 1455, 3201 and 3220
  newly pass, no regression against the passing list. Added them to
  `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` and updated the win-x64 row of
  `Upstream-Test-Case-Pass-Rate.md`; both added to `touches` (no task in Doing names them).
- Quality: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` shows 100% line and
  branch; the new members pass (the read loop was split out of `ReadPastAsync` after the script
  counted its async state machine at 12). Two members this task did not touch,
  `HttpRequestAnswerer.FileAnswer` (16) and `HttpRequestAnswerer.StoreUploadAsync` (12), fail the
  script's count though the build's `CA1502` passes them; filed as BL-917.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The HTTP upstream-test-case server half reads past and records a PROXY v1 header; cases 1455, 3201 and 3220 pass
