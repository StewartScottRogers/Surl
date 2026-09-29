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
