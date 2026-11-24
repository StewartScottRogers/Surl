# ADR-0190: `-w` / `--write-out` prints a format to stdout once per exchange

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation (BL-1663)

## Context

The product overview listed "`-w` style output per exchange" as in scope and not built. A script
that drives surl (or a test that starts it as a process) has no line on stdout that says how each
exchange ended: the info log goes to stderr and the event stream is read through `Surl.Api`,
`surl-mcp` or `--control`.

## Measured from pinned upstream curl 8.21.0

`Record-CurlExchange.ps1` with `-s -w <format>` against a loopback server:

| Format | stdout | stderr |
| --- | --- | --- |
| `a%%b\tc\nd` | `a%b`, TAB, `c`, LF, `d` | none |
| `x%zy\qz\\` | `x%zy\qz\\` unchanged | none |
| `x%{bogus}y` | `xy` | `curl: unknown --write-out variable: 'bogus'` |
| `x%{http_code` | `x%{http_code` unchanged | none |
| `%{json}` | every variable as one JSON object | none |

So: `%%` is a percent sign; `\r`, `\n` and `\t` are the control characters and no other backslash
is special (`\\` is two backslashes); an unknown `%{name}` prints nothing and warns on stderr; a
`%{` with no `}` is printed as written; `%{json}` is all variables in an object with its keys in
ordinal order. Exit code is unaffected by any of it.

## Decisions

1. **The option.** `-w <format>` / `--write-out <format>`, category `logging`, argument any text
   (the empty string included), the last one given wins, default none. `@file` and `@-` formats
   are not read: the argument is the format. It is also a `-K` file line (`write-out = "..."`).
2. **Source.** One subscriber of the instance's event stream (`WriteOutPrinter` in
   `Surl.Console`): it keeps each `ExchangeStartedEvent` and, at the matching
   `ExchangeEndedEvent`, expands the format and writes it to stdout. An exchange that never ended
   prints nothing. The stream drops the oldest event when a subscriber lags, so a slow stdout
   loses lines rather than slowing a server (ADR-0124).
3. **Expansion** is `Surl.Cli`'s `WriteOutFormat.Expand`, a pure function, by the rules measured above.
4. **Variables**, named as curl names the client's view (`size_download` is what the client
   downloaded, so what surl sent): `exchange_id`, `json`, `listen_url`, `remote_ip` (empty when
   the client has no IP address), `remote_port` (0 then), `scheme`, `size_download`, `size_upload`
   and `time_total` (seconds, six decimals, from the event stream's clock). curl's other variables
   describe a client's transfer (`http_code`, `url`, `num_connects`...) and have no server-side
   meaning; they are unknown here. Variables only grow, never change meaning.
5. **Streams.** The expanded text goes to stdout with no added newline; the unknown-variable
   warning goes to stderr as `surl: unknown --write-out variable: '<name>'`, once per
   occurrence per exchange, as curl does. `-s` does not hide the format (as in curl, `-s -w` prints).
6. **Not for `mcp+stdio:`**, whose stdout holds only MCP messages and which has no exchanges.
7. **Secrets.** No variable carries a credential.
