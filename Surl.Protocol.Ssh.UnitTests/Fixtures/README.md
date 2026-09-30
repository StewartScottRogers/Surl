# SSH fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, libssh2/1.11.1 with its
WinCNG backend), with `Record-CurlExchange.ps1 -Raw`, never from the Curl port (ADR-0003). They
are runs D and F of ADR-0051's measurements. The recorder sent `SSH-2.0-surl` CR LF as soon as
curl connected, recorded what curl sent until it went quiet, then hung up; curl then failed
with `(2) Failure establishing ssh session: -1, Unable to exchange encryption keys`.

Each folder holds the recorder's files: `request.bin` (curl's identification line
`SSH-2.0-libssh2_1.11.1` CR LF, then its unencrypted `SSH_MSG_KEXINIT` packet),
`transcript.txt` (`<` for the recorder's line, `>` for curl's, bytes outside printable ASCII as
`\xHH`), `stdout.bin`, `stderr.txt` and `exitcode.txt`. `SshProtocolServerTests` replays
`request.bin` into `SshProtocolServer` and asserts surl's identification line, its `KEXINIT`
bytes with a fixed cookie and padding, and the algorithms negotiated with curl's lists. The
files are embedded resources of `Surl.Protocol.Ssh.UnitTests`, so the tests read them without
touching the file system; `.gitattributes` here keeps git from rewriting their line endings.

Recorded on 2026-09-29 from the repository root, in Windows PowerShell:

| Folder | Exit | What curl did | Command line |
| --- | --- | --- | --- |
| `sftp-insecure` | 2 | identification line and `KEXINIT` with compression `none` | `.\Record-CurlExchange.ps1 -Port 47301 -Raw -RawReplyFirst -RawReply 'SSH-2.0-surl\r\n' -CurlArgs '-sS','-v','sftp://127.0.0.1:47301/x','-k' -OutDirectory Surl.Protocol.Ssh.UnitTests\Fixtures\sftp-insecure` |
| `sftp-insecure-compressed` | 2 | the same, with compression `zlib,zlib@openssh.com,none` | `.\Record-CurlExchange.ps1 -Port 47301 -Raw -RawReplyFirst -RawReply 'SSH-2.0-surl\r\n' -CurlArgs '-sS','-v','sftp://127.0.0.1:47301/x','-k','--compressed-ssh' -OutDirectory Surl.Protocol.Ssh.UnitTests\Fixtures\sftp-insecure-compressed` |

## SFTP byte scripts

Nothing after `KEXINIT` can be recorded until surl itself serves `sftp` (ADR-0054, Context), so
`SftpSessionTests` drives the `sftp` subsystem at channel level with byte scripts written by hand
from draft-ietf-secsh-filexfer-02 and ADR-0054's worked bytes (`VERSION`, `REALPATH .`, `ATTRS`
for `a.txt`, `STATUS OK`), not from a recording. BL-172 proves those answers against the pinned
upstream curl build above, never against the Curl port (ADR-0003); a disagreement there is a new
task, never a changed expectation.
