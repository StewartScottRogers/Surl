# DICT fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw` (BL-029). Never from the Curl port (ADR-0003).

Each case was fed the exact replies `DictProtocolServer` sends for it, with exchange id 1,
before any test pinned them: the banner first, as soon as curl connected, then everything
Surl answers to the three lines curl sends at once (`CLIENT libcurl 8.21.0`, the command,
`QUIT`). Every case exited 0 with an empty `stderr.txt`, and curl printed every byte it
received, so `stdout.bin` is the whole of Surl's reply that curl accepted.

Each folder holds the recorder's five files: `request.bin` (the bytes curl sent),
`transcript.txt` (both directions), `stdout.bin`, `stderr.txt` and `exitcode.txt`. They
are embedded resources of `Surl.Protocol.Dict.UnitTests`, so the tests read them without
touching the file system. `.gitattributes` here keeps git from rewriting their CRLF line
endings. `DictProtocolServerTests` replays each `request.bin` against a content store
holding `hello` (`A greeting.\n`), `help` and `world`, and asserts the bytes Surl writes
equal `stdout.bin`.

Recorded on 2026-09-28 from the repository root, in Windows PowerShell, with
`$banner = '220 surl DICT server <mime> <1@surl>\r\n'`:

| Folder | curl sends | Command line |
| --- | --- | --- |
| `define-hello` | `DEFINE ! hello` | `.\Record-CurlExchange.ps1 -Port 18628 -Raw -RawReplyFirst -RawReply $banner,'250 ok\r\n150 1 definitions retrieved\r\n151 \"hello\" surl \"Files served by surl\"\r\nA greeting.\r\n.\r\n250 ok\r\n221 bye\r\n' -CurlArgs '-sS','dict://127.0.0.1:18628/d:hello' -OutDirectory Surl.Protocol.Dict.UnitTests\Fixtures\define-hello` |
| `match-hel` | `MATCH ! . hel` | `.\Record-CurlExchange.ps1 -Port 18628 -Raw -RawReplyFirst -RawReply $banner,'250 ok\r\n152 2 matches found\r\nsurl \"hello\"\r\nsurl \"help\"\r\n.\r\n250 ok\r\n221 bye\r\n' -CurlArgs '-sS','dict://127.0.0.1:18628/m:hel' -OutDirectory Surl.Protocol.Dict.UnitTests\Fixtures\match-hel` |
| `bare-hello` | `hello` | `.\Record-CurlExchange.ps1 -Port 18628 -Raw -RawReplyFirst -RawReply $banner,'250 ok\r\n500 unknown command\r\n221 bye\r\n' -CurlArgs '-sS','dict://127.0.0.1:18628/hello' -OutDirectory Surl.Protocol.Dict.UnitTests\Fixtures\bare-hello` |
| `define-missing` | `DEFINE ! missing` | `.\Record-CurlExchange.ps1 -Port 18628 -Raw -RawReplyFirst -RawReply $banner,'250 ok\r\n552 no match\r\n221 bye\r\n' -CurlArgs '-sS','dict://127.0.0.1:18628/d:missing' -OutDirectory Surl.Protocol.Dict.UnitTests\Fixtures\define-missing` |
| `show-db` | `show db` | `.\Record-CurlExchange.ps1 -Port 18628 -Raw -RawReplyFirst -RawReply $banner,'250 ok\r\n110 1 databases present\r\nsurl \"Files served by surl\"\r\n.\r\n250 ok\r\n221 bye\r\n' -CurlArgs '-sS','dict://127.0.0.1:18628/show:db' -OutDirectory Surl.Protocol.Dict.UnitTests\Fixtures\show-db` |

A bare path is sent as it stands, with each `:` turned into a space, so
`dict://host/hello` sends the command `hello`, which RFC 2229 does not define and Surl
answers 500.
