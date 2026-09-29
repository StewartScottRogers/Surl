# TELNET fixtures

Sessions recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw` (BL-029). Never from the Curl port (ADR-0003).

Each case was fed the exact bytes `TelnetProtocolServer` sends for it before any test
pinned them: the opening negotiation and banner first, as soon as curl connected, then
everything Surl answers to what curl sent. Upstream curl sends no negotiation until the
server sends one, then answers the opening in one burst together with its first line of
standard input, and sends the next line on its own. The second reply is Surl's answer to
both bursts. Every case exited 0 with an empty `stderr.txt`; curl printed the banner and
every echoed line, with the TELNET commands taken out, to `stdout.bin`.

Each folder holds the recorder's five files - `request.bin` (the bytes curl sent),
`transcript.txt` (both directions), `stdout.bin`, `stderr.txt` and `exitcode.txt` - and
`replies.bin`, every byte the server sent, in order: the recorder's replies joined. They
are embedded resources of `Surl.Protocol.Telnet.UnitTests`, so the tests read them without
touching the file system. `.gitattributes` here keeps git from rewriting their line
endings. `TelnetProtocolServerTests` replays each `request.bin`, whole and one byte per
read, and asserts the bytes Surl writes equal `replies.bin`.

Recorded on 2026-09-28 from the repository root, in Windows PowerShell, with

```powershell
$open = '\xFF\xFB\x03\xFF\xFD\x18\xFF\xFD\x23\xFF\xFD\x27\xFF\xFD\x1Fsurl TELNET server: each line is echoed back; quit ends the session.\r\n'
```

(`WILL SUPPRESS-GO-AHEAD`, `DO TERMINAL-TYPE`, `DO X-DISPLAY-LOCATION`, `DO NEW-ENVIRON`,
`DO NAWS`, the banner), each command run as
`.\Record-CurlExchange.ps1 -Port 18623 -Raw -RawReplyFirst -RawIdleMilliseconds 1500 -RawReply $open,<second reply> -StandardInput <input> -CurlArgs <arguments> -OutDirectory Surl.Protocol.Telnet.UnitTests\Fixtures\<folder>`:

| Folder | Standard input | Arguments | Second reply |
| --- | --- | --- | --- |
| `plain-session` | `'hello\nquit\n'` | `'-sS','telnet://127.0.0.1:18623'` | `'\xFF\xFD\x00\xFF\xFB\x00\xFF\xFD\x03hello\r\nbye\r\n'` |
| `telnet-options-session` | `'hello\nquit\n'` | `'-sS','-t','TTYPE=vt100','-t','NEW_ENV=USER,alice','telnet://127.0.0.1:18623'` | `'\xFF\xFA\x18\x01\xFF\xF0\xFF\xFA\x27\x01\xFF\xF0\xFF\xFD\x00\xFF\xFB\x00\xFF\xFD\x03hello\r\nbye\r\n'` |
| `iac-in-data` | `'\xFFab\nquit\n'` | `'-sS','telnet://127.0.0.1:18623'` | `'\xFF\xFD\x00\xFF\xFB\x00\xFF\xFD\x03\xFF\xFFab\r\nbye\r\n'` |

`replies.bin` is `$open` and the second reply, decoded the way the recorder decodes them
and joined.

What curl sent, measured:

- **Without `-t`:** `DO SUPPRESS-GO-AHEAD`, `WONT` to each of `TERMINAL-TYPE`,
  `X-DISPLAY-LOCATION` and `NEW-ENVIRON`, `WILL NAWS` with an unasked `SB NAWS 0 0 0 0`
  (a 0x0 window: its standard input is not a console), then its own offers
  `WILL BINARY`, `DO BINARY` and `WILL SUPPRESS-GO-AHEAD`, then the data.
- **With `-t TTYPE=vt100 -t NEW_ENV=USER,alice`:** `WILL` in place of `WONT` for
  `TERMINAL-TYPE` and `NEW-ENVIRON`, and after Surl's `SEND`s,
  `SB TERMINAL-TYPE IS vt100 SE` and `SB NEW-ENVIRON IS VAR USER VALUE alice SE`.
- **Standard input** is sent with `IAC` doubled. This Windows build sends a line ended
  CRLF on its standard input as ending LF alone (a probe with `'hello\r\n'` sent
  `hello\n`), so Surl takes LF alone as a line end. curl does not close the connection when its standard input
  ends; it waits for the server to close, which is why Surl ends the session on `quit`.
