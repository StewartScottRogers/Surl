# SOCKS fixtures

Recorded on 2026-11-05 (BL-558) from upstream curl 8.21.0, the win-x64 reference build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400`, from the repository root in PowerShell.
Never from the Curl port (ADR-0003). The recorder played the SOCKS proxy and then, inside the
tunnel, the origin; `origin.test` was never resolved by curl.

Each folder holds the recorder's files: `request.bin` (every byte curl sent: the SOCKS request,
then the `GET` it sent inside the tunnel), `transcript.txt` (both directions), `stdout.bin`,
`stderr.txt` and `exitcode.txt`. They are embedded resources of `Surl.Protocol.Socks.UnitTests`;
`.gitattributes` keeps git from rewriting their bytes.

`<ok>` is `-RawReply '\x00\x5A\x00\x00\x00\x00\x00\x00','HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok'`:
the granted reply ADR-0121 decision 2 writes, then the origin's answer.

| Folder | Command line | Curl sent first | Exit, stdout or stderr |
| --- | --- | --- | --- |
| `socks4-connect` | `.\Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400 -Port 18141 -OutDirectory ...\socks4-connect -RawReply <ok> -CurlArgs '-sS','--socks4','127.0.0.1:18141','http://127.0.0.1:8080/a'` | `04 01 1F90 7F000001 00` | 0, `ok` |
| `socks4-user-id` | `... -Port 18142 -RawReply <ok> -CurlArgs '-sS','--proxy','socks4://alice@127.0.0.1:18142','http://127.0.0.1:8080/a'` | `04 01 1F90 7F000001 "alice" 00` | 0, `ok` |
| `socks4a-host-name` | `... -Port 18143 -RawReply <ok> -CurlArgs '-sS','--socks4a','127.0.0.1:18143','http://origin.test:8080/a'` | `04 01 1F90 00000001 00 "origin.test" 00` | 0, `ok` |
| `socks4-rejected` | `... -Port 18144 -RawReply '\x00\x5B\x00\x00\x00\x00\x00\x00' -CurlArgs '-sS','--socks4','127.0.0.1:18144','http://127.0.0.1:8080/a'` | `04 01 1F90 7F000001 00` | 97, `curl: (97) [SOCKS] cannot complete SOCKS4 connection to 0.0.0.0:0. (91), request rejected or failed.` |

The SOCKS5 folders were recorded the same way on 2026-11-05 (BL-559). `<ok5>` is
`'\x05\x00','\x05\x00\x00\x01\x7F\x00\x00\x01\x1F\x90','HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok'`:
the method selection, the success reply (`BND` 127.0.0.1:8080), then the origin's answer. Each
`request.bin` holds the greeting, any RFC 1929 login and the request, then the `GET`.

| Folder | Command line | Curl sent | Exit, stdout or stderr |
| --- | --- | --- | --- |
| `socks5-connect` | `... -Port 18151 -RawReply <ok5> -CurlArgs '-sS','--socks5','127.0.0.1:18151','http://127.0.0.1:8080/a'` | `05 02 00 01`, `05 01 00 01 7F000001 1F90` | 0, `ok` |
| `socks5h-host-name` | `... -Port 18152 -RawReply <ok5> -CurlArgs '-sS','--socks5-hostname','127.0.0.1:18152','http://origin.test:8080/a'` | `05 02 00 01`, `05 01 00 03 0B "origin.test" 1F90` | 0, `ok` |
| `socks5-ipv6` | `... -Port 18153 -RawReply <ok5> -CurlArgs '-sS','--socks5','127.0.0.1:18153','http://[::1]:8080/a'` | `05 02 00 01`, `05 01 00 04 <::1> 1F90` | 0, `ok` |
| `socks5h-user-password` | `... -Port 18154 -RawReply '\x05\x02','\x01\x00',<ok5 without its first reply> -CurlArgs '-sS','--socks5-hostname','127.0.0.1:18154','-U','alice:secret','http://origin.test:8080/a'` | `05 03 00 01 02`, `01 05 "alice" 06 "secret"`, then as `socks5h-host-name` | 0, `ok` |
| `socks5h-user-rejected` | `... -Port 18155 -RawReply '\x05\x02','\x01\x01' -CurlArgs '-sS','--socks5-hostname','127.0.0.1:18155','-U','alice:wrong','http://origin.test:8080/a'` | `05 03 00 01 02`, `01 05 "alice" 05 "wrong"` | 97, `curl: (97) User was rejected by the SOCKS5 server (1 1).` |
| `socks5-no-acceptable-method` | `... -Port 18156 -RawReply '\x05\xFF' -CurlArgs '-sS','--socks5','127.0.0.1:18156','http://127.0.0.1:8080/a'` | `05 02 00 01` | 97, `curl: (97) No authentication method was acceptable.` |
| `socks5-connection-refused` | `... -Port 18157 -RawReply '\x05\x00','\x05\x05\x00\x01\x00\x00\x00\x00\x00\x00' -CurlArgs '-sS','--socks5','127.0.0.1:18157','http://127.0.0.1:8080/a'` | `05 02 00 01`, `05 01 00 01 7F000001 1F90` | 97, `curl: (97) cannot complete SOCKS5 connection to 127.0.0.1. (5)` |
