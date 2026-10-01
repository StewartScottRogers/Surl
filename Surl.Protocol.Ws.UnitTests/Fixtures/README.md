# WebSocket upgrade fixtures (BL-301)

Upgrade requests recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw`, each answered with the exact bytes `WsProtocolServer` sends for
it at the tests' fixed clock (2026-09-28 12:00:00 UTC), so each answer was fed to curl before a
test pinned it. Never from the Curl port (ADR-0003).

Each folder holds the recorder's files: `request.bin` (the bytes curl sent), `stdout.bin`,
`stderr.txt`, `exitcode.txt`, and `transcript.txt` (both directions, `> ` from curl, `< ` from
the server, the `101`'s `{WS_ACCEPT}` already replaced with the accept value sent). The files are
embedded resources of `Surl.Protocol.Ws.UnitTests`. `.gitattributes` here keeps git from
rewriting their CRLF line endings.

Recorded on 2026-09-30 from the repository root, in PowerShell, with `$d` being
`Surl.Protocol.Ws.UnitTests\Fixtures` and `$date` being `Date: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n`:

| Folder | Reply | curl arguments | Exit |
| --- | --- | --- | --- |
| `upgrade-101` | `HTTP/1.1 101 Switching Protocols\r\n$date` `Upgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {WS_ACCEPT}\r\n\r\n\x88\x00` | `-sS`, `ws://127.0.0.1:18301/chat` | 0 |
| `basic-401` | `HTTP/1.1 401 Unauthorized\r\n$date` `Content-Length: 0\r\nWWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n\r\n` | `-sS`, `-u`, `tester:secret`, `ws://127.0.0.1:18301/chat` | 22 |
| `head-405` | `HTTP/1.1 405 Method Not Allowed\r\n$date` `Allow: GET\r\nContent-Length: 0\r\nConnection: close\r\n\r\n` | `-sS`, `-I`, `ws://127.0.0.1:18301/chat` | 22 |
| `version-8-426` | `HTTP/1.1 426 Upgrade Required\r\n$date` `Sec-WebSocket-Version: 13\r\nContent-Length: 0\r\nConnection: close\r\n\r\n` | `-sS`, `-H`, `Sec-WebSocket-Version: 8`, `ws://127.0.0.1:18301/chat` | 22 |

Each as `.\Record-CurlExchange.ps1 -Port 18301 -Raw -RawReply <reply> -CurlArgs <arguments> -OutDirectory "$d\<folder>"`.
curl completed the `101` (exit 0, nothing written: the empty `CLOSE` carries no payload) and
answered every refusal `curl: (22) Refused WebSocket upgrade: <status>`, as ADR-0071 measured.

## After the upgrade (BL-302)

Recorded on 2026-09-30 the same way, with `$h` being the `101` of `upgrade-101` above
(`HTTP/1.1 101 Switching Protocols\r\n$date` `Upgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {WS_ACCEPT}\r\n\r\n`):

| Folder | Replies, in order | curl arguments | Exit |
| --- | --- | --- | --- |
| `file-chat` | `$h\x82\x04chat\x88\x00` | `-sS`, `ws://127.0.0.1:18301/chat` | 0 |
| `ping-pong` | `$h\x89\x04ping`, then `\x88\x00` | `-sS`, `ws://127.0.0.1:18301/chat` | 0 |

`file-chat` is what `WsProtocolServer` sends for `/chat` holding `chat`: curl wrote `chat`
(`stdout.bin`). In `ping-pong` curl answered the `PING` with a masked `PONG` carrying `ping`
(`\x8A\x84` and a masking key, after the head in `request.bin`), and the tests replay it to
show a client `PONG` is ignored (ADR-0071 decision 6).

The other refusal cases (no `Host`, `HTTP/1.0`, no `Upgrade`, no `Connection: Upgrade`, a body,
a missing or malformed key, a missing version, an unknown path) are the recorded `upgrade-101`
request with one line changed by the test, since curl does not send them on its own.
