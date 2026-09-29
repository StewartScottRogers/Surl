# Gopher fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw`, never from the Curl port (ADR-0003). In each case the
recorder read the selector line curl sent, answered with the reply the Gopher server sends
for it (`-RawReply`), and closed the connection, as the server does.

Each folder holds the recorder's files: `request.bin` (the bytes curl sent), `stdout.bin`,
`stderr.txt`, `exitcode.txt` and `transcript.txt`. Every case exited 0 with an empty
`stderr.txt`, and in every case `stdout.bin` is exactly the reply sent: curl writes every
byte of a Gopher reply as it arrives, until the close. `GopherProtocolServerTests` replays
`request.bin` through the in-memory connection and asserts the server's reply equals
`stdout.bin`. The files are embedded resources of `Surl.Protocol.Gopher.UnitTests`, so the
tests read them without touching the file system; `.gitattributes` here keeps git from
rewriting their CRLF line endings.

The menu in `root-menu` names host `127.0.0.1` and port 18634, the listen URL the tests
give the server.

Recorded on 2026-09-28 from the repository root, in Windows PowerShell:

| Folder | curl sent | Command line |
| --- | --- | --- |
| `file-selector` | `/file.txt` CRLF | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply 'Hello from Surl.\n' -CurlArgs '-sS','gopher://127.0.0.1:18634/0/file.txt' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\file-selector` |
| `root-menu` | CRLF (the empty selector) | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply '0file.txt\t/file.txt\t127.0.0.1\t18634\r\n1sub\t/sub\t127.0.0.1\t18634\r\n.\r\n' -CurlArgs '-sS','gopher://127.0.0.1:18634/' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\root-menu` |
| `missing-selector` | `/missing.txt` CRLF | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply '3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n' -CurlArgs '-sS','gopher://127.0.0.1:18634/0/missing.txt' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\missing-selector` |
| `path-as-is-dot-segment` | `/../file.txt` CRLF | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply '3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n' -CurlArgs '-sS','--path-as-is','gopher://127.0.0.1:18634/0/../file.txt' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\path-as-is-dot-segment` |
| `encoded-dot-segment` | `/%2e%2e/file.txt` CRLF | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply '3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n' -CurlArgs '-sS','gopher://127.0.0.1:18634/0/%252e%252e/file.txt' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\encoded-dot-segment` |

What else was measured the same way on 2026-09-28, and not kept as a fixture: curl drops
the item-type character and URL-decodes the rest of the path, so
`gopher://h/1/sub` sends `/sub` and `gopher://h/7/find%09hello%20world` sends
`/find` TAB `hello world`; without `--path-as-is` it removes dot segments before sending,
including percent-encoded ones, so `gopher://h/0/%2e%2e/x` and `gopher://h/0/../x` both
send the empty selector.

## Limits and exposure (BL-052)

Recorded on 2026-09-29 from the repository root, in Windows PowerShell, with the same
pinned build (SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`),
where `$missing` is `'3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n'`.
Each fed curl the exact bytes `GopherProtocolServer` writes for the case (none, for a
close), and each exited 0 with an empty `stderr.txt`; `stdout.bin` is what the tests pin.
A close with no bytes is an empty, successful transfer to curl.

| Folder | curl sent | What Surl sends | Pinned by | Command line |
| --- | --- | --- | --- | --- |
| `directory-selector` | `/sub` CRLF | the error menu: a directory with listings off is answered as absent | `ExposureTests` | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply $missing -CurlArgs '-sS','gopher://127.0.0.1:18634/1/sub' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\directory-selector` |
| `closed-with-no-bytes` | `/file.txt` CRLF | nothing: the connection is closed, as after a head timeout | `HeadTimeoutTests` | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -CurlArgs '-sS','gopher://127.0.0.1:18634/0/file.txt' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\closed-with-no-bytes` |
| `selector-8192-bytes` | `/` and 8189 `x`, CRLF: 8192 bytes | the error menu: the line is within `MaxLineBytes`, so it is answered | `SelectorLimitTests` | `$x = 'x' * 8189; .\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -RawReply $missing -CurlArgs '-sS',"gopher://127.0.0.1:18634/0/$x" -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\selector-8192-bytes` |
| `selector-8193-bytes` | `/` and 8190 `x`, CRLF: 8193 bytes | nothing: the line is past `MaxLineBytes` | `SelectorLimitTests` | `$x = 'x' * 8190; .\Record-CurlExchange.ps1 -Port 18634 -Raw -RawIdleMilliseconds 300 -CurlArgs '-sS',"gopher://127.0.0.1:18634/0/$x" -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\selector-8193-bytes` |

curl sends its selector without pausing, so no live recording can show a head timeout;
the tests drive one with a hand-written `TimeProvider`, and `closed-with-no-bytes` pins
what curl makes of the close that follows it.

## gophers (BL-066)

Recorded on 2026-09-29 from the repository root, in Windows PowerShell, with the same
pinned build (SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`),
with `Record-CurlExchange.ps1 -Raw -Tls`: the recorder answered over TLS 1.2 with its
throwaway self-signed certificate (so curl was given `-k`), recorded the decrypted bytes
in `request.bin` and `transcript.txt`, and sent a TLS close_notify before closing. curl
sent the same selector lines as over `gopher`, and each case exited 0 with an empty
`stderr.txt`. `GopherProtocolServerTests` replays them through an `InMemoryConnection`
that already holds a TLS session, as the engine hands one over (ADR-0010), with listen URL
`gophers://127.0.0.1:18634/`; the menu names host `127.0.0.1` and port 18634.

| Folder | curl sent | Command line |
| --- | --- | --- |
| `gophers-file-selector` | `/file.txt` CRLF | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -Tls -RawIdleMilliseconds 300 -RawReply 'Hello from Surl.\n' -CurlArgs '-sS','-k','gophers://127.0.0.1:18634/0/file.txt' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\gophers-file-selector` |
| `gophers-root-menu` | CRLF (the empty selector) | `.\Record-CurlExchange.ps1 -Port 18634 -Raw -Tls -RawIdleMilliseconds 300 -RawReply '0file.txt\t/file.txt\t127.0.0.1\t18634\r\n1sub\t/sub\t127.0.0.1\t18634\r\n.\r\n' -CurlArgs '-sS','-k','gophers://127.0.0.1:18634/' -OutDirectory Surl.Protocol.Gopher.UnitTests\Fixtures\gophers-root-menu` |
