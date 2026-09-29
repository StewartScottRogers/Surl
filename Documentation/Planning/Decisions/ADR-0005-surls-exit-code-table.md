# ADR-0005 — Surl's exit-code table

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

`SurlExitCode` in `Surl.Protocol.Abstractions.UnitLibrary` holds `Ok = 0` and
`FailedInit = 2`, and its remarks say Phase 1 decides the full table in an ADR, reusing
upstream curl's `CURLE_*` number wherever a server-side meaning carries over, and that a
value is never renumbered. This is that ADR. BL-004 turns the table into enum members; the
command-line ADR (BL-003) and the serving engine (BL-015, and ADR-0004 section 6 for a
failure to bind) name a row of it for every failure path.

`surl [options] <url>` mirrors `curl [options] <url>`, so a script that already knows
curl's exit codes should read surl's the same way. What upstream curl's own command line
returns for its own bad command lines is a measurement, taken from the pinned reference
build (ADR-0003):

- Build: `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32),
  SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (`UpstreamCurlBuilds.json`).
- Tool: `Record-CurlExchange.ps1 -NoServer -CurlArgs … -OutDirectory …`, 2026-09-28.

| Case | Command line | Exit code | First line of stderr |
| --- | --- | --- | --- |
| Unknown long option | `curl --no-such-option http://127.0.0.1:1/` | 2 | `curl: option --no-such-option: is unknown` |
| Option missing its argument | `curl http://127.0.0.1:1/ -o` | 2 | `curl: option -o: requires parameter` |
| Malformed URL | `curl -g http://[::1/` | 3 | `curl: (3) URL rejected: Bad IPv6 address` |
| Malformed URL | `curl http://` | 3 | `curl: (3) URL rejected: No host part in the URL` |
| Unsupported scheme | `curl bogus://127.0.0.1/` | 1 | `curl: (1) Protocol "bogus" not supported` |
| Invalid option value | `curl --max-time abc http://127.0.0.1:1/` | 2 | `curl: option --max-time: expected a proper numerical parameter` |
| Port out of range | `curl http://127.0.0.1:99999/` | 3 | `curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535` |
| File that cannot be opened | `curl file:///Z:/no/such/dir/` | 37 | `curl: (37) Could not open file Z:/no/such/dir/` |

Without `-g`, `curl http://[::1/` fails in curl's URL globbing instead (exit 3,
`curl: (3) bad range specification in position 9:`); `-g` isolates the URL parser.

Upstream numbers not measured here come from https://curl.se/libcurl/c/libcurl-errors.html,
read 2026-09-28; the page names no version and the numbers are stable across releases.

## Decision

### 1. The table

| `SurlExitCode` member | Number | Upstream `CURLE_*` reused | Failure it covers |
| --- | --- | --- | --- |
| `Ok` | 0 | `CURLE_OK` | Success: surl served until it was stopped (section 2), or had nothing to serve, such as `--help` or `--version`. |
| `UnsupportedProtocol` | 1 | `CURLE_UNSUPPORTED_PROTOCOL` | The listen URL names a scheme Surl has no server for. Measured: curl returns 1 for a scheme it does not support. |
| `FailedInit` | 2 | `CURLE_FAILED_INIT` | An unknown option; an option missing its argument; an invalid option value (not a number, out of range, not one of the allowed words); any other command line surl cannot act on that is not a URL error. Measured: curl returns 2 for all three kinds. |
| `MalformedUrl` | 3 | `CURLE_URL_MALFORMAT` | The listen URL cannot be parsed: no host, a bad IPv6 literal, a port outside 0-65535, or no URL at all. Measured: curl returns 3 for each. |
| `CouldNotResolveHost` | 6 | `CURLE_COULDNT_RESOLVE_HOST` | The listen URL's host name does not resolve to any address (`ListenerBindFailure.HostNotFound`, ADR-0004 section 6). Same meaning: a name that did not resolve. |
| `CouldNotReadFile` | 37 | `CURLE_FILE_COULDNT_READ_FILE` | The served directory (or served file) is missing, is not the kind of entry the option asked for, or cannot be read for lack of permission. Measured: curl returns 37 for a `file://` path it cannot open. |
| `BindFailed` | 45 | `CURLE_INTERFACE_FAILED` | A listen address cannot be bound: in use, not a local address, or not permitted (`ListenerBindFailure.AddressInUse`, `AddressNotAvailable`, `PermissionDenied`, `Other`). Upstream returns 45 when it cannot bind the local address or port it was told to use (`--interface`, `--local-port`), the one place curl itself binds. |
| `InternalError` | 125 | none | An unexpected internal failure: an exception no other row names, reaching the top of `surl`. Upstream has no generic "internal error" code, and borrowing one with another meaning (`CURLE_OUT_OF_MEMORY` 27, `CURLE_FAILED_INIT` 2) would tell a script something false. |

`Ok = 0` and `FailedInit = 2` keep their numbers and meaning; nothing is renumbered.

### 2. Stopped by Ctrl+C or SIGTERM

A server's normal end is being told to stop. When surl has started serving and receives
Ctrl+C (SIGINT) or SIGTERM, it stops accepting, lets the exchanges in flight finish or
cancels them, and returns **`Ok` (0)**. It does not return the shell's 128+signal
number: that would make every ordinary shutdown look like a crash to a script or a
service manager. A signal that arrives before surl has started serving ends startup the
same way and also returns 0; a failure found during startup keeps its own row.

### 3. Numbers Surl adds

A failure with a server-side meaning that upstream curl also has reuses curl's number.
A failure with no upstream counterpart takes a number counting down from 125. Upstream's
`CURLE_*` values grow upward from 1 and stand near 100 today; 126 and 127 are reserved by
POSIX shells for "not executable" and "not found", and 128 and up mean "killed by a
signal". Counting down from 125 keeps Surl's own codes out of all three ranges.

### 4. One mapping, in one place

Libraries report what went wrong as exceptions or result types (`ListenerBindException`
and its `ListenerBindFailure`, ADR-0004 section 6; the command-line parser's result, BL-003).
`Surl.Console` and the serving engine turn them into a `SurlExitCode`, and nothing else
chooses an exit code.

## Alternatives considered

- **A distinct code per command-line error** (unknown option, missing argument, bad
  value). Rejected: upstream curl returns 2 for all three (measured above), and surl's
  command line mirrors curl's; the stderr message tells the kinds apart.
- **`CURLE_COULDNT_CONNECT` (7) for a failure to bind.** Rejected: 7 means the peer
  could not be reached, which is not what happened. 45 is what curl returns when its own
  local bind fails.
- **`CURLE_READ_ERROR` (26) for the served directory.** Rejected: curl uses 26 when
  reading local data fails part-way through a transfer; 37 is what it returns when a file
  it was told to use cannot be opened, which is the served-root case (measured).
- **Return 130 or 143 after Ctrl+C or SIGTERM.** Rejected for the reason in section 2.

## Consequences

- BL-004 adds `UnsupportedProtocol`, `MalformedUrl`, `CouldNotResolveHost`,
  `CouldNotReadFile`, `BindFailed` and `InternalError` to `SurlExitCode` with these
  numbers.
- The command-line ADR (BL-003) cites `FailedInit`, `MalformedUrl` and
  `UnsupportedProtocol` for its bad-input paths.
- The serving engine maps `ListenerBindFailure.HostNotFound` to `CouldNotResolveHost` and
  every other `ListenerBindFailure` to `BindFailed`.
- A later failure that fits no row is decided in a new ADR, which adds a row and never
  renumbers one.
