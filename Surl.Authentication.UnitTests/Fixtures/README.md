# Basic and Bearer credential fixtures (BL-111)

Request heads recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1`. Never from the Curl port (ADR-0003).

Each folder holds the recorder's four files: `request.bin` (the bytes curl sent),
`stdout.bin`, `stderr.txt` and `exitcode.txt`. Every case exited 0 with stdout and stderr
empty: curl sent its credentials unasked, got the `401` and stopped there without opening
the second connection. The files are embedded resources of `Surl.Authentication.UnitTests`,
so the tests read them without touching the file system. `.gitattributes` here keeps git
from rewriting their CRLF line endings.

Recorded on 2026-09-29 from the repository root in Windows PowerShell, `-Port 18111
-Connections 2`, with two `-Response` values: connection 1 got `<401>` followed by one
challenge and `\r\n`, connection 2 the `200` of `Surl.Protocol.Http.UnitTests/Fixtures`'
`get-file`. `<401>` is
`HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n`,
`<basic>` `WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n` and `<bearer>`
`WWW-Authenticate: Bearer realm=\"surl\"\r\n` (ADR-0032, section 4).

| Folder | Challenge | `-CurlArgs` | `Authorization` sent |
| --- | --- | --- | --- |
| `basic` | `<basic>` | `'-sS','--basic','-u','tester:secret','http://127.0.0.1:18111/x'` | `Basic dGVzdGVyOnNlY3JldA==` |
| `user-default` | `<basic>` | `'-sS','-u','tester:secret','http://127.0.0.1:18111/x'` | `Basic dGVzdGVyOnNlY3JldA==`: `-u` alone is Basic, sent unasked |
| `bearer` | `<bearer>` | `'-sS','--oauth2-bearer','tok','http://127.0.0.1:18111/x'` | `Bearer tok` |
| `basic-non-ascii` | `<basic>` | `'-sS','--basic','-u',"t$([char]0xEB)ster:s$([char]0xE9):cr$([char]0x20AC)t",'http://127.0.0.1:18111/x'` | `Basic dOtzdGVyOnPpOmNygHQ=`: the Windows-1252 bytes `74 EB 73 74 65 72 3A 73 E9 3A 63 72 80 74` |
| `basic-utf8-config` | `<basic>` | `'-sS','--basic','-K',<cfg>,'http://127.0.0.1:18111/x'` | `Basic dMOrc3Rlcjpzw6k6Y3Ligqx0`: the UTF-8 bytes of `tëster:sé:cr€t` |

`<cfg>` is a file in the temporary directory holding the one line
`user = "tëster:sé:cr€t"` in UTF-8 without a byte-order mark.

What the two non-ASCII cases show: the reference build sends the user name and password as
the bytes it was given. A command-line argument on Windows reaches it in the ANSI code page
(Windows-1252 here), whatever `charset="UTF-8"` the challenge names; a config file's bytes,
like a command line on Linux and macOS, reach it as UTF-8. Surl reads Basic credentials as
UTF-8, the charset it announces (RFC 7617 section 2.1), so the first is refused and the
second accepted for the account `tëster:sé:cr€t` (ADR-0035).
