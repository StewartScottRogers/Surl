---
id: BL-035
title: Answer TELNET sessions in Surl.Protocol.Telnet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-029]
touches: [Surl.Protocol.Telnet.UnitLibrary, Surl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-035 — Answer TELNET sessions in Surl.Protocol.Telnet

## Goal

`Surl.Protocol.Telnet.UnitLibrary` contains a TELNET protocol server for the `telnet`
scheme. It completes the option negotiation the pinned upstream curl 8.21.0 build
performs, including the options curl's `-t`/`--telnet-option` sets, and then runs a
session whose behaviour the plan decides. It is proven by byte scripts recorded from
that build.

## Context

- TELNET is RFC 854 (protocol) and RFC 855 (options). Upstream curl's
  `-t`/`--telnet-option` sets `TTYPE`, `XDISPLOC` and `NEW_ENV`
  (https://curl.se/docs/manpage.html, checked 2026-09-28; the page then documented
  8.23.0). Which `IAC` sequences 8.21.0 actually sends, with and without `-t`, is
  measured with `Record-CurlExchange.ps1 -Raw` (BL-029) against the pinned build, never
  assumed. Use `-StandardInput` to give curl session input.
- Decide in the `/feature` plan, and state in the server's XML doc: which options the
  server agrees to (`DO`/`WILL`) and refuses, and what the session does with data (for
  example echo each line back and end on a fixed command). That makes the session
  predictable for conformance. `IAC IAC` escaping in data (RFC 854) is handled both ways.
- The server implements the protocol-server interface from the listener-seam ADR
  (BL-000, BL-005), and is tested through BL-005's in-memory connection. It references
  only Abstractions and ADR-0002's horizontal libraries.
- Fixtures: as in BL-017, under `Surl.Protocol.Telnet.UnitTests/Fixtures/<case>/`,
  embedded, with a `README.md`.

## Acceptance criteria

- [x] Recordings exist for a plain session with standard input, and for a session with
      `-t TTYPE=vt100` and `-t NEW_ENV=USER,alice`. Each is fed Surl's intended bytes,
      and the pinned build's exit code and stdout are recorded.
- [x] Fast tests replay each recording and assert Surl's negotiation and session bytes
      equal the accepted ones, and that received `TTYPE` and `NEW_ENV` values are
      reported through the exchange context.
- [x] Fast tests cover `IAC IAC` in data both ways, an unknown option (refused with
      `DONT` or `WONT` as RFC 855 says), and a connection closed mid-sequence.
- [x] `ProtocolIsolationTests` pass. `dotnet build Surl.Protocol.Telnet.UnitLibrary
      -warnaserror` is clean, the fast tests are green with no `Integration` test in
      `Surl.Protocol.Telnet.UnitTests`, and `Measure-CodeQuality.ps1` reports no failing
      member in `Surl.Protocol.Telnet.UnitLibrary`.

## Notes

Wiring `telnet` into `surl` and the live conformance run are BL-041.

**Measured first** (pinned win-x64 8.21.0, `Record-CurlExchange.ps1 -Raw`, 2026-09-28):
curl sends no negotiation until the server sends one, then answers it together with its
first line of standard input; it never closes the connection when its standard input
ends, only when the server does, and exits 0. Without `-t` it refuses TTYPE, XDISPLOC and
NEW-ENVIRON, agrees to NAWS with an unasked `SB NAWS 0 0 0 0`, and offers `WILL BINARY`,
`DO BINARY`, `WILL SGA`. With `-t` it agrees and answers `SEND` with `IS`. `IAC` in its
standard input is doubled. This Windows build sent `hello\r\n` input as `hello\n`.
Details in `Surl.Protocol.Telnet.UnitTests/Fixtures/README.md`.

**Decisions** (Claude under Stewart's delegation; stated in `TelnetProtocolServer`'s XML
doc; the ADR is BL-077, see below):
- The server speaks first: `WILL SGA`, `DO` TTYPE, XDISPLOC, NEW-ENVIRON, NAWS, and a
  one-line banner that names no version (ADR-0006). Without a server-first negotiation
  curl never reveals its `-t` values.
- Agreed: SGA and BINARY both ways; TTYPE, XDISPLOC, NEW-ENVIRON and NAWS from the client.
  Everything else refused (`DO` -> `WONT`, `WILL` -> `DONT`). The RFC 1143 rule that a
  request for the current state is not answered stops loops; the server never asks to
  turn anything off, so three states per side suffice.
- `SEND` for TTYPE, XDISPLOC and NEW-ENVIRON once the client turns each on; the answer,
  and the first NAWS after NAWS turns on, is one `IExchangeLog.Note` each ("reported
  through the exchange context" - the context has no other channel, and changing
  Abstractions is outside this task). Unasked subnegotiations are ignored unreported, so
  a client cannot flood the log (the code-review's one should-fix).
- The session echoes each line (LF, CRLF or CR NUL ends it) with CRLF, and `quit` (any
  case) answers `bye` and closes once every `SEND` is answered. A predictable echo makes
  conformance simple, and a server-side end is needed because curl never hangs up.
- Limits: `MaxLineBytes` bounds a line (answered `line too long`, closed) and a
  subnegotiation payload (closed, no answer). A broken subnegotiation (`IAC` + other
  command inside it) is dropped and the command read, so nothing is swallowed.
- Fixtures add a `replies.bin` per case (every byte the server sent), since curl strips
  TELNET commands from stdout and `stdout.bin` alone cannot pin the negotiation.

**ADR deferred to BL-077.** CLAUDE.md wants an ADR, but BL-027 (in `Doing`) holds
`Documentation/Planning/Decisions`, so this task did not widen its `touches` into it.
BL-077 writes the ADR from these notes; it touches only its own file and the index.

**Review** (code-reviewer): no must-fix. Fixed: log flood; broken subnegotiation
swallowing the next command; `IAC SB IAC`; `IsOver` scanning 256 slots per byte (now a
count); no `SEND` after `quit`; `ThrowsExactlyAsync`; one test class per production
class. Left as documented choices: a bare CR in a line is echoed unchanged; invalid UTF-8
in a value is logged as U+FFFD; a second unescaped `VALUE` in NEW-ENVIRON replaces the
first.

Result: 74 tests in `Surl.Protocol.Telnet.UnitTests`; 100% line and branch coverage,
worst CRAP 10, no failing member. `dotnet format --verify-no-changes` flags LF line endings in
`Surl.Cli.UnitLibrary` files, which predates this task and is outside its `touches`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. TelnetProtocolServer negotiates options with pinned curl 8.21.0, reports -t TTYPE/NEW_ENV values as log notes, and echoes lines until quit
