---
id: BL-033
title: Answer DICT requests in Surl.Protocol.Dict
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-029]
touches: [Surl.Protocol.Dict.UnitLibrary, Surl.Protocol.Dict.UnitTests, Documentation/Planning/Decisions/ADR-0011-how-the-dict-server-answers.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
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

- [x] Recordings exist for at least: `dict://127.0.0.1:<P>/d:hello`,
      `dict://127.0.0.1:<P>/m:hel`, and `dict://127.0.0.1:<P>/hello`, each fed the
      responses Surl will send, and each shows the pinned build exiting 0.
- [x] Fast tests replay each recording's `request.bin` through the in-memory connection
      and assert Surl's reply bytes equal the reply bytes in the recording that curl
      accepted.
- [x] Fast tests cover an unknown command (RFC 2229 status 500), a word with no
      definition, an over-long line, and a connection closed mid-command.
- [x] The DICT server declares the `dict` scheme, and `ProtocolIsolationTests` pass.
- [x] `dotnet build Surl.Protocol.Dict.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Dict.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Dict.UnitLibrary`.

## Notes

Wiring `dict` into `surl` and the live conformance run are BL-039.

- **Measured first** (`Record-CurlExchange.ps1 -Raw`, pinned build, 2026-09-28): curl
  sends `CLIENT libcurl 8.21.0`, the command and `QUIT` in one burst without waiting for
  the banner. `d:`/`define:` send `DEFINE ! <word>`, `m:`/`match:`/`find:` send
  `MATCH ! . <word>`, an empty word becomes `default`, extra `:db:strategy` fields replace
  `!` and `.`, and a bare path is sent as-is with `:` turned into spaces (`/hello` sends
  `hello`, `/show:db` sends `show db`). Spaces, quotes and backslashes in a word arrive
  backslash-escaped. curl prints every byte and exits 0 whatever the replies.
- **Decisions: ADR-0011** (decided by Claude under Stewart's delegation). Definitions come
  from the content store: the served root's files are the one database, `surl`, a file
  name is a headword, its bytes the definition (CRLF lines, dot-stuffed, streamed).
  Ordinal headwords; dot-files, directories and names the content store refuses have no
  definition. Strategies `exact` and `prefix` (`.` = `prefix`). Banner
  `220 surl DICT server <mime> <n@surl>`, no version. No definition: `552 no match`.
  Unknown command: `500 unknown command`.
- **Line limit:** ADR-0006 (BL-024) exists, so the server reads
  `ExchangeContext.Limits.MaxLineBytes` (8192 default, CRLF included, 0 no limit) and
  answers `500 line too long`, then closes. No follow-up was needed; the head timeout and
  `420` refusal stay with BL-051.
- **Touches widened** to add ADR-0011 and the Decisions `README.md` index row: the ADR the
  decisions need. No task in Doing names either file (BL-019: Surl.Console; BL-031:
  Surl.Networking).
- `Surl.Protocol.Dict.UnitLibrary` now references `Surl.Content.UnitLibrary`, a horizontal
  library ADR-0002 allows; `ProtocolIsolationTests` pass.
- Fixtures: `define-hello`, `match-hel`, `bare-hello`, plus `define-missing` and `show-db`;
  all exit 0 with an empty stderr. The tests compare Surl's bytes with `stdout.bin`,
  which is everything curl received and accepted.
- Review (code-reviewer): fixed its two should-fix items - a definition file that cannot be opened after the `151` line now aborts the connection with a note, and the XML doc now says `SHOW INFO` takes only `surl` - and reused the text stream's output buffer.
- Results: 110 tests in `Surl.Protocol.Dict.UnitTests`, all fast; solution fast tests
  green; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Dict.UnitLibrary`: 100% line,
  100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. DictProtocolServer answers dict:// from the content store; replies to curl's d:, m: and bare-path requests match pinned upstream curl 8.21.0 recordings
