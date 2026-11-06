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

`ftp://` forwarding (BL-594, ADR-0088 Amendment 4), recorded on 2026-11-06 the same way.
`response.bin` is what `Surl.Protocol.HttpProxy` writes to curl after fetching over FTP: `<200f>` is
`'HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Type: application/octet-stream\r\nTransfer-Encoding: chunked\r\nVia: 1.1 surl\r\n\r\n6\r\nhello\n\r\n0\r\n\r\n'`,
and `<200l>` the same with `Content-Type: text/plain` and the chunk `7\r\na.txt\r\n\r\n`. Both
credential forms put `user:pass` in the target's userinfo **and** send `Authorization: Basic`.

| Folder | Command line | Exit, stdout or stderr |
| --- | --- | --- |
| `ftp-forward-file` | `... -Port 18311 -Response <200f> -CurlArgs '-sS','-x','http://127.0.0.1:18311','ftp://user:pass@origin.test/a.txt'` | 0, `hello` |
| `ftp-forward-listing` | `... -Port 18312 -Response <200l> -CurlArgs '-sS','-u','user:pass','-x','http://127.0.0.1:18312','ftp://origin.test/dir/'` | 0, `a.txt` |
| `ftp-forward-502` | `... -Port 18313 -Response <502> -CurlArgs '-sS','-x','http://127.0.0.1:18313','ftp://origin.test/a.txt'` | 0, `502 Bad Gateway`; no credentials sent |

The HTTPS proxy (BL-554, ADR-0088 Amendment 2): the recorder played an HTTPS proxy with `-Tls`
and offered exactly the ALPN protocol ID the proxy listener offers, `http/1.1`; `alpn.txt` holds
what was agreed. `request.bin` is the plaintext read inside the TLS, and `response.bin` is
`forward-get`'s, the canned reply.

| Folder | Command line | Exit, stdout or stderr |
| --- | --- | --- |
| `https-proxy-forward` | `... -Port 18144 -Tls -TlsAlpn 'http/1.1' -Response 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\nVia: 1.1 surl\r\n\r\nok' -CurlArgs '-sS','-x','https://127.0.0.1:18144','--proxy-insecure','http://origin.test/a?b'` | 0, `ok`; ALPN `http/1.1` |
| `https-proxy-http2-offered` | the same with `-Curl` the pinned static 8.21.0 build (`589C8E4D…2648`, which has HTTP/2), `-Port 18145` and `'--proxy-http2'` before `-x` | 0, `ok`; ALPN `http/1.1`, curl having offered `h2` first |

`Proxy-Authorization` (BL-553, ADR-0088 decision 5 and Amendment 3): the recorder answered with the
bytes Surl's proxy writes, so `response.bin` is each canned reply, concatenated in order. With
`-ResponsesPerConnection 2` or `3`, `request-1.bin`... hold each request alone, the last of a
`CONNECT` case being the `GET` sent inside the tunnel. `<407>` is
`'HTTP/1.1 407 Proxy Authentication Required\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n<challenges>Content-Type: text/plain; charset=us-ascii\r\nContent-Length: 34\r\n\r\n407 Proxy Authentication Required\n'`,
where `<challenges>` is `Proxy-Authenticate: Digest realm="surl", qop="auth", algorithm=MD5, nonce="abc"\r\nProxy-Authenticate: Basic realm="surl", charset="UTF-8"\r\n`,
or for NTLM the one field `Proxy-Authenticate: NTLM TlRMTVNTUAACAAAAAAAAADgAAAABgggAASNFZ4mrze8AAAAAAAAAAAAAAAA4AAAAAAAAAAAAAAA=\r\n`
(a CHALLENGE_MESSAGE written from MS-NLMP 2.2.1.2 with no target name or information, flags
`0x00088201`, server challenge `0123456789ABCDEF`). `<ok>` is `forward-get`'s reply, `<est>` the
`CONNECT` reply and `<tok>` `'HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok'`. The
AUTHENTICATE_MESSAGEs carry the recording machine's workstation name, as curl sends it.

| Folder | Command line | Responses | Exit, stdout or stderr |
| --- | --- | --- | --- |
| `proxy-unauthorized-407` | `... -Port 18262 -CurlArgs '-sS','-x','http://127.0.0.1:18262','http://origin.test/a'` | `<407>` | 0, the `407`'s body |
| `proxy-connect-unauthorized-407` | `... -Port 18263 -CurlArgs '-sS','-p','-x','http://127.0.0.1:18263','http://origin.test:8080/a'` | `<407>` | 7, `curl: (7) CONNECT tunnel failed, response 407` |
| `proxy-basic-forward` | `... -Port 18264 -CurlArgs '-sS','-U','u:p','-x','http://127.0.0.1:18264','http://origin.test/a'` | `<ok>` | 0, `ok` |
| `proxy-basic-connect` | `... -Port 18265 -ResponsesPerConnection 2 -CurlArgs '-sS','-p','-U','u:p','-x','http://127.0.0.1:18265','http://origin.test:8080/a'` | `<est>`, `<tok>` | 0, `ok` |
| `proxy-anyauth-digest-forward` | `... -Port 18266 -ResponsesPerConnection 2 -CurlArgs '-sS','--proxy-anyauth','-U','u:p','-x','http://127.0.0.1:18266','http://origin.test/a?b'` | `<407>`, `<ok>` | 0, `ok`; Digest `uri="/a?b"` |
| `proxy-digest-connect` | `... -Port 18267 -ResponsesPerConnection 3 -CurlArgs '-sS','-p','--proxy-digest','-U','u:p','-x','http://127.0.0.1:18267','http://origin.test:8080/a'` | `<407>`, `<est>`, `<tok>` | 0, `ok`; Digest `uri="origin.test:8080"` |
| `proxy-ntlm-forward` | `... -Port 18268 -ResponsesPerConnection 2 -CurlArgs '-sS','-m','10','--proxy-ntlm','-U','u:p','-x','http://127.0.0.1:18268','http://origin.test/a'` | NTLM `<407>`, `<ok>` | 0, `ok` |
| `proxy-ntlm-connect` | `... -Port 18269 -ResponsesPerConnection 3 -CurlArgs '-sS','-m','10','-p','--proxy-ntlm','-U','u:p','-x','http://127.0.0.1:18269','http://origin.test:8080/a'` | NTLM `<407>`, `<est>`, `<tok>` | 0, `ok` |
