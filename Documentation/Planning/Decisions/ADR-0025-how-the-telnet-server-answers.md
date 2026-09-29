# ADR-0025 — How the TELNET server answers

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28, in BL-035; recorded 2026-09-29 in BL-077

## Context

BL-035 asks for a TELNET server in `Surl.Protocol.Telnet.UnitLibrary` that completes the
sessions pinned upstream curl 8.21.0 holds for `curl telnet://host:port`, with and without
`-t` (curl's `TTYPE`, `XDISPLOC` and `NEW_ENV` options), and that reports what the client
tells it about its terminal and environment. TELNET (RFC 854) says nothing about what a
server does with the data a client sends, so the server's behaviour had to be decided.
BL-035 decided it and stated it in the XML doc of `TelnetProtocolServer`; it could not
write this ADR itself, because another task held this folder at the time.

Measured with `Record-CurlExchange.ps1 -Raw` against the pinned win-x64 build (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), 2026-09-28, standard
input `hello\nquit\n`; the recordings are in `Surl.Protocol.Telnet.UnitTests/Fixtures`,
whose `README.md` gives every command:

| Server sends first | curl arguments | curl sends |
| --- | --- | --- |
| nothing | `telnet://127.0.0.1:P` | its standard input only, no negotiation; it then waits and never closes the connection |
| `WILL SGA`, `DO TTYPE`, `DO XDISPLOC`, `DO NEW-ENVIRON`, `DO NAWS`, a banner line | `-sS telnet://127.0.0.1:P` | in one burst: `DO SGA`, `WONT TTYPE`, `WONT XDISPLOC`, `WONT NEW-ENVIRON`, `WILL NAWS`, `SB NAWS 0 0 0 0 SE` (a 0x0 window: its standard input is not a console), its own offers `WILL BINARY`, `DO BINARY`, `WILL SGA`, then `hello\nquit\n` |
| the same | `-sS -t TTYPE=vt100 -t NEW_ENV=USER,alice telnet://127.0.0.1:P` | the same, with `WILL` in place of `WONT` for `TTYPE` and `NEW-ENVIRON`; after the server's `SEND`s, `SB TTYPE IS vt100 SE` and `SB NEW-ENVIRON IS VAR USER VALUE alice SE` |
| the same | `-sS telnet://127.0.0.1:P`, standard input `\xFFab\nquit\n` | the byte 255 doubled, `IAC IAC` |

In every case curl exits 0 with an empty stderr once the server closes the connection,
and prints what the server sent with the TELNET commands taken out. It does not close the
connection when its standard input ends; only the server's close ends the session. This
Windows build sent standard input `hello\r\n` as `hello\n`.

## Decision

1. **The server speaks first.** On connecting it sends `IAC WILL SUPPRESS-GO-AHEAD`,
   `IAC DO TERMINAL-TYPE`, `IAC DO X-DISPLAY-LOCATION`, `IAC DO NEW-ENVIRON`,
   `IAC DO NAWS` and the banner line
   `surl TELNET server: each line is echoed back; quit ends the session.` followed by CRLF.
   Upstream curl negotiates only after the server does, so without a server-first
   negotiation curl never reveals its `-t` values. The banner names no version
   (ADR-0006, section 3).
2. **Options agreed and refused.** The server performs `SUPPRESS-GO-AHEAD` (which it
   offers) and `BINARY`, and agrees when the client offers either of them, or
   `TERMINAL-TYPE`, `X-DISPLAY-LOCATION`, `NEW-ENVIRON` or `NAWS`, the options curl's `-t`
   sets. Every other option is refused, `DO` with `WONT` and `WILL` with `DONT` (RFC 855).
   A request for the state an option is already in, including the client's agreement to
   one of the server's own offers or requests, is not answered (RFC 1143), so negotiation
   never loops. The server never asks to turn an option off, so each side of an option is
   off, on, or asked for and not yet answered.
3. **`SEND`, and one log note per answer.** Once the client performs `TERMINAL-TYPE`,
   `X-DISPLAY-LOCATION` or `NEW-ENVIRON`, the server sends `IAC SB <option> SEND IAC SE`
   for it, unless the client has already sent `quit`. The client's answer to each `SEND`,
   and the first window size it sends after turning `NAWS` on, is reported as one
   `IExchangeLog.Note` on the exchange's log: the terminal type, the X display location,
   the window size as `<width>x<height>`, or the environment variables as `NAME=value`
   (`NAME (undefined)` for one sent without a value), values decoded as UTF-8. The
   exchange context has no other channel for what a client reports. A subnegotiation the
   server cannot read gets a note saying so. Any other subnegotiation - one not asked for,
   or a second answer to one `SEND` - is ignored unreported, so a client cannot flood the
   log. Other two-byte commands (`NOP`, `AYT` and the rest) are ignored.
4. **The session echoes each line until `quit`.** Each line of data, ended by LF (a CR
   directly before it dropped) or by CR NUL, is sent back byte for byte, followed by CRLF;
   a bare CR inside a line is sent back as it is. `IAC IAC` in the client's data is the
   byte 255, and 255 in an echoed line is sent as `IAC IAC` (RFC 854). A predictable echo
   makes the session checkable byte for byte. A line reading `quit`, in any case, is
   answered `bye` and CRLF, and nothing the client sends after it is echoed. Because curl
   never closes the connection itself, the server must end the session: it closes the
   connection once the client has answered every `SEND` it made, since curl sends its
   `quit` line before its answers arrive (the `-t` recording above). A client that answers
   none keeps the connection until the serving engine's idle timeout cancels the exchange
   (ADR-0006, sections 1 and 6).
5. **Limits.** ADR-0006's `MaxLineBytes` (`--max-line`, default 8192) bounds both a line
   and a subnegotiation. A line longer than it, its line ending included, is answered
   `line too long` and CRLF, noted on the log, and the connection closed. This departs
   from ADR-0006 section 5's "close with no bytes" for TELNET: a TELNET line is typed and
   read by a person, and curl prints what the server sends, so the reason is shown where
   it helps. A subnegotiation whose payload is longer than it closes the connection with
   no answer and a note on the log. A limit of 0 lets a line or subnegotiation grow
   without bound. ADR-0006's head timeout does not apply to TELNET (section 1).
6. **Malformed commands.** Inside a subnegotiation, `IAC` followed by anything but `SE`
   or `IAC` discards the subnegotiation and is read as that command, and so is
   `IAC SB IAC`, which names no option, so a broken subnegotiation never swallows the next
   command. A client that closes the connection ends the session; one that closes it part
   way through a command gets a note saying so.

## Consequences

- `curl telnet://host:port` against `surl` prints the banner, each line it sends, and
  `bye`, and exits 0 once it has sent `quit`; BL-041 proves it live with the three
  recorded cases.
- `curl -t TTYPE=… -t XDISPLOC=… -t NEW_ENV=…` makes those values visible in `surl -v`'s
  log, which is what an operator testing curl's `-t` wants to see.
- `surl` is not a login server or a shell: TELNET here is an echo that shows the
  negotiation and ends on `quit`, a deliberate limit of a server built to answer curl.
- A session that never sends `quit` lasts until the idle timeout or the maximum exchange
  duration ends it (ADR-0006, section 1).
