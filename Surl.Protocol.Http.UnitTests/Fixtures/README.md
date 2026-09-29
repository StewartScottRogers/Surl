# HTTP request-head fixtures

Request heads recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1` in its default HTTP mode and its default canned response
(`HTTP/1.1 200 OK`, `Content-Length: 0`). Never from the Curl port (ADR-0003).

Each folder holds the recorder's four files: `request.bin` (the bytes curl sent),
`stdout.bin`, `stderr.txt` and `exitcode.txt`. Every case exited 0. The files are
embedded resources of `Surl.Protocol.Http.UnitTests`, so the tests read them without
touching the file system. `.gitattributes` here keeps git from rewriting their CRLF line
endings.

Recorded on 2026-09-28 from the repository root, in Windows PowerShell:

| Folder | Command line |
| --- | --- |
| `default-get` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\default-get -CurlArgs 'http://127.0.0.1:18017/'` |
| `head` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\head -CurlArgs '-I','http://127.0.0.1:18017/file.txt'` |
| `http10` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\http10 -CurlArgs '-0','http://127.0.0.1:18017/file.txt'` |
| `custom-header` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\custom-header -CurlArgs '-H','X-Custom: a b','http://127.0.0.1:18017/file.txt'` |
| `path-as-is` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\path-as-is -CurlArgs '--path-as-is','http://127.0.0.1:18017/a/../b'` |
| `query-string` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\query-string -CurlArgs 'http://127.0.0.1:18017/file.txt?x=1&y=%20'` |
| `two-urls` | `.\Record-CurlExchange.ps1 -Port 18017 -Connections 2 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\two-urls -CurlArgs 'http://127.0.0.1:18017/one','http://127.0.0.1:18017/two'` |

In `two-urls` the recorder closes each connection after its response, so curl sends the
second head on a new connection; `request.bin` holds both heads in the order accepted.
`stderr.txt` holds curl's progress meter, whose timings differ from run to run.

## Response fixtures (BL-018)

Each response the HTTP server sends for these three cases was fed to the same pinned
build with `Record-CurlExchange.ps1 -Response` before any test pinned it, and curl
accepted it. Each folder holds the recorder's four files plus `response.bin`, the exact
response bytes sent (decoded from `-Response`, one byte per character), which
`HttpProtocolServerTests` compares the server's output with. The fixed clock is
2026-09-28 12:00:00 UTC and `file.txt` was last written 2026-09-01 08:30:00 UTC.

| Folder | Exit code | stdout.bin | Command line |
| --- | --- | --- | --- |
| `get-file` | 0 | the 17-byte body `Hello from Surl.\n` | `.\Record-CurlExchange.ps1 -Port 18018 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\get-file -Response 'HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nLast-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n\r\nHello from Surl.\n' -CurlArgs 'http://127.0.0.1:18018/file.txt'` |
| `head-file` | 0 | the response head | `.\Record-CurlExchange.ps1 -Port 18018 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\head-file -Response 'HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nLast-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n\r\n' -CurlArgs '-I','http://127.0.0.1:18018/file.txt'` |
| `not-found-fail` | 22 | empty; stderr ends `curl: (22) The requested URL returned error: 404` | `.\Record-CurlExchange.ps1 -Port 18018 -OutDirectory Surl.Protocol.Http.UnitTests\Fixtures\not-found-fail -Response 'HTTP/1.1 404 Not Found\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nContent-Length: 0\r\n\r\n' -CurlArgs '--fail','http://127.0.0.1:18018/missing.txt'` |

Recorded on 2026-09-28 from the repository root, in Windows PowerShell.
