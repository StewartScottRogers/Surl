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
| `default-get` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\default-get -CurlArgs 'http://127.0.0.1:18017/'` |
| `head` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\head -CurlArgs '-I','http://127.0.0.1:18017/file.txt'` |
| `http10` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\http10 -CurlArgs '-0','http://127.0.0.1:18017/file.txt'` |
| `custom-header` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\custom-header -CurlArgs '-H','X-Custom: a b','http://127.0.0.1:18017/file.txt'` |
| `path-as-is` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\path-as-is -CurlArgs '--path-as-is','http://127.0.0.1:18017/a/../b'` |
| `query-string` | `.\Record-CurlExchange.ps1 -Port 18017 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\query-string -CurlArgs 'http://127.0.0.1:18017/file.txt?x=1&y=%20'` |
| `two-urls` | `.\Record-CurlExchange.ps1 -Port 18017 -Connections 2 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\two-urls -CurlArgs 'http://127.0.0.1:18017/one','http://127.0.0.1:18017/two'` |

In `two-urls` the recorder closes each connection after its response, so curl sends the
second head on a new connection; `request.bin` holds both heads in the order accepted.
`stderr.txt` holds curl's progress meter, whose timings differ from run to run.

## Response fixtures (BL-018, re-recorded and extended by BL-050)

Each response the HTTP server sends in these cases was fed to the same pinned build with
`Record-CurlExchange.ps1 -Response` before any test pinned it. Each folder holds the
recorder's four files plus `response.bin`, the exact response bytes sent (decoded from
`-Response`, one byte per character), which the tests compare the server's output with.
The fixed clock is 2026-09-28 12:00:00 UTC and `file.txt` was last written 2026-09-01
08:30:00 UTC. BL-050 added `Server: surl` to every response (ADR-0019), so it re-recorded
the first three on 2026-09-29 and recorded the rest the same day, from the repository
root in Windows PowerShell. `<upload>` is a 2048-byte file of `a` bytes in the temporary
directory; `expect-continue-413` was recorded with `-RespondAfterBodyBytes 0`, so the
413 went out as soon as the head arrived, and its `request.bin` is the 181-byte head
alone: curl sent no body byte.

| Folder | Exit code | stdout.bin | Command line |
| --- | --- | --- | --- |
| `get-file` | 0 | the 17-byte body `Hello from Surl.\n` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\get-file -Response 'HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nLast-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n\r\nHello from Surl.\n' -CurlArgs 'http://127.0.0.1:18050/file.txt'` |
| `head-file` | 0 | the response head | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\head-file -Response 'HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nLast-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n\r\n' -CurlArgs '-I','http://127.0.0.1:18050/file.txt'` |
| `not-found-fail` | 22 | empty; stderr ends `curl: (22) The requested URL returned error: 404` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\not-found-fail -Response 'HTTP/1.1 404 Not Found\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n\r\n' -CurlArgs '--fail','http://127.0.0.1:18050/missing.txt'` |
| `head-timeout-408` | 22 | empty; stderr `curl: (22) The requested URL returned error: 408` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\head-timeout-408 -Response 'HTTP/1.1 408 Request Timeout\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','http://127.0.0.1:18050/file.txt'` |
| `head-too-large-431` | 22 | empty; stderr `curl: (22) The requested URL returned error: 431` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\head-too-large-431 -Response 'HTTP/1.1 431 Request Header Fields Too Large\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','http://127.0.0.1:18050/file.txt'` |
| `upload-too-large-413` | 22 | empty; stderr `curl: (22) The requested URL returned error: 413` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\upload-too-large-413 -Response 'HTTP/1.1 413 Content Too Large\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','--data-binary','@<upload>','http://127.0.0.1:18050/file.txt'` |
| `expect-continue-413` | 22 | empty; stderr `curl: (22) The requested URL returned error: 413` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\expect-continue-413 -RespondAfterBodyBytes 0 -Response 'HTTP/1.1 413 Content Too Large\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','-H','Expect: 100-continue','--data-binary','@<upload>','http://127.0.0.1:18050/file.txt'` |
| `refusal-503` | 22 | empty; stderr `curl: (22) The requested URL returned error: 503` | `.\Record-CurlExchange.ps1 -Port 18050 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\refusal-503 -Response 'HTTP/1.1 503 Service Unavailable\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','http://127.0.0.1:18050/file.txt'` |

## Drain and invalid Content-Length fixtures (BL-061)

Recorded on 2026-09-29 the same way, from the repository root in Windows PowerShell, with
the same pinned build (ADR-0024). `response.bin` is again the decoded `-Response`. In
`post-refused-405` curl sent the one-byte body `x` with the head; in
`invalid-content-length-400` it sent `Content-Length: abc` as given, and no body.

| Folder | Exit code | stdout.bin | Command line |
| --- | --- | --- | --- |
| `post-refused-405` | 22 | empty; stderr `curl: (22) The requested URL returned error: 405` | `.\Record-CurlExchange.ps1 -Port 18061 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\post-refused-405 -Response 'HTTP/1.1 405 Method Not Allowed\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nAllow: GET, HEAD\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','-d','x','http://127.0.0.1:18061/file.txt'` |
| `invalid-content-length-400` | 22 | empty; stderr `curl: (22) The requested URL returned error: 400` | `.\Record-CurlExchange.ps1 -Port 18061 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\invalid-content-length-400 -Response 'HTTP/1.1 400 Bad Request\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','--fail','-H','Content-Length: abc','http://127.0.0.1:18061/file.txt'` |

## 100 Continue fixture (BL-083)

Recorded on 2026-09-29 the same way, from the repository root in Windows PowerShell, with
the same pinned build (ADR-0027), using `-InterimResponse`, which sends its bytes as soon as
the head has arrived and before the body is waited for. `<upload>` is a 16-byte file of `a`
bytes in the temporary directory. `request.bin` is curl's 178-byte head followed by its
16-byte body, sent at once after the `100 Continue`; curl exited 0 after 105 ms, where the
same run without `-InterimResponse` took 1037 ms, curl's one-second `--expect100-timeout`.
`response.bin` holds every byte the server sent: the `100 Continue` followed by the 200 of
`get-file`.

| Folder | Exit code | stdout.bin | Command line |
| --- | --- | --- | --- |
| `expect-continue-get` | 0 | the 17-byte body `Hello from Surl.\n` | `.\Record-CurlExchange.ps1 -Port 18083 -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\expect-continue-get -InterimResponse 'HTTP/1.1 100 Continue\r\n\r\n' -Response 'HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nLast-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n\r\nHello from Surl.\n' -CurlArgs '-sS','-X','GET','-H','Expect: 100-continue','--data-binary','@<upload>','http://127.0.0.1:18083/file.txt'` |

## Authentication fixtures (BL-114)

Recorded on 2026-09-29 from the repository root in Windows PowerShell, with the same pinned
build and `-Connections 2` (ADR-0032, section 4). Every run gave two `-Response` values:
connection 1 got the `401` (or `403`) below and connection 2 the `200` of `get-file`, so a
client that logs in after the challenge is served. `<401>` is
`HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n`,
`<digest>` the three fields
`WWW-Authenticate: Digest realm=\"surl\", qop=\"auth\", algorithm=<A>, nonce=\"abc\"\r\n` for
`MD5`, `SHA-256` and `SHA-512-256` in that order, `<basic>`
`WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n`, `<bearer>`
`WWW-Authenticate: Bearer realm=\"surl\"\r\n`, `<403>`
`HTTP/1.1 403 Forbidden\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n`
and `<200>` the `-Response` of `get-file`. `response.bin` is every byte the server sends to
the recorded request on one kept-alive connection: in `digest-401-then-200` the `401`
followed by the `200`, answering the two heads of `request.bin`.

| Folder | Exit code | What curl did | Command line |
| --- | --- | --- | --- |
| `basic-401` | 0 | sent `Authorization: Basic` unasked, stopped at the `401`; stdout empty | `.\Record-CurlExchange.ps1 -Port 18114 -Connections 2 -OutDirectory ...\basic-401 -Response '<401><digest><basic>\r\n','<200>' -CurlArgs '-sS','-u','tester:secret','--basic','http://127.0.0.1:18114/file.txt'` |
| `digest-401-then-200` | 0 | sent no `Authorization`, then after the `401` a new connection with `Authorization: Digest ... algorithm=MD5 ...`; stdout the 17-byte body | `... -OutDirectory ...\digest-401-then-200 -Response '<401><digest>\r\n','<200>' -CurlArgs '-sS','--digest','-u','tester:secret','http://127.0.0.1:18114/file.txt'` |
| `bearer-401` | 0 | sent `Authorization: Bearer tok` unasked, stopped at the `401`; stdout empty | `... -OutDirectory ...\bearer-401 -Response '<401><bearer>\r\n','<200>' -CurlArgs '-sS','--oauth2-bearer','tok','http://127.0.0.1:18114/file.txt'` |
| `anonymous-401-fail` | 22 | stderr `curl: (22) The requested URL returned error: 401` | `... -OutDirectory ...\anonymous-401-fail -Response '<401><digest>\r\n','<200>' -CurlArgs '-sS','-f','http://127.0.0.1:18114/file.txt'` |
| `basic-plaintext-403` | 22 | sent `Authorization: Basic` unasked; stderr `curl: (22) The requested URL returned error: 403` | `... -OutDirectory ...\basic-plaintext-403 -Response '<403>','<200>' -CurlArgs '-sS','-f','-u','tester:secret','http://127.0.0.1:18114/file.txt'` |

## HTTP/2 frame fixtures (BL-367)

Recorded on 2026-11-04 from the repository root in PowerShell, with the stunnel/static-curl
8.21.0 build pinned in `UpstreamCurlBuilds.json` (SHA-256
`589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`), the build ADR-0081
decision 1 names for HTTP/2 on Windows, with `Record-CurlExchange.ps1 -Raw`. The first reply
is an empty `SETTINGS`; the second is the `SETTINGS` ACK, a `HEADERS` on stream 1 with
`END_STREAM` and `END_HEADERS` and the block `88` (`:status 200`), and a `DATA` frame that
curl ignores because the stream had already ended. `request.bin` is the 165 bytes curl sent:
the connection preface, `SETTINGS`, the connection `WINDOW_UPDATE`, `HEADERS`, two stream-1
`WINDOW_UPDATE`s, its `SETTINGS` ACK and `GOAWAY NO_ERROR "shutdown\0"`, as ADR-0081's
"Measured" table records. `transcript.txt` holds both directions.

| Folder | Exit code | stdout.bin | Command line |
| --- | --- | --- | --- |
| `h2-prior-knowledge-get` | 0 | `HTTP/2 200 \r\n\r\n` | `.\Record-CurlExchange.ps1 -Port 18367 -Raw -Curl 'C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe' -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\h2-prior-knowledge-get -RawReply '\x00\x00\x00\x04\x00\x00\x00\x00\x00','\x00\x00\x00\x04\x01\x00\x00\x00\x00\x00\x00\x01\x01\x05\x00\x00\x00\x01\x88\x00\x00\x02\x00\x01\x00\x00\x00\x01hi' -CurlArgs '-sS','-i','--http2-prior-knowledge','http://127.0.0.1:18367/'` |
| `h2-prior-knowledge-get-file` | 0 | `HTTP/2 200 \r\n\r\n` | `.\Record-CurlExchange.ps1 -Port 18050 -Raw -Curl 'C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe' -OutDirectory Protocols\Http\Surl.Protocol.Http.UnitTests\Fixtures\h2-prior-knowledge-get-file -RawReply '\x00\x00\x00\x04\x00\x00\x00\x00\x00','\x00\x00\x00\x04\x01\x00\x00\x00\x00\x00\x00\x01\x01\x05\x00\x00\x00\x01\x88' -CurlArgs '-sS','-i','--http2-prior-knowledge','http://127.0.0.1:18050/file.txt'` |

`h2-prior-knowledge-get-file` (BL-368) is the same exchange for the content-store file
`get-file` fetches over HTTP/1.1, on the same port, so its 173 request bytes - `:path /file.txt`,
`:authority 127.0.0.1:18050` - are what the HTTP/2 server's replay tests answer and compare with
the HTTP/1.1 answer. The second reply is the `SETTINGS` ACK and the `HEADERS` alone.

## HTTP/3 client Initial fixture (BL-383)

`h3-client-initial/static-8.21.0-win.datagrams.bin` is a byte-for-byte copy of
`Surl.Quic.UnitTests/Fixtures/ClientInitial/static-8.21.0-win.datagrams.bin`: the four Initial
datagrams the stunnel/static-curl 8.21.0 build for Windows (SHA-256
`589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`) sent with `--http3-only`,
recorded on 2026-11-04 by `Record-CurlExchange.ps1 -QuicInitial` (BL-363; ADR-0082 "Measured"),
each prefixed with its two-byte big-endian length. The HTTP/3 tests' QUIC client
(`UnitTestQuicClient`, copied from `Surl.Quic.UnitTests`) starts every connection from it, with its
own x25519 key share in place of curl's. The HTTP/3 frames the tests send after the handshake are
written from RFC 9114 and RFC 9204: the recorder answers nothing, so no curl has yet sent a
request stream to Surl; BL-385 records curl's against a live `surl`.

## Range and PUT fixtures (BL-723)

Recorded on 2026-11-05 from the repository root in PowerShell, with the same pinned build (ADR-0132).
`response.bin` is the decoded `-Response` each case was fed, which the tests compare the server's
output with; the clock and `file.txt` are those of the response fixtures above, and every case exited
0 with an empty `stderr.txt`. `<lm>` is `Tue, 01 Sep 2026 08:30:00 GMT`, `<head>` is
`HTTP/1.1 206 Partial Content\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nLast-Modified: <lm>\r\nContent-Type: application/octet-stream\r\n`,
and `<upload>` is a 16-byte file of `a` bytes in the temporary directory. In the `put-*` cases curl
sent the 16-byte body with the head, and no `Expect`.

| Folder | stdout.bin | Command line |
| --- | --- | --- |
| `range-206` | `Hello from` | `.\Record-CurlExchange.ps1 -Port 18723 -OutDirectory <folder> -Response '<head>Content-Range: bytes 0-9/17\r\nContent-Length: 10\r\n\r\nHello from' -CurlArgs '-sS','-r','0-9','http://127.0.0.1:18723/file.txt'` |
| `range-suffix-206` | `url.\n` | `... -Response '<head>Content-Range: bytes 12-16/17\r\nContent-Length: 5\r\n\r\nurl.\n' -CurlArgs '-sS','-r','-5','http://127.0.0.1:18723/file.txt'` |
| `range-unsatisfiable-416` | empty | `... -Response 'HTTP/1.1 416 Range Not Satisfiable\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Range: bytes */17\r\nContent-Length: 0\r\n\r\n' -CurlArgs '-sS','-r','100-200','http://127.0.0.1:18723/file.txt'` |
| `range-multiple-200` | the 17-byte file | `... -Response` the `get-file` response `-CurlArgs '-sS','-r','0-1,3-4','http://127.0.0.1:18723/file.txt'` |
| `range-if-range-206` | `Hello from` | `... -Response` as `range-206` `-CurlArgs '-sS','-r','0-9','-H','If-Range: <lm>','http://127.0.0.1:18723/file.txt'` |
| `put-created-201` | empty | `... -Response 'HTTP/1.1 201 Created\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n\r\n' -CurlArgs '-sS','--fail','-u','user:secret','-T','<upload>','http://127.0.0.1:18723/up.bin'` |
| `put-replaced-204` | empty | `... -Response 'HTTP/1.1 204 No Content\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n\r\n' -CurlArgs` as `put-created-201` |
