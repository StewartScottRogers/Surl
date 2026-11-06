---
id: BL-681
title: Read and record the PROXY protocol header in the HTTP server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Notes record, for `--haproxy-protocol` and `--haproxy-clientip`, the curl 8.21.0 command line
      run through `Record-CurlExchange.ps1` and the PROXY header bytes it measured.
- [ ] Unit tests in `Protocols/Http/Surl.Protocol.Http.UnitTests` (for example
      `HttpUpstreamTestCaseServerTests`) show that a request preceded by a measured PROXY v1 header
      is answered with the case's reply and that the header is recorded as the first received line,
      and that a malformed PROXY line (over 107 bytes, or no CRLF) is not taken as a request.
- [ ] In the integration run above, cases 1455, 3201 and 3220 are `pass` in
      `upstream-test-case-results.win-x64.txt`, and no previously passing case now fails.
- [ ] `Surl.Protocol.Http.UnitLibrary` keeps 100% line and branch coverage, and no method exceeds
      cyclomatic complexity 10.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
