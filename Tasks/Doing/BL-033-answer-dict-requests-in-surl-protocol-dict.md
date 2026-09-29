---
id: BL-033
title: Answer DICT requests in Surl.Protocol.Dict
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-029]
touches: [Surl.Protocol.Dict.UnitLibrary, Surl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-033 — Answer DICT requests in Surl.Protocol.Dict

## Goal

`Surl.Protocol.Dict.UnitLibrary` contains a DICT protocol server for the `dict` scheme.
It answers every command the pinned upstream curl 8.21.0 build sends for its DICT URL
forms, proven by byte scripts recorded from that build.

## Context

- DICT is RFC 2229. The URL forms upstream curl supports (`d:`/`define:`,
  `m:`/`match:`/`find:`, and a bare path) and the commands it sends for each are
  measured with `Record-CurlExchange.ps1 -Raw` (BL-029) against the pinned build, never
  assumed.
- `Surl.Protocol.Dict.UnitLibrary/CLAUDE.md` holds the project rules. The server
  implements the protocol-server interface from the listener-seam ADR (BL-000, types
  added by BL-005), and is driven in tests by the in-memory connection BL-005 added.
  It references only Abstractions and ADR-0002's horizontal libraries.
- Decide in the `/feature` plan, and state in the server's XML doc: where definitions
  come from (for example a file under the served directory through `Surl.Content`, or a
  built-in response), the banner text, and the reply to a word with no definition
  (RFC 2229 status 552).
- Command-line length limit: follow the hardening ADR (BL-024) if it exists when this
  task runs. If not, choose a limit, document it, and file a follow-up to align it with
  BL-024.
- Fixtures: commit each recording's `request.bin`, `transcript.txt`, `stdout.bin`,
  `stderr.txt` and `exitcode.txt` under `Surl.Protocol.Dict.UnitTests/Fixtures/<case>/`
  as `EmbeddedResource`, with the recorder command line and build SHA-256 in a
  `README.md` there.

## Acceptance criteria

- [ ] Recordings exist for at least: `dict://127.0.0.1:<P>/d:hello`,
      `dict://127.0.0.1:<P>/m:hel`, and `dict://127.0.0.1:<P>/hello`, each fed the
      responses Surl will send, and each shows the pinned build exiting 0.
- [ ] Fast tests replay each recording's `request.bin` through the in-memory connection
      and assert Surl's reply bytes equal the reply bytes in the recording that curl
      accepted.
- [ ] Fast tests cover an unknown command (RFC 2229 status 500), a word with no
      definition, an over-long line, and a connection closed mid-command.
- [ ] The DICT server declares the `dict` scheme, and `ProtocolIsolationTests` pass.
- [ ] `dotnet build Surl.Protocol.Dict.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Dict.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Dict.UnitLibrary`.

## Notes

Wiring `dict` into `surl` and the live conformance run are BL-039.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
