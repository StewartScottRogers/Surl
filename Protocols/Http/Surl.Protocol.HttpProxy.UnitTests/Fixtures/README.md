# HTTP proxy fixtures

Recorded on 2026-11-05 (BL-552) from upstream curl 8.21.0, the win-x64 reference build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1`, from the repository root in PowerShell. Never from the Curl port
(ADR-0003). The recorder played the proxy: curl was pointed at it with `-x`, and the origin
`origin.test` was never resolved.

Each folder holds the recorder's files - `request.bin` (the bytes curl sent; with
`-ResponsesPerConnection 2` also `request-1.bin` and `request-2.bin`), `stdout.bin`,
`stderr.txt` and `exitcode.txt` - plus `response.bin`: the exact bytes the canned reply sent,
which is what `Surl.Protocol.HttpProxy` writes to curl for that exchange at
2026-09-28 12:00:00 UTC (ADR-0088 decisions 2 to 4). The files are embedded resources of
`Surl.Protocol.HttpProxy.UnitTests`; `.gitattributes` keeps git from rewriting their CRLF line
endings.

`<502>` is `'HTTP/1.1 502 Bad Gateway\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Type: text/plain; charset=us-ascii\r\nContent-Length: 16\r\n\r\n502 Bad Gateway\n'`;
`<403>` is the same with `403 Forbidden` and `Content-Length: 14`.

| Folder | Command line | Exit, stdout or stderr |
| --- | --- | --- |
| `forward-get` | `.\Record-CurlExchange.ps1 -Port 18128 -OutDirectory ...\forward-get -Response 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\nVia: 1.1 surl\r\n\r\nok' -CurlArgs '-sS','-x','http://127.0.0.1:18128','http://origin.test/a?b'` | 0, `ok` |
| `connect-tunnel` | `... -Port 18129 -ResponsesPerConnection 2 -Response 'HTTP/1.1 200 Connection established\r\n\r\n','HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok' -CurlArgs '-sS','-p','-x','http://127.0.0.1:18129','http://origin.test:8080/a'` | 0, `ok`; `request-2.bin` is the `GET` curl sent inside the tunnel |
| `forward-unreachable-502` | `... -Port 18130 -Response <502> -CurlArgs '-sS','-x','http://127.0.0.1:18130','http://origin.test/a'` | 0, `502 Bad Gateway` |
| `connect-refused-502` | `... -Port 18131 -Response <502> -CurlArgs '-sS','-p','-x','http://127.0.0.1:18131','http://origin.test:8080/a'` | 7, `curl: (7) CONNECT tunnel failed, response 502` |
| `connect-not-allowed-403` | `... -Port 18132 -Response <403> -CurlArgs '-sS','-p','-x','http://127.0.0.1:18132','http://origin.test:25/a'` | 7, `curl: (7) CONNECT tunnel failed, response 403` |
