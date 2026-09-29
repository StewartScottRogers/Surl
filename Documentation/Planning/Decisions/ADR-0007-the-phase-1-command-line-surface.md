# ADR-0007 — The Phase 1 command-line surface

- **Status:** Accepted
- **Superseded in part:** the `--directory` default of `.` is superseded by [ADR-0031](ADR-0031-the-data-directory-and-in-memory-mode.md) (in memory when `--directory` is absent).
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

`surl [options] <url>...` names what to listen on, the way `curl [options] <url>` names
what to fetch (product overview, "What is Surl?"). The product overview's "Also in scope"
left the option table as a Phase 1 decision, and `Surl.Cli.UnitLibrary/CLAUDE.md` says an
option keeps curl's name and meaning wherever a server-side meaning exists. This ADR is
that table, with everything the tasks built on it need to be implemented without asking:
BL-013 (listen URLs), BL-014 (options, `--help`, `--version`), BL-016 (status line and
verbose log) and BL-019 (composing `surl`).

Inputs, all Accepted:

- ADR-0004: `ListenUrl(Scheme, Host, Port)` with `BoundPort`, the exchange log
  (`IExchangeLog.BytesReceived`, `BytesSent`, `Note`), `ExchangeId`, and the engine
  refusing a scheme no registered server claims.
- ADR-0005: the exit codes. This ADR uses `Ok` (0), `UnsupportedProtocol` (1),
  `FailedInit` (2), `MalformedUrl` (3), `CouldNotResolveHost` (6), `CouldNotReadFile` (37),
  `BindFailed` (45) and `InternalError` (125), and adds none.
- ADR-0006: every configurable limit, exposure default and TLS bound, with its option name
  and default, and the escaped rendering of untrusted bytes in the verbose log.

### What upstream curl 8.21.0's own parser does

surl shares curl's command-line conventions, so each one was measured, not assumed.

- Build: `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32),
  SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (`UpstreamCurlBuilds.json`).
- Tool: `Record-CurlExchange.ps1 -NoServer -CurlArgs … -OutDirectory …`, 2026-09-28.
  `http://127.0.0.1:1/` refuses the connection, so exit 7 (and, with `-w %{exitcode}`,
  stdout `7`) shows the command line was accepted and the transfer attempted.
- Every stderr line below is followed, for exit 2, by
  `curl: try 'curl --help' or 'curl --manual' for more information`.

| # | Convention | Command line (after `curl`) | Exit | stdout | First line of stderr |
| --- | --- | --- | --- | --- | --- |
| 1 | Short options bundled | `-sS http://127.0.0.1:1/` | 7 | | `curl: (7) Failed to connect to 127.0.0.1:1 after 2056 ms: Could not connect to server` |
| 2 | The same, separate | `-s -S http://127.0.0.1:1/` | 7 | | the same |
| 3 | Bundle ending in an option that takes an argument, argument next | `-sw %{exitcode} http://127.0.0.1:1/` | 7 | `7` | |
| 4 | Bundle ending in an option that takes an argument, argument attached | `-sw%{exitcode} http://127.0.0.1:1/` | 7 | `7` | |
| 5 | Short option, argument attached | `-s -w%{exitcode} http://127.0.0.1:1/` | 7 | `7` | |
| 6 | Short option, argument separate | `-s -w %{exitcode} http://127.0.0.1:1/` | 7 | `7` | |
| 7 | Short option, attached argument starting with `=` | `-s -w=%{exitcode} http://127.0.0.1:1/` | 7 | `=7` | |
| 8 | Long option, argument separate | `-s --write-out %{exitcode} http://127.0.0.1:1/` | 7 | `7` | |
| 9 | Long option, `--option=value` | `-s --write-out=%{exitcode} http://127.0.0.1:1/` | 7 | `7` | |
| 10 | `--option=` with an empty value | `-s --max-time= http://127.0.0.1:1/` | 2 | | `curl: option --max-time=: expected a proper numerical parameter` |
| 11 | `=value` on an option that takes none | `--silent=no http://127.0.0.1:1/` | 7 | | (none: `--silent` took effect and the value was ignored) |
| 12 | `--` ends options | `-sS -- -v` | 6 | | `curl: (6) Could not resolve host: -v` |
| 13 | `--` then a URL | `-s -w %{exitcode} -- http://127.0.0.1:1/` | 7 | `7` | |
| 14 | `--no-` on a boolean | `-s --no-silent http://127.0.0.1:1/` | 7 | | `curl: (7) Failed to connect to 127.0.0.1:1 after 2042 ms: Could not connect to server` |
| 15 | `--no-` on a boolean, later wins | `-s -w %{exitcode} -v --no-verbose http://127.0.0.1:1/` | 7 | `7` | (none) |
| 16 | `--no-` on an option that takes an argument | `-s --no-max-time http://127.0.0.1:1/` | 2 | | `curl: option --no-max-time: the given option cannot be reversed with a --no- prefix` |
| 17 | `--no-` on a TLS version option | `--no-tlsv1.2 http://127.0.0.1:1/` | 2 | | `curl: option --no-tlsv1.2: the given option cannot be reversed with a --no- prefix` |
| 18 | `--no-no-` | `-s --no-no-silent http://127.0.0.1:1/` | 2 | | `curl: option --no-no-silent: is unknown` |
| 19 | Options after the URL | `http://127.0.0.1:1/ -s -w %{exitcode}` | 7 | `7` | |
| 20 | Repeated option, last wins | `-s -w A -w B http://127.0.0.1:1/` | 7 | `B` | |
| 21 | Long option abbreviated | `-s --silen http://127.0.0.1:1/` | 2 | | `curl: option --silen: is unknown` |
| 22 | Unknown short option | `-~ http://127.0.0.1:1/` | 2 | | `curl: option -~: is unknown` |
| 23 | A lone `-` | `-s -` | 2 | | `curl: option -: is unknown` |
| 24 | An empty argument | `-sS -g --connect-timeout 3 -m 5 ""` | 2 | | `curl: option : blank argument where content is expected` |
| 25 | An empty option argument | `--cert "" http://127.0.0.1:1/` | 2 | | `curl: option --cert: blank argument where content is expected` |
| 26 | Bundle missing its argument | `http://127.0.0.1:1/ -sw` | 2 | | `curl: option -sw: requires parameter` |
| 27 | Long option missing its argument | `http://127.0.0.1:1/ --max-time` | 2 | | `curl: option --max-time: requires parameter` |
| 28 | No URL | `-s` | 2 | | `curl: (2) no URL specified` |
| 29 | Negative number | `--max-time -1 http://127.0.0.1:1/` | 2 | | `curl: option --max-time: expected a proper numerical parameter` |
| 30 | Decimal fraction | `-s -w %{exitcode} --max-time 0.5 http://127.0.0.1:1/` | 28 | `28` | |
| 31 | Comma as a decimal separator | `--max-time 1,5 http://127.0.0.1:1/` | 28 | | `curl: (28) Connection timed out after 1010 milliseconds` (read as 1) |
| 32 | Number too large, seconds | `--max-time 99999999999999999999 http://127.0.0.1:1/` | 2 | | `curl: option --max-time: expected a proper numerical parameter` |
| 33 | Number too large, bytes | `--max-filesize 99999999999999999999 http://127.0.0.1:1/` | 2 | | `curl: option --max-filesize: too large number` |
| 34 | Size suffix | `-s -w %{exitcode} --max-filesize 1k http://127.0.0.1:1/` | 7 | `7` | |
| 35 | Unknown size suffix | `--max-filesize 1q http://127.0.0.1:1/` | 2 | | `curl: option --max-filesize: is badly used here` |
| 36 | Word not in the allowed set | `--tls-max 9 http://127.0.0.1:1/` | 2 | | `curl: option --tls-max: is badly used here` |
| 37 | Lowest TLS version above the highest | `--tlsv1.3 --tls-max 1.2 https://127.0.0.1:1/` | 2 | | `curl: option --tls-max: is badly used here` |
| 38 | Error before `-V` | `--no-such -V` | 2 | | `curl: option --no-such: is unknown` |
| 39 | `-V` before an error | `-V --no-such` | 0 | the version text | (none) |
| 40 | `--version` | `--version` | 0 | four lines, below | (none) |
| 41 | `--help` | `--help` | 0 | first line `Usage: curl [options...] <url>` | (none) |

Line 1 of `--version` is `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel …`,
then `Release-Date: 2026-06-24`, `Protocols: …` and `Features: …`. On this Windows build
every line curl writes to stdout or stderr ends in CR LF (4 CRs and 4 LFs in the
`--version` output).

`curl --manual` from the same build says the same in words: short options may take their
value with or without a space; flags may be bundled (`-OLv`); boolean options are
disabled with `--no-option`; "If the long option name ends with an equals sign ("="), the
argument is the text following on its right side. (Added in 8.16.0)"; `--` marks the end
of options; and `--max-filesize` takes the suffixes k, M, G, T and P, 1024-based, and
since 8.19.0 a fraction with a period (`2.5M`).

### What upstream curl 8.21.0 does with the URL forms a listen URL shares

Same build and tool, each with `-sS -g --connect-timeout 3 -m 5 <url>`:

| URL | Exit | stderr |
| --- | --- | --- |
| `http://127.0.0.1:abc/` | 3 | `curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535` |
| `http://127.0.0.1:65536/` | 3 | the same |
| `http://127.0.0.1:/` | 7 | `… Failed to connect to 127.0.0.1:80 …` (an empty port is the default port) |
| `http://127.0.0.1:0001/` | 7 | `… Failed to connect to 127.0.0.1:1 …` (leading zeros are allowed) |
| `http://127.0.0.1:0/` | 7 | `… Failed to connect to 127.0.0.1:0 …` (0 parses) |
| `http://:8080/` | 3 | `curl: (3) URL rejected: No host part in the URL` |
| `http://[::1/` | 3 | `curl: (3) URL rejected: Bad IPv6 address` |
| `not a url` | 3 | `curl: (3) URL rejected: Malformed input to a URL function` |
| `127.0.0.1:1` | 7 | `… Failed to connect to 127.0.0.1:1 …` (curl guessed `http`) |
| `NOSUCH://127.0.0.1/` | 1 | `curl: (1) Protocol "nosuch" not supported` (the scheme is lower-cased) |

### Upstream curl 8.21.0's default port per scheme

Measured with `-sS -v --connect-timeout 3 -m 5 <scheme>://127.0.0.1/x` (`scp` and `sftp`
with `-k` and a user, since without them the build stops for want of a `known_hosts`
file), reading the port from curl's `Trying 127.0.0.1:<port>` line:

| Scheme | Port | Scheme | Port | Scheme | Port |
| --- | ---: | --- | ---: | --- | ---: |
| `dict` | 2628 | `ldap` | 389 | `scp` | 22 |
| `ftp` | 21 | `ldaps` | 636 | `sftp` | 22 |
| `ftps` | 990 | `mqtt` | 1883 | `smtp` | 25 |
| `gopher` | 70 | `mqtts` | 8883 | `smtps` | 465 |
| `gophers` | 70 | `pop3` | 110 | `telnet` | 23 |
| `http` | 80 | `pop3s` | 995 | `tftp` | 69 |
| `https` | 443 | `rtsp` | 554 | `ws` | 80 |
| `imap` | 143 | | | `wss` | 443 |
| `imaps` | 993 | | | | |

The pinned build has no `smb` or `smbs`. Both are 445 in upstream's source (`PORT_SMB`
and `PORT_SMBS`, `lib/urldata.h` at tag `curl-8_21_0`), not measured; the SMB server's
task measures them with the supplementary build BL-026 pins, and a difference is a new ADR.

### Upstream curl 8.21.0's own verbose markers

`-sS -v http://127.0.0.1:18763/x` against the recorder's canned 200: lines starting `* `
are curl's notes, `> ` the header lines curl sent, `< ` the header lines it received, and
`{ [2 bytes data]` stands for body bytes received.

## Decision

### 1. How the command line is read

surl adopts upstream curl's conventions as measured, with two deliberate differences
(marked **differs**).

| Convention | surl | Measured row |
| --- | --- | --- |
| Short flags bundle: `-vV` is `-v -V`. | Adopted. | 1, 2 |
| A short option that takes an argument may end a bundle; its argument is the rest of that argument if any (`-vm30`), else the next argument (`-vm 30`). | Adopted. | 3, 4 |
| A short option's argument may be attached (`-m30`) or separate (`-m 30`). An attached argument is taken literally, so `-m=30` has the argument `=30`. | Adopted. | 5, 6, 7 |
| A long option's argument may be separate (`--max-time 30`) or after `=` (`--max-time=30`). `--max-time=` has the empty argument. | Adopted. | 8, 9, 10 |
| `=value` after a flag (an option that takes no argument). | **differs**: refused, `FailedInit`, `surl: option --verbose=no: does not take a parameter`. curl ignores the value, so `--silent=no` silences; surl will not act against what its command line says. | 11 |
| `--` ends options: every later argument is a listen URL, even one starting with `-`. | Adopted. | 12, 13 |
| `--no-<name>` turns off a negatable flag (section 2 marks which). The later of `--name` and `--no-name` wins. `--no-` on anything else is refused. | Adopted. | 14 to 18 |
| Options and listen URLs may be interleaved in any order. | Adopted. | 19 |
| A repeated option: the last value wins. | Adopted. | 20 |
| A long option must be spelled in full; no abbreviation. | Adopted. | 21 |
| Unknown options, a lone `-`, and an empty argument are refused. | Adopted. | 22, 23, 24, 25 |
| An argument that does not start with `-` is a listen URL; an option is never guessed from one. | Adopted. | 19 |
| No scheme in a listen URL. | **differs**: refused, `MalformedUrl` (section 4). curl guesses `http` for `127.0.0.1:1`; a server binding an address should not guess what protocol it speaks there. | URL table |
| Arguments are read left to right, and the first error ends reading. `-h`/`--help` and `-V`/`--version` end reading where they stand: an error before them is reported, anything after them is not read. When both appear, the first wins. | Adopted. | 38, 39 |

`-h` takes no argument in surl (curl's takes an optional help subject); surl's help is one
page (section 6). There is no config file, no `-K`, no `--next` and no URL globbing in
Phase 1; each is a later ADR if it earns a server-side meaning.

### 2. The Phase 1 option table

Every option is global: it applies to every listen URL. Names are curl's wherever curl
has an option with the same meaning turned to the server's side (`-v`, `-m`/`--max-time`,
`--max-filesize`, `--cert`, `--key`, `--cacert`, `--tlsv1.x`, `--tls-max`, `-h`, `-V`), and
ADR-0006's names for the rest.

Argument kinds, and the error each gives. Every error below is `FailedInit` (2) with the
message and the `try` line of section 5.

- **`<seconds>`**: a decimal number, digits with an optional `.` and more digits (`30`,
  `0.5`); no sign, no exponent, no comma. At most `2147483.647` (the longest delay .NET's
  timers take, `int.MaxValue` milliseconds). Anything else:
  `option <name>: expected a proper numerical parameter` (rows 29, 32; a comma, which curl
  reads up to, is refused too).
- **`<number>`**: digits only, 0 to 2147483647. Anything else, too large included:
  `option <name>: expected a proper numerical parameter`.
- **`<bytes>`**: a `<seconds>`-shaped number followed by at most one suffix, `k`, `m`,
  `g`, `t` or `p` in either case, each 1024 times the one before (`100k` is 102400, `2.5M`
  is 2621440); the result is truncated to whole bytes. Not a number:
  `option <name>: expected a proper numerical parameter`. Any other suffix:
  `option <name>: is badly used here` (row 35). Above 9223372036854775807
  (`long.MaxValue`): `option <name>: too large number` (row 33).
- **`<file>`**, **`<directory>`**: any non-empty text, taken as a path relative to the
  current directory unless rooted. Empty: `option <name>: blank argument where content is
  expected` (row 25). Whether it exists is checked when surl starts serving (section 5),
  not while parsing.
- **`<version>`**: exactly `1.0`, `1.1`, `1.2` or `1.3`. Anything else:
  `option <name>: is badly used here` (row 36).
- A missing argument: `option <name>: requires parameter`, `<name>` being the option as
  written (`-m`, `-vm`, `--max-time`; rows 26, 27).

In every message `<name>` is the option exactly as the user wrote it, `=value` included
(row 10): `--max-time=abc` gives `option --max-time=abc: expected a proper numerical
parameter`.

| Short | Long | Argument | Server-side meaning | Default | Negatable | Bad value |
| --- | --- | --- | --- | --- | --- | --- |
| `-h` | `--help` | none | Print the help (section 6) to stdout and return `Ok`. | | no | |
| `-V` | `--version` | none | Print the version (section 6) to stdout and return `Ok`. | | no | |
| `-v` | `--verbose` | none | Write the verbose exchange log (section 8) to stderr. | off | yes | |
| | `--directory` | `<directory>` | The served directory: the root of the content store every protocol server serves from. | `.`, the current directory | no | `FailedInit` when empty; `CouldNotReadFile` (37) at start when missing, not a directory or unreadable |
| | `--allow-uploads` | none | Accept uploads into the served directory (ADR-0006 section 2). | off | yes | |
| | `--list-directories` | none | Answer directory listings (ADR-0006 section 2). | off | yes | |
| | `--follow-symlinks` | none | Follow a link whose final target stays inside the served directory (ADR-0006 section 2). | off | yes | |
| | `--serve-dot-files` | none | Serve paths with a segment starting with `.` (ADR-0006 section 2). | off | yes | |
| | `--max-connections` | `<number>` | Concurrent connections and datagram flows, all listeners together; 0 is no limit. | 1024 | no | `FailedInit` |
| | `--max-connections-per-address` | `<number>` | Concurrent connections and flows from one remote IP address; 0 is no limit. | 100 | no | `FailedInit` |
| | `--idle-timeout` | `<seconds>` | Close an exchange after this long with no byte moving; 0 is no limit. | 120 | no | `FailedInit` |
| `-m` | `--max-time` | `<seconds>` | Longest one exchange may last; 0 is no limit. | 3600 | no | `FailedInit` |
| | `--head-timeout` | `<seconds>` | Time a peer has to deliver a complete request head, command line or first packet; 0 is no limit. | 30 | no | `FailedInit` |
| | `--max-request-head` | `<bytes>` | Largest HTTP/1.x or RTSP request head; 0 is no limit. | 102400 | no | `FailedInit` |
| | `--max-line` | `<bytes>` | Longest command line of a line-oriented protocol, line ending included; 0 is no limit. | 8192 | no | `FailedInit` |
| | `--max-message` | `<bytes>` | Largest framed message of a binary protocol; 0 is no limit. | 1048576 | no | `FailedInit` |
| | `--max-filesize` | `<bytes>` | Largest upload accepted; 0 is no limit. | 104857600 | no | `FailedInit` |
| | `--tlsv1.0` | none | Lowest TLS version accepted is 1.0. | | no | |
| | `--tlsv1.1` | none | Lowest TLS version accepted is 1.1. | | no | |
| | `--tlsv1.2` | none | Lowest TLS version accepted is 1.2. | this is the default | no | |
| | `--tlsv1.3` | none | Lowest TLS version accepted is 1.3. | | no | |
| | `--tls-max` | `<version>` | Highest TLS version accepted. | `1.3` | no | `FailedInit`; also when the lowest version is above it: `option --tls-max: is badly used here` (row 37), checked after the whole command line is read |
| | `--cert` | `<file>` | The server certificate for secure schemes. **Parsed in Phase 1, served once BL-012 lands.** | none | no | `FailedInit` when empty; formats, and a file that cannot be read or used, are BL-002's TLS ADR |
| | `--key` | `<file>` | The private key for `--cert`. **Parsed in Phase 1, served once BL-012 lands.** | none | no | as `--cert` |
| | `--cacert` | `<file>` | Trust anchors for verifying a client certificate from upstream curl's `--cert`. **Parsed in Phase 1, served once BL-012 lands.** | none | no | as `--cert` |

- The `--tlsv1.x` options are four flags setting one value; the last one given wins
  (row 20), and none is negatable (row 17).
- BL-002's TLS ADR decides what a secure scheme does with no `--cert`, which certificate
  formats `--cert` and `--key` read, and whether client verification needs an option
  beyond `--cacert`. An option it needs that is not in this table is added by that ADR.
- Every limit's meaning, and how it is enforced, is ADR-0006; this table only gives each
  one its option.

### 3. The parsed command line

`Surl.Cli` returns either a failure (`SurlExitCode`, message) or one of three results:
show help, show version, or serve. Serve carries a `SurlCommandLine` record: the listen
URLs in command-line order (`IReadOnlyList<ListenUrl>`), `ServedDirectory` (`string`, as
given, `.` by default), `Verbose` (`bool`), the four exposure flags, `ExchangeLimits`
(ADR-0006 section 6) for the per-exchange limits, `MaxConnections`,
`MaxConnectionsPerAddress`, `IdleTimeout` and `MaxTime` for the engine's, the lowest and
highest TLS versions as `System.Security.Authentication.SslProtocols`, and the three
certificate paths (`string?`). Seconds become `TimeSpan`, 0 becoming
`Timeout.InfiniteTimeSpan`. BL-014 settles the member names beyond these and states them
in the XML docs.

### 4. Listen URLs

A listen URL is `scheme://host[:port][/]`, read by these rules in order. The `surl: `
prefix and the exit code are in section 5.

1. **Scheme.** The text before `://`. It must start with an ASCII letter and continue
   with letters, digits, `+`, `-` or `.`, and is lower-cased (`HTTP` is `http`). An
   argument with no `://`, or whose scheme breaks that rule: `MalformedUrl`,
   `(3) URL rejected: Malformed input to a URL function`. surl does not guess a scheme.
2. **Accepted schemes.** Every scheme in the default-port table above: the 26 schemes
   upstream curl requests over a wire. `file`, `ipfs`, `ipns` and any other scheme:
   `UnsupportedProtocol`, `(1) Protocol "<scheme>" not supported`, `<scheme>`
   lower-cased (as curl, URL table). `file` has no wire and no server (ADR-0002); what a
   file URL would name is the served directory, given with `--directory`. `ipfs` and
   `ipns` are gateway paths the `http` and `https` servers answer.
3. **Registered schemes.** A scheme this rule accepts but no registered protocol server
   claims (ADR-0004 section 4) gets the same `UnsupportedProtocol` message from
   `Surl.Console` before any listener starts. In Phase 1 that is every scheme except
   those whose server has landed; `https` and the other secure schemes are served once
   BL-012 and their server's task land.
4. **User and password.** Any `@` before the host: `MalformedUrl`,
   `(3) URL rejected: A listen URL cannot have a user name or password`. Credentials on a
   command line are visible to every local user; authentication is configured by later
   options, never in a listen URL.
5. **Host.** The text after `://` up to the first `/`, `?` or `#`, less the port.
   - `[` starts a bracketed IPv6 literal, which runs to `]`. The text between must parse
     as an IPv6 address (`System.Net.IPAddress.TryParse`); a zone may follow as `%25`
     and a zone name, and is stored decoded (`fe80::1%eth0`). Otherwise, or with no `]`:
     `MalformedUrl`, `(3) URL rejected: Bad IPv6 address`.
   - Otherwise the host runs to the first `:`. Four dot-separated decimal numbers are an
     IPv4 literal and must each be 0 to 255, else `(3) URL rejected: Bad IPv4 address`.
     Anything else is a host name: ASCII letters, digits, `-` and `.`, stored as written.
     Any other character, including a second `:` (an unbracketed IPv6 address):
     `(3) URL rejected: Malformed input to a URL function`.
   - An empty host: `MalformedUrl`, `(3) URL rejected: No host part in the URL`.
   - `ListenUrl.Host` holds the host without brackets (ADR-0004). `0.0.0.0` and `[::]`
     bind every interface; nothing else does, and there is no host-less form
     (ADR-0006 section 2).
   - A host name that resolves to nothing is found when the listener starts:
     `CouldNotResolveHost` (section 5).
6. **Port.** After the host's `:`, up to the end, `/`, `?` or `#`. Digits only, leading
   zeros allowed (`0080` is 80), 0 to 65535, else `MalformedUrl`,
   `(3) URL rejected: Port number was not a decimal number between 0 and 65535`. No `:`,
   or a `:` with nothing after it, is the scheme's default port from the table above
   (URL table: curl reads an empty port as the default). **0 asks for an ephemeral
   port**; the listener reports the port it got in `BoundPort`, and the status line
   shows it.
7. **Path, query and fragment.** After the authority, only nothing or a single `/` is
   accepted. Any other path, or any `?` or `#`: `MalformedUrl`,
   `(3) URL rejected: A listen URL cannot have a path, query or fragment`. Phase 1 gives
   none of them a meaning; the served directory is `--directory`, and a later ADR that
   gives a path a meaning (a mount prefix, say) adds `ListenUrl.Path` as ADR-0004
   section 1 allows.
8. **Transport.** `tftp` listens on UDP; every other scheme on TCP.
9. **Several URLs** mean several listeners, started in command-line order. The same
   address and port twice is not refused while parsing; the second bind fails with
   `BindFailed`.

Examples: `http://127.0.0.1:8080/` is `ListenUrl("http", "127.0.0.1", 8080)`;
`HTTP://[::1]:8080` is `("http", "::1", 8080)`; `http://localhost/` is
`("http", "localhost", 80)`; `tftp://0.0.0.0:0` is `("tftp", "0.0.0.0", 0)`.

### 5. What surl writes, and the exit codes

Every line surl writes ends with `Environment.NewLine`, so CR LF on Windows and LF on
Linux and macOS, as the pinned Windows build's CR LF lines are its platform's text
convention. Tests build expected text with `Environment.NewLine`, which keeps them
platform-neutral.

Every message on stderr starts with `surl: `, as curl's start with `curl: `. The status
line and the help and version text on stdout have no prefix.

| When | Stream | Exact text (after `surl: ` on stderr) | Exit code |
| --- | --- | --- | --- |
| Unknown option | stderr | `option <name>: is unknown` | `FailedInit` (2) |
| `--no-` on a non-negatable option | stderr | `option <name>: the given option cannot be reversed with a --no- prefix` | 2 |
| `=value` on a flag | stderr | `option <name>: does not take a parameter` | 2 |
| Option missing its argument | stderr | `option <name>: requires parameter` | 2 |
| Bad value | stderr | the message section 2 gives for its argument kind | 2 |
| Empty argument where a listen URL would be | stderr | `option : blank argument where content is expected` (row 24) | 2 |
| No listen URL, and neither `--help` nor `--version` | stderr | `(2) no URL specified` | 2 |
| Each of the seven rows above | stderr | followed by the line `try 'surl --help' for more information` | |
| Scheme not accepted or not registered | stderr | `(1) Protocol "<scheme>" not supported` | `UnsupportedProtocol` (1) |
| Malformed listen URL | stderr | `(3) URL rejected: <reason>`, reason from section 4 | `MalformedUrl` (3) |
| Served directory missing, not a directory, or unreadable | stderr | `(37) Could not open directory <path>`, `<path>` as given | `CouldNotReadFile` (37) |
| Host name resolves to nothing | stderr | `(6) Could not resolve host: <host>` | `CouldNotResolveHost` (6) |
| Address cannot be bound | stderr | `(45) Could not bind <scheme>://<address>:<port>/: <reason>`, `<address>` bracketed when IPv6, `<port>` as asked, `<reason>` one of `Address already in use`, `Address not available`, `Permission denied`, `Bind failed` for `ListenerBindFailure.AddressInUse`, `AddressNotAvailable`, `PermissionDenied`, `Other`; `<address>` is the host as written when `ListenerBindException.EndPoint` is null | `BindFailed` (45) |
| An exception no row names | stderr | `(125) Internal error: <exception message>` | `InternalError` (125) |
| Each listener bound | stdout | the status line (section 7) | |
| Stopped by Ctrl+C or SIGTERM | nothing | | `Ok` (0), ADR-0005 section 2 |

Parse errors come first, in argument order; then, before any listener starts, the served
directory is checked and every scheme is checked against the registered servers; then
listeners start in order, and the first failure stops the ones already started
(ADR-0004 section 6) and ends surl with its code. The `(N) ` numbers, and the `try` line
only after command-line errors, follow curl (rows 22 to 28 and the URL table).

### 6. `--help` and `--version`

`--help` writes exactly these lines to stdout (every description starts in column 46,
after the left side padded with spaces; no line has trailing spaces):

```
Usage: surl [options...] <url>...
     --allow-uploads                         Accept uploads into the served directory
     --cacert <file>                         CA certificates that verify client certificates
     --cert <file>                           Server certificate for secure schemes
     --directory <directory>                 Directory to serve (default: current directory)
     --follow-symlinks                       Follow links that stay inside the directory
     --head-timeout <seconds>                Time a peer has to send a request head (default 30)
 -h, --help                                  Show this help and quit
     --idle-timeout <seconds>                Close an exchange idle this long (default 120)
     --key <file>                            Private key for --cert
     --list-directories                      Answer directory listings
     --max-connections <number>              Connections at once, all listeners (default 1024)
     --max-connections-per-address <number>  Connections at once from one address (default 100)
     --max-filesize <bytes>                  Largest upload accepted (default 100M)
     --max-line <bytes>                      Longest command line accepted (default 8192)
     --max-message <bytes>                   Largest framed message accepted (default 1M)
     --max-request-head <bytes>              Largest HTTP or RTSP request head (default 100k)
 -m, --max-time <seconds>                    Longest time one exchange may take (default 3600)
     --serve-dot-files                       Serve names that start with a dot
     --tls-max <version>                     Highest TLS version accepted (default 1.3)
     --tlsv1.0                               Accept TLS 1.0 or later
     --tlsv1.1                               Accept TLS 1.1 or later
     --tlsv1.2                               Accept TLS 1.2 or later (default)
     --tlsv1.3                               Accept TLS 1.3 or later
 -v, --verbose                               Log every exchange to stderr
 -V, --version                               Show version number and quit
```

The first line mirrors curl's `Usage: curl [options...] <url>` (row 41), with `<url>...`
because several listen URLs are normal. Options are listed alphabetically by long name,
as curl's `--help all` does. An option added later takes its alphabetical place; the
description column moves only if a longer left side needs it.

`--version` writes exactly two lines to stdout:

```
surl <version> (<runtime>)
Protocols: <schemes>
```

- `<version>` is `surl` assembly's `AssemblyInformationalVersionAttribute` with any `+`
  and what follows removed (the SDK appends the commit there); `1.0.0` while no version
  is set.
- `<runtime>` is `System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier`
  (`win-x64`, `linux-arm64`).
- `<schemes>` is every scheme a registered protocol server claims, in ordinal order,
  separated by single spaces: what this `surl` build actually serves, as curl's
  `Protocols:` line is what its build requests.

curl's `Release-Date:` and `Features:` lines have no Phase 1 counterpart; a later ADR adds
them if they gain one. No version number appears anywhere a peer can see (ADR-0006
section 3).

### 7. The listener status line

Once every listener has bound, surl writes one status line per listen URL to stdout, in
command-line order, then serves:

```
Listening on <scheme>://<host>:<bound port>/
```

- `<host>` is `ListenUrl.Host` as written on the command line (lower-cased scheme, host
  as written), bracketed when it is an IPv6 literal (`[::1]`; a zone as `%25<zone>`).
- `<bound port>` is `ListenUrl.BoundPort`, so a URL that asked for port 0 shows the port
  it got, and a URL with no port shows the default.
- Examples: `Listening on http://127.0.0.1:8080/`, `Listening on http://[::1]:49731/`,
  `Listening on tftp://0.0.0.0:69/`, `Listening on http://localhost:80/`.

The line is written after all listeners bound, not as each binds, so a reader that sees
the line for the last URL knows every listener is up. A test reads the bound port from
it. Nothing is written on stop.

### 8. The `-v` verbose exchange log

With `-v`, `Surl.Output`'s `IExchangeLogFactory` (ADR-0004 section 5) writes every
exchange event to stderr, one event line at a time, whole lines only, so lines from
concurrent exchanges never interleave. Without `-v` it writes nothing.

```
#<exchange id> <marker> <text>
```

| Marker | Written for | `<text>` |
| --- | --- | --- |
| `<` | `IExchangeLog.BytesReceived`: bytes surl received from the peer | the bytes, escaped |
| `>` | `IExchangeLog.BytesSent`: bytes surl sent to the peer | the bytes, escaped |
| `*` | `IExchangeLog.Note`: a note from the server or the engine | the note, escaped |

- The markers keep curl's direction: in curl's `-v`, `>` is what curl sent and `<` what
  it received; in surl's, `>` is what surl sent and `<` what surl received. The request
  line curl logs as `> GET / HTTP/1.1` appears in surl's log as
  `#1 < GET / HTTP/1.1\r\n`.
- `<exchange id>` is `ExchangeContext.ExchangeId` in decimal.
- **Escaping** is ADR-0006 section 3's escaped rendering: each byte 0x20 to 0x7E except
  backslash as itself; CR as `\r`; LF as `\n`; every other byte, and backslash, as `\x`
  and two upper-case hex digits. A note is encoded as UTF-8 and escaped by the same rule,
  since the log cannot tell which part of a note came from a peer; a Windows path in a
  note therefore shows its backslashes as `\x5C`.
- **Splitting bytes into lines.** The bytes of one `BytesReceived` or `BytesSent` call
  are split after every LF, and after every 1024 bytes that hold no LF; each piece is one
  log line. So a request head of three header lines is three `<` lines, each ending in
  `\r\n`, and a large binary body does not make one enormous line. An empty call writes
  nothing. A piece never spans two calls.
- A note is always one line: a CR or LF in it is escaped, never written.
- The engine's notes (BL-015) are, exactly: `Connection from <remote> to <local> on
  <listen url>` or, for a datagram flow, `Flow from <remote> to <local> on
  <listen url>`, when the exchange starts; `Closed` when it ends normally; and
  `Protocol server threw <exception type name>: <message>` when `ServeAsync` throws.
  `<remote>` and `<local>` are `IPEndPoint.ToString()` (`127.0.0.1:50000`,
  `[::1]:50000`), and `<listen url>` is written as in the status line. A limit being hit
  is noted by whoever enforces it, naming the limit (ADR-0006); each task pins its text.
  A connection or flow refused past a connection limit belongs to no exchange, so its
  note is written as `#- * <text>`, with `-` where the exchange id goes; its seam and text
  are [ADR-0028](ADR-0028-a-connection-refused-past-a-limit-is-noted-outside-any-exchange.md).

Example of one HTTP exchange:

```
#1 * Connection from 127.0.0.1:50000 to 127.0.0.1:8080 on http://127.0.0.1:8080/
#1 < GET /hello.txt HTTP/1.1\r\n
#1 < Host: 127.0.0.1:8080\r\n
#1 < User-Agent: curl/8.21.0\r\n
#1 < Accept: */*\r\n
#1 < \r\n
#1 > HTTP/1.1 200 OK\r\n
…
#1 * Closed
```

Timestamps are not in Phase 1's log (they would make it untestable without a clock
rendering rule); curl's `--trace-time`, `--trace` and `-w` are later ADRs.

## Alternatives considered

- **Mirror curl's `=value` on a flag (ignore the value).** Rejected: `--allow-uploads=no`
  would allow uploads. A command line that says the opposite of what surl does breaks
  "say what it does, do what it says", and the refusal costs curl compatibility only for
  a spelling curl's own manual does not document for flags.
- **Guess `http` for a scheme-less listen URL, as curl does.** Rejected: the scheme picks
  the protocol a public address speaks; one character of shorthand is not worth a server
  that speaks the wrong protocol.
- **`file:///dir` as the way to name the served directory.** Rejected: one directory
  serves every listener, so it is a global option, not one listen URL among several;
  and `file` has no wire (ADR-0002).
- **A path in a listen URL as the served directory or a mount prefix.** Deferred to a
  later ADR; refusing it now keeps that choice open without changing what an accepted
  URL means.
- **`--root` or `--output-dir` for the served directory.** Rejected: `--output-dir` is
  curl's option for where downloads are written, a different meaning; `--directory`
  says what the value is, and the glossary calls it the served directory.
- **Require `--directory`.** Rejected: `surl http://127.0.0.1:0/` should serve the
  current directory, as a quick server does; exposure is already bounded by ADR-0006's
  defaults (no listings, no uploads, no dot-files, no links), and the status line names
  what is listening.
- **`-s`/`--silent` and `-S`/`--show-error`.** Not in Phase 1: surl has no progress meter
  to silence, and silencing a server's own startup errors hides why it is not running.
- **Separate `>`/`<` meaning from curl's (`<` for what curl sent).** Rejected: the marker
  would then mean "sent" in one tool and "received" in the other depending on who prints
  it; keeping it relative to the printer makes both logs read the same way.
- **Write `\n` on every platform.** Rejected: the pinned Windows build writes CR LF, and
  `Environment.NewLine` keeps surl a good citizen of each platform's text tools; tests stay
  neutral by building expectations from it.

## Consequences

- BL-013 implements section 4 and its messages; BL-014 implements sections 1 to 3, 5 and
  6; BL-016 implements sections 7 and 8 (the line-ending choice it was to make is made
  here); BL-015 writes the engine notes of section 8; BL-019 implements section 5's
  ordering and texts and maps each failure to its exit code.
- BL-002's TLS ADR fills in `--cert`, `--key` and `--cacert`, and may add options.
- `Surl.Cli` needs the default-port table above as data; a scheme's default port that
  differs on another pinned build is a new ADR.
- An option added later, a change of meaning, or a change to any exact text here is a
  new ADR that supersedes the affected section.
