# TFTP fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Tftp`, never from the Curl port (ADR-0003). In each case the
recorder answered curl from a new UDP port with the datagrams the TFTP server sends for
the same request (ADR-0013): its own OACK and DATA for a read, and `-TftpReply` for an
ERROR.

Each folder holds the recorder's files: `request.bin` (every datagram curl sent, one after
another), `transcript.txt` (one line per datagram in either direction, `>` for curl's and
`<` for the server's, bytes outside printable ASCII as `\xHH` and a backslash as `\\`),
`stdout.bin`, `stderr.txt`, `exitcode.txt` and `upload.bin` (empty: no case uploads).
`TftpProtocolServerTests` replays the `>` datagrams through a scripted datagram flow and
asserts the server sends exactly the `<` datagrams, and that the DATA payloads of each read
equal `stdout.bin`. The files are embedded resources of `Surl.Protocol.Tftp.UnitTests`, so
the tests read them without touching the file system; `.gitattributes` here keeps git from
rewriting their line endings.

Recorded on 2026-09-28 from the repository root, in Windows PowerShell, with
`$big = ('0123456789abcdef' * 70) + 'Surl'` (1124 bytes) and
`$exact = '0123456789abcdef' * 32` (512 bytes):

| Folder | Exit | What curl did | Command line |
| --- | --- | --- | --- |
| `default-read` | 0 | RRQ with `tsize 0`, `blksize 512`, `timeout 6`; accepted the OACK `tsize 17 blksize 512 timeout 6`, ACK 0, then DATA 1 | `.\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpData 'Hello from Surl.\n' -CurlArgs '-sS','tftp://127.0.0.1:18069/file.txt' -OutDirectory Surl.Protocol.Tftp.UnitTests\Fixtures\default-read` |
| `blksize-1024-read` | 0 | RRQ with `blksize 1024`; OACK, then DATA 1 (1024 bytes) and DATA 2 (100 bytes) | `.\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpData $big -CurlArgs '-sS','--tftp-blksize','1024','tftp://127.0.0.1:18069/big.txt' -OutDirectory Surl.Protocol.Tftp.UnitTests\Fixtures\blksize-1024-read` |
| `no-options-read` | 0 | RRQ with no options; DATA 1 first, no OACK | `.\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpData 'Hello from Surl.\n' -CurlArgs '-sS','--tftp-no-options','tftp://127.0.0.1:18069/file.txt' -OutDirectory Surl.Protocol.Tftp.UnitTests\Fixtures\no-options-read` |
| `missing-file` | 68 | RRQ with options answered by ERROR 1; stderr `curl: (68) TFTP: File Not Found` | `.\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpReply '\x00\x05\x00\x01File not found\x00' -CurlArgs '-sS','tftp://127.0.0.1:18069/missing.txt' -OutDirectory Surl.Protocol.Tftp.UnitTests\Fixtures\missing-file` |
| `exactly-512-bytes` | 0 | DATA 1 of 512 bytes, then the empty DATA 2 that ends the transfer (RFC 1350 section 6) | `.\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpData $exact -CurlArgs '-sS','tftp://127.0.0.1:18069/exact-512.txt' -OutDirectory Surl.Protocol.Tftp.UnitTests\Fixtures\exactly-512-bytes` |
| `write-refused` | 69 | WRQ with `tsize 6` answered by ERROR 2; stderr `curl: (69) TFTP: Access Violation` | `.\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpReply '\x00\x05\x00\x02Access violation\x00' -CurlArgs '-sS','-T',$tmp,'tftp://127.0.0.1:18069/upload.txt' -OutDirectory Surl.Protocol.Tftp.UnitTests\Fixtures\write-refused`, with `$tmp` a 6-byte file |

What else was measured the same way on 2026-09-28, and not kept as a fixture: curl sends
the URL path percent-decoded and without its first `/`, so
`tftp://127.0.0.1:18069/sub/a%20b%25c%C3%A9.txt` sends `sub/a b%c` `\xC3\xA9` `.txt`;
and with `;mode=netascii` it sends mode `netascii` and writes the DATA bytes
`a CR LF b CR NUL c` to stdout unchanged.
