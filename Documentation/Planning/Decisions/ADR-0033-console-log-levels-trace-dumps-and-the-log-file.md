# ADR-0033 — Console log levels, `-s`, `-S`, `--log-level`, `--trace`, `--trace-ascii`, `--trace-time` and `--log-file`

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-101. Stewart approved the feature on 2026-09-29 - console logging with curl-shaped
  levels none, error, info (the default), verbose and trace, selected by `-s`, `-s -S`, nothing,
  `-v` and `--trace`/`--trace-ascii`, plus `--log-level`, `--trace-time` and `--log-file`; the
  details he left to this ADR.
- **Supersedes in part:** [ADR-0007](ADR-0007-the-phase-1-command-line-surface.md) section 8's
  "Without `-v` it writes nothing", its list of the engine's notes, and its closing note that
  timestamps and `--trace` are later ADRs; and ADR-0007 "Alternatives considered"'s rejection of
  `-s`/`--silent` and `-S`/`--show-error`. Everything else in ADR-0007 stands.

## Context

Today surl has two settings: `-v` writes the exchange log of ADR-0007 section 8 to stderr, and
without it no exchange line is written at all. An operator running surl without `-v` cannot tell
that a client connected; with `-v` every byte of every exchange floods stderr. Stewart asked for
curl's own shape: levels from silent to a full byte dump.

Where the code is on 2026-09-29:

- `Surl.Cli.UnitLibrary/SurlCommandLine.cs` has `bool Verbose`; `Surl.Cli.UnitLibrary/CommandLineOptions.cs`
  lists `verbose` (`-v`) as a negatable flag.
- `Surl.Output.UnitLibrary/VerboseExchangeLogFactory.cs` creates a `VerboseExchangeLog`
  (`Surl.Output.UnitLibrary/VerboseExchangeLog.cs`) or `SilentExchangeLog`
  (`Surl.Output.UnitLibrary/SilentExchangeLog.cs`), and writes `NoteOutsideExchange` lines as
  `#- * <text>` ([ADR-0028](ADR-0028-a-connection-refused-past-a-limit-is-noted-outside-any-exchange.md)).
  `Surl.Output.UnitLibrary/ListenerStatusLine.cs` writes `Listening on ...` to stdout.
- `Surl.Console/CommandLineRunner.cs` writes every `surl: ` message to its `error` writer, the
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 9
  `surl: warning: ...` lines always, and `* Serving a throwaway certificate, SHA-256 <fingerprint>`
  with `-v` only.
- The engine's notes, as `Surl.Core.UnitLibrary/ServingEngine.cs` and
  `Surl.Core.UnitLibrary/ServingEngine.Datagrams.cs` write them today (they have drifted from the
  texts ADR-0007 section 8 lists, so section 3 below restates them from the code):
  - `Exchange <id> opened: <scheme> from <remote>.` when a connection or a datagram flow starts
    an exchange (one form for both);
  - `Exchange <id> ended; closing the connection.` or `Exchange <id> ended; closing the flow.`;
  - `Exchange <id> ended because the protocol server threw <exception type name>: <message>`;
  - `Exchange <id> cancelled: no byte moved for the idle timeout of <n> s.`,
    `Exchange <id> cancelled: it reached the maximum exchange duration of <n> s.` and
    `Exchange <id> cancelled at shutdown.`;
  - `TLS handshake completed: <protocol>, <cipher suite>, ALPN <protocol or none>` and
    `TLS handshake failed: <message>` ([ADR-0010](ADR-0010-the-server-side-tls-contract.md) section 2);
  - outside any exchange, `Refused <a connection or a flow> from <remote>: past <limit>.` (ADR-0028).

### What upstream curl 8.21.0 does

Measured on 2026-09-29 with `Record-CurlExchange.ps1` on the pinned reference build
`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` (`UpstreamCurlBuilds.json`,
Git for Windows, curl 8.21.0), against the recorder's canned
`HTTP/1.1 200 OK\r\nContent-Length: 6\r\nContent-Type: text/plain\r\n\r\nHello\n` on port 18101.
Each run was `& .\Record-CurlExchange.ps1 -Port 18101 -Response <that> -OutDirectory <dir> -CurlArgs <args>`
with `<args>` as below; each exited 0.

**`--trace <file> http://127.0.0.1:18101/`** (the file, whole):

```
*   Trying 127.0.0.1:18101...
* Established connection to 127.0.0.1 (127.0.0.1 port 18101) from 127.0.0.1 port 64960 
* using HTTP/1.x
=> Send header, 79 bytes (0x4f)
0000: 47 45 54 20 2f 20 48 54 54 50 2f 31 2e 31 0d 0a GET / HTTP/1.1..
0010: 48 6f 73 74 3a 20 31 32 37 2e 30 2e 30 2e 31 3a Host: 127.0.0.1:
0020: 31 38 31 30 31 0d 0a 55 73 65 72 2d 41 67 65 6e 18101..User-Agen
0030: 74 3a 20 63 75 72 6c 2f 38 2e 32 31 2e 30 0d 0a t: curl/8.21.0..
0040: 41 63 63 65 70 74 3a 20 2a 2f 2a 0d 0a 0d 0a    Accept: */*....
* Request completely sent off
<= Recv header, 17 bytes (0x11)
0000: 48 54 54 50 2f 31 2e 31 20 32 30 30 20 4f 4b 0d HTTP/1.1 200 OK.
0010: 0a                                              .
<= Recv header, 19 bytes (0x13)
0000: 43 6f 6e 74 65 6e 74 2d 4c 65 6e 67 74 68 3a 20 Content-Length: 
0010: 36 0d 0a                                        6..
<= Recv header, 26 bytes (0x1a)
0000: 43 6f 6e 74 65 6e 74 2d 54 79 70 65 3a 20 74 65 Content-Type: te
0010: 78 74 2f 70 6c 61 69 6e 0d 0a                   xt/plain..
<= Recv header, 2 bytes (0x2)
0000: 0d 0a                                           ..
<= Recv data, 6 bytes (0x6)
0000: 48 65 6c 6c 6f 0a                               Hello.
* Connection #0 to host 127.0.0.1:18101 left intact
```

- One event is a header line then its dump rows. The header is `=> Send <kind>` or
  `<= Recv <kind>` (`header` or `data`), `, <n> bytes (0x<n in lower-case hex>)`. A note is
  `* <text>` (a trailing space on the `Established` line is curl's own).
- A row is the offset of its first byte as at least four lower-case hex digits, `: `, sixteen
  bytes as two lower-case hex digits and a space each (three spaces for each byte past the end
  of the event), then those bytes as characters: 0x20 to 0x7E as themselves, every other byte `.`.
  Offsets restart at `0000` for every event.
- The file's lines end CR LF: curl opens it as text on Windows.

**`--trace-ascii <file> http://127.0.0.1:18101/`** (the file, whole):

```
*   Trying 127.0.0.1:18101...
* Established connection to 127.0.0.1 (127.0.0.1 port 18101) from 127.0.0.1 port 64961 
* using HTTP/1.x
=> Send header, 79 bytes (0x4f)
0000: GET / HTTP/1.1
0010: Host: 127.0.0.1:18101
0027: User-Agent: curl/8.21.0
0040: Accept: */*
004d: 
* Request completely sent off
<= Recv header, 17 bytes (0x11)
0000: HTTP/1.1 200 OK
<= Recv header, 19 bytes (0x13)
0000: Content-Length: 6
<= Recv header, 26 bytes (0x1a)
0000: Content-Type: text/plain
<= Recv header, 2 bytes (0x2)
0000: 
<= Recv data, 6 bytes (0x6)
0000: Hello.
* Connection #0 to host 127.0.0.1:18101 left intact
```

- No hex column. A row holds at most 64 bytes as characters (the same `.` rule) and ends early
  after a CR LF pair, which is not printed; the next row's offset is the byte after the LF
  (`0027` is 39, the byte after `Host: 127.0.0.1:18101\r\n`). A lone LF is not a break: it prints
  as `.` (`Hello.`). A CR LF at the start of a row prints an empty row (`004d: `, with its space).

**`--trace <file> --trace-time http://127.0.0.1:18101/`** (the first lines):

```
10:07:20.895000 *   Trying 127.0.0.1:18101...
10:07:20.895000 * Established connection to 127.0.0.1 (127.0.0.1 port 18101) from 127.0.0.1 port 64962 
10:07:20.896000 * using HTTP/1.x
10:07:20.896000 => Send header, 79 bytes (0x4f)
0000: 47 45 54 20 2f 20 48 54 54 50 2f 31 2e 31 0d 0a GET / HTTP/1.1..
...
10:07:20.899000 <= Recv header, 17 bytes (0x11)
0000: 48 54 54 50 2f 31 2e 31 20 32 30 30 20 4f 4b 0d HTTP/1.1 200 OK.
```

- The stamp is local time `HH:MM:SS.uuuuuu` (24-hour, six fraction digits) and a space, before
  every header and note line; dump rows are not stamped. This Windows build's clock has
  millisecond resolution, so the last three digits read `000`.

**`-v --trace-time http://127.0.0.1:18101/`** (stderr, progress meter lines left out):

```
10:07:20.961000 *   Trying 127.0.0.1:18101...
10:07:20.963000 * Established connection to 127.0.0.1 (127.0.0.1 port 18101) from 127.0.0.1 port 64963 
10:07:20.963000 * using HTTP/1.x
10:07:20.963000 > GET / HTTP/1.1
10:07:20.963000 > Host: 127.0.0.1:18101
10:07:20.963000 > User-Agent: curl/8.21.0
10:07:20.963000 > Accept: */*
10:07:20.963000 > 
10:07:20.964000 * Request completely sent off
10:07:20.966000 < HTTP/1.1 200 OK
10:07:20.966000 < Content-Length: 6
10:07:20.966000 < Content-Type: text/plain
10:07:20.966000 < 
10:07:20.966000 { [6 bytes data]
10:07:20.966000 * Connection #0 to host 127.0.0.1:18101 left intact
```

- The same stamp before every verbose line.

**`--trace - http://127.0.0.1:18101/`**: the same dump as `--trace <file>`, written to stdout
before the body (`Hello\n`), with LF line endings (curl's stdout is binary). stderr held only the
progress meter.

Also measured, with `-NoServer` against the closed port `http://127.0.0.1:1/` (exit 7 each):

| Arguments | stderr |
| --- | --- |
| `-s` | nothing |
| `-sS` | `curl: (7) Failed to connect to 127.0.0.1:1 after 2024 ms: Could not connect to server` |
| `--trace Z:\no\such\dir\t.txt` | the trace, as text, on stderr, then the `curl: (7)` line: an unopenable trace file falls back to stderr, with no message and no exit code of its own |
| `--trace <f> -v` | `Warning: -v, --verbose overrides an earlier trace option`, then the verbose log |
| `-v --trace <f>` | `Warning: --trace overrides an earlier trace/verbose option`; the trace goes to `<f>` |
| `--trace <f> --trace-ascii <g>` | `Warning: --trace-ascii overrides an earlier trace/verbose option`; only `<g>` is written |
| `--trace <f>`, `<f>` a 5000-byte file | `<f>` afterwards holds only the new trace (213 bytes): the file is truncated |

`curl --help --silent` (measured 2026-09-29, same build): "Do not show progress meter, warning
messages or error messages ... Use --show-error in addition to this option to disable progress
meter but still show error messages." `curl --help` puts `-s`, `-S`, `-v`, `--trace`,
`--trace-ascii` and `--trace-time` in its `verbose` category ("Tracing, logging etc").
Upstream's manual: https://curl.se/docs/manpage.html.

## Decision

### 1. The levels: what each writes, and where

surl has five log levels, in this order: `none`, `error`, `info`, `verbose`, `trace`. Each
writes everything the one before it writes, except that the per-exchange lines are written in
one form only - the info line, the verbose lines or the dump - the form of the chosen level.

"The log stream" below is stderr, or the `--log-file` (section 6) when one is given.

| Level | Selected by | stdout | The log stream | `surl: (N)` failures |
| --- | --- | --- | --- | --- |
| `none` | `-s` | nothing | nothing | not written (stderr) |
| `error` | `-s -S` | nothing | `Exchange <id> ended because the protocol server threw ...` notes | written (stderr) |
| `info` (default) | nothing | the `Listening on` lines | startup warnings (section 7), and the info lines of section 3 | written (stderr) |
| `verbose` | `-v` | the `Listening on` lines | startup warnings, the throwaway-certificate note, and ADR-0007 section 8's `#<id> <marker> <text>` lines for every event | written (stderr) |
| `trace` | `--trace <file>`, `--trace-ascii <file>` | the `Listening on` lines, and the dump when `<file>` is `-` | startup warnings, the throwaway-certificate note, and the dump of section 4 when no trace file was given (`--log-level trace` alone) | written (stderr) |

- **`-s` hides the `Listening on` lines and the `surl: ` failure messages.** `none` means
  nothing, as curl's `-s` hides curl's error messages (measured); the exit code still tells a
  script what failed. A script that needs the bound port does not pass `-s`.
- **`-s -S` shows the failure messages** - every `surl: (N) ...` line of ADR-0007 section 5,
  ADR-0031 and ADR-0032, on stderr - and the notes of a protocol server that threw, which are
  surl's own faults. Nothing else: no `Listening on` line, no warning (curl's `-S` shows errors,
  not warnings).
- **Command-line errors are always written.** A command line surl cannot read has no level yet,
  so `option <name>: ...` and its `try 'surl --help' for more information` line are written to
  stderr whatever `-s` says.
- **`--help` and `--version` are not log output**: written to stdout at every level, as curl's
  `-s --help` still prints its help.
- The failure messages always go to stderr, never to the `--log-file`: they are what a
  supervisor reads when surl does not start, and a log file that cannot be opened is itself one
  of them (section 6).
- At `trace` with a trace file, the per-exchange output is in the file, and the log stream holds
  only the warnings and the throwaway-certificate note, as curl's `--trace <file>` leaves stderr
  with nothing but its errors.

### 2. `-s`, `-S`, `--log-level` and how they combine

| Option | Argument | Meaning | Negatable |
| --- | --- | --- | --- |
| `-s`, `--silent` | none | level `none` | yes: `--no-silent` sets level `info` |
| `-S`, `--show-error` | none | when the final level is `none`, it becomes `error`; otherwise no effect | yes: `--no-show-error` turns it off |
| `-v`, `--verbose` | none | level `verbose` (unchanged, ADR-0007) | yes: `--no-verbose` sets level `info` |
| `--log-level` | `<none\|error\|info\|verbose\|trace>` | that level | no |
| `--trace` | `<file>` | level `trace`, hex-and-ASCII dump to `<file>` (`-` for stdout) | no |
| `--trace-ascii` | `<file>` | level `trace`, ASCII dump to `<file>` (`-` for stdout) | no |
| `--trace-time` | none | stamp the log's lines (section 5) | yes: `--no-trace-time` |
| `--log-file` | `<file>` | the log stream is `<file>`, appended (section 6); `-` for stdout | no |

- **The last level option given wins.** `-s`, `--no-silent`, `-v`, `--no-verbose`,
  `--log-level`, `--trace` and `--trace-ascii` each set the one level, read left to right, as
  ADR-0007 section 1's "last value wins"; `-S` is applied after the whole line is read. So
  `-s -S`, `-sS` and `-S -s` are `error`; `-s -v` is `verbose`; `-v --log-level error` is `error`;
  `--trace f -v` is `verbose` and writes no dump (curl: `-v` overrides an earlier trace);
  `--trace f --trace-ascii g` dumps ASCII to `g` only and never opens `f`. surl writes no
  "overrides" warning: curl's warning describes its own conflict, and in surl a later level
  option is simply the level asked for.
- **The trace file and its kind** are the last `--trace` or `--trace-ascii` given, and are used
  only when the final level is `trace`. `--log-level trace` with no trace option dumps in the
  `--trace` (hex) layout to the log stream.
- **`--log-level` words** are matched case-insensitively (as `--auth` and `--cert-type`) and
  stored lower-case. Any other word: `FailedInit` (2),
  `surl: option --log-level: is badly used here` and the `try` line (ADR-0007 row 36). An empty
  argument to `--log-level`, `--trace`, `--trace-ascii` or `--log-file`:
  `surl: option <name>: blank argument where content is expected` and the `try` line (ADR-0007
  row 25). Missing argument, `=value` on a flag and `--no-` on a non-negatable option take
  ADR-0007 section 5's texts.
- The parsed command line (BL-104) replaces `bool Verbose` with the resolved level (one level
  type, defined in `Surl.Output.UnitLibrary`), the trace file and kind (or none), the timestamp
  switch and the log file (or none).

### 3. The info line

At `info`, per exchange, surl writes one line when it opens, and a line for each engine note that
tells of trouble; nothing for the bytes, for a normal close or for a completed TLS handshake.
Each is the verbose log's own line for that note, unchanged:

```
#<id> * Exchange <id> opened: <scheme> from <remote>.
```

for example `#1 * Exchange 1 opened: http from 127.0.0.1:50000.` and, for a datagram flow,
`#7 * Exchange 7 opened: tftp from 127.0.0.1:50001.` - the engine writes one form for both.

The notes written at `info`, matched on the start of the note's text:

| Note starts with | Written at |
| --- | --- |
| `Exchange <id> opened: ` | `info` |
| `Exchange <id> cancelled` | `info` |
| `TLS handshake failed: ` | `info` |
| `Exchange <id> ended because the protocol server threw ` | `error` and above |
| any note outside an exchange (`#- * Refused ...`, ADR-0028) | `info` |

- **When:** at open, so an operator sees a client the moment it connects, and a connection that
  hangs is visible while it hangs; one line per connection, as Stewart's table says. The close
  is not written: the verbose level has it, and at `info` it would double every line.
- **Built only from the engine's existing notes:** the level-aware log (BL-105) chooses which
  `Note` calls to write by the text's start; no `IExchangeLog` member and no protocol server
  changes. A protocol server's own notes are never written at `info` (they are verbose detail),
  so the match is made only against these engine prefixes; a protocol server's note that happened
  to start the same way would also be written, which is harmless.
- The line is escaped as ADR-0007 section 8 escapes every note.

### 4. The `--trace` and `--trace-ascii` dump

The dump is curl's layout, turned to the server's side and labelled with the exchange id.

```
#<id> * <note>
#<id> <= Recv data, <n> bytes (0x<n>)
<rows>
#<id> => Send data, <n> bytes (0x<n>)
<rows>
#- * <note outside an exchange>
```

- **Direction** keeps ADR-0007 section 8's rule, relative to the printer: `<= Recv` is bytes surl
  received from the peer (`IExchangeLog.BytesReceived`), `=> Send` bytes surl sent
  (`BytesSent`). The request curl dumps as `=> Send header` appears in surl's dump as
  `<= Recv data`.
- **Kind** is always `data`: `IExchangeLog` carries bytes, not their meaning, and a label that
  claimed `header` would sometimes be false.
- **One call is one event.** Each `BytesReceived` or `BytesSent` call is one header line and its
  rows, written whole under the log's lock, so concurrent exchanges' events never interleave
  inside a block; an empty call writes nothing. `<n>` is the call's byte count in decimal, then in
  lower-case hex without leading zeros.
- **Exchange id:** every header and note line starts `#<id> ` (`ExchangeContext.ExchangeId` in
  decimal), `#- ` outside an exchange, so concurrent exchanges can be told apart; rows carry no
  prefix and belong to the header above them.
- **Rows**, `--trace`: offset as at least four lower-case hex digits and `: `, sixteen bytes as
  `hh ` each (three spaces per byte past the end), then the bytes as characters, 0x20 to 0x7E as
  themselves and every other byte `.`. `--trace-ascii`: offset and `: `, then at most 64 bytes as
  characters by the same rule, the row ending early after a CR LF pair, which is not printed.
  Offsets start at `0000` in each event. Exactly as measured above.
- **Notes** are `* ` and the note escaped as ADR-0007 section 8 escapes it (a note is one line).
- **Line endings** are `Environment.NewLine`, as ADR-0007 section 5 decides for every line surl
  writes: CR LF on Windows (as the pinned build writes its trace file) and LF elsewhere. For
  `--trace -` on Windows this differs from curl's LF on stdout; one rule for every line surl
  writes is simpler to test and to read than a per-stream one.
- **`-` is stdout**, as in curl; the dump then shares stdout with the `Listening on` lines, each
  written as whole lines.
- **The file is truncated** when surl starts, as curl truncates (measured), and created if
  missing.
- The dump is the plaintext of a TLS exchange, as curl's is: `Surl.Core`'s recording decorators
  call `IExchangeLog` above TLS.

### 5. `--trace-time`

- **Format:** `HH:mm:ss.ffffff` and one space, before the line: the hour 00 to 23, six fraction
  digits, invariant culture, as measured (`10:07:20.895000 `). surl writes all six digits the
  clock gives, rather than the pinned Windows build's millisecond `000`.
- **Clock:** `TimeProvider.GetLocalNow()` of the injected `TimeProvider`, so the time is in its
  `LocalTimeZone`; a test uses a fake provider with a fixed zone, never the wall clock.
- **Which lines:** every line the exchange log writes - the info lines, the verbose lines, the
  dump's header and note lines, and `#- *` lines outside an exchange - at the moment it is
  written. Not stamped: dump rows (as curl), the `Listening on` lines, `surl: ` messages and
  warnings (curl does not stamp its `Warning:` lines), the throwaway-certificate note, help and
  version text.
- It changes nothing at `none` and `error` levels except the protocol-server-threw notes, which
  are exchange log lines and are stamped.

### 6. `--log-file <file>`

- **What goes there:** the log stream - the exchange log lines of the chosen level (info lines,
  verbose lines, or the dump when there is no trace file), the startup warnings and the
  throwaway-certificate note. Not the `surl: (N)` failure messages or command-line errors (stderr,
  section 1), not the `Listening on` lines (stdout), not a trace file's dump.
- **Appended**, and created if missing: a server's log outlives a restart, unlike curl's
  per-transfer trace. Each line is flushed as it is written, so `tail -f` sees it at once.
- `-` means stdout, as curl's `--stderr -`.
- **A file that cannot be opened** - `--log-file`, `--trace` or `--trace-ascii` - ends surl
  before any listener binds with `CouldNotWriteFile` (23, ADR-0031) and, on stderr:
  `surl: (23) Could not open <path> for <option>: <exception message>`, `<path>` as given and
  `<option>` the option's long name with its dashes (`--log-file`). curl instead falls back to
  stderr silently (measured); a server usually runs unattended, and a trace asked for that
  silently lands somewhere else hides the one thing the operator wanted. The file is opened
  whenever the option is in effect, even at `none`, so a bad path is never a surprise later.
  The message follows section 1's rules (hidden by `-s` alone).
- **A trace file that is the `--log-file` file** - the same full path, ignoring case - is
  refused the same way, with `: --log-file names the same file` after the option; see
  [ADR-0037](ADR-0037-a-trace-file-that-is-the-log-file-is-refused.md).

### 7. Startup warnings and the throwaway-certificate note

- ADR-0032 section 9's `surl: warning: <option>: ...` lines keep their text, order and timing
  (after the command line is read, before any `Listening on` line). They are written at `info`,
  `verbose` and `trace`, to the log stream, unstamped. `-s` and `-s -S` hide them, as curl's `-s`
  hides its warnings.
- `* Serving a throwaway certificate, SHA-256 <fingerprint>` keeps its text and is written at
  `verbose` and `trace`, to the log stream, unstamped: at `info` the `--self-signed` warning
  already says a throwaway certificate is served.

### 8. `--trace-ids`, `--trace-config` and `--stderr`

- **`--trace-ids`: never.** curl's option adds connection and transfer ids to its lines; surl's
  verbose and trace lines always carry the exchange id, so the option would change nothing. It
  stays an unknown option (`FailedInit`, 2).
- **`--trace-config`: later.** curl's picks libcurl components (`ids`, `time`, `http/2`, `tls`,
  ...) to trace in more detail. surl's servers have no component-level trace notes yet; when one
  gains them (HTTP/2 frames, TLS internals), the ADR that adds them gives `--trace-config` its
  meaning. Until then it is an unknown option.
- **`--stderr`: never.** `--log-file` is the name Stewart approved, and it differs from curl's
  `--stderr` by keeping the failure messages on stderr; two names for one concept break the
  solution's naming rule. `--stderr` stays an unknown option.

## Alternatives considered

- **The info line at close, with the exchange's duration and byte counts.** Rejected: the engine
  writes no such note, so it would need a new `IExchangeLog` member or engine change, and a line
  only at close hides a hanging connection. The open line needs nothing new.
- **Info lines both at open and at close.** Rejected: doubles the default output for no new
  fact; the verbose level has the close.
- **Follow curl on an unopenable trace file (fall back to stderr).** Rejected: section 6.
- **`-s` keeps the `Listening on` lines.** Rejected: `none` would then not mean nothing, and
  Stewart's table says `-s` writes nothing.
- **Label dumped bytes `header` until the head ends, as curl does.** Rejected: the log cannot
  tell a head from a body without each protocol server saying so, which is an `IExchangeLog`
  change for a cosmetic label.
- **Truncate `--log-file` as curl truncates `--trace` and `--stderr`.** Rejected: a restarted
  server would erase the log of the run that failed.
- **A warning when one level option overrides another, as curl warns.** Rejected: section 2.

## Consequences

- BL-104 parses the options of section 2 into `SurlCommandLine`; BL-105 writes the levels,
  the info line and `--trace-time` (sections 1, 3, 5) in `Surl.Output`; BL-106 writes the dumps
  (section 4); BL-107 composes them in `Surl.Console` (sections 1, 6, 7). BL-102's ADR-0034 puts
  the options in `--help`'s categories.
- ADR-0007 section 8's list of the engine's notes is replaced by the list in this ADR's Context,
  which is what `Surl.Core.UnitLibrary/ServingEngine.cs` writes.
- Changing any engine note's opening words is now a change to what the info level writes, and
  needs this ADR's table updated by a new ADR.
- A new option that selects a level, or a change to any text here, is a new ADR that supersedes
  the affected section.
