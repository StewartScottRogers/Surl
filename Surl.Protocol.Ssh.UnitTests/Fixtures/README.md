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

## SCP byte scripts

`ScpDownloadHandlerTests` and `ScpUploadHandlerTests` drive `scp -f` and `scp -t` at channel
level the same way, with byte scripts written by hand from ADR-0054 decisions 3 and 4 and their
account of libssh2's `scp_recv` and `scp_send` (a `\0` to start and after each control line on a
download; a `C` line, the file's bytes and `EOF` on an upload), not from a recording. BL-172
proves them against the pinned upstream curl build above, never against the Curl port
(ADR-0003); a disagreement there is a new task, never a changed expectation.

## Encrypted OpenSSH host keys

`openssh-encrypted-keys` holds test-only host keys in `openssh-key-v1` format, encrypted under
the `bcrypt` KDF, for `SshOpenSshKeyDecryptionTests` (BL-223). They are not curl recordings: a
host key is read by surl alone, so the writer is OpenSSH's `ssh-keygen`, from OpenSSH_10.3p1
(`C:\Program Files\Git\usr\bin\ssh-keygen.exe`, Git for Windows). Every key's passphrase is
`correct horse` and its round count 2, to keep the tests fast; each `.key.pub` is the key's public
half as `ssh-keygen` wrote it. Written on 2026-09-30, in Git Bash:

| File | Command line |
| --- | --- |
| `ed25519-<cipher>.key` for `aes128-ctr`, `aes192-ctr`, `aes256-ctr`, `aes128-gcm@openssh.com`, `aes256-gcm@openssh.com`, `chacha20-poly1305@openssh.com`, `aes256-cbc` | `ssh-keygen -q -t ed25519 -a 2 -Z <cipher> -N "correct horse" -C "surl test key <cipher>" -f ed25519-<cipher>.key` |
| `rsa-aes256-ctr.key` | `ssh-keygen -q -t rsa -b 2048 -a 2 -N "correct horse" -C "surl test key rsa" -f rsa-aes256-ctr.key` |
| `ecdsa-nistp384-aes256-ctr.key` | `ssh-keygen -q -t ecdsa -b 384 -a 2 -N "correct horse" -C "surl test key ecdsa" -f ecdsa-nistp384-aes256-ctr.key` |

## Host certificates

`host-certificates` holds test-only host keys and OpenSSH host certificates for
`SshHostCertificateTests`, `SshHostKeySetTests` and `SshKeyExchangeTests` (BL-222), described by
`SshTestCertificates`. Like the encrypted keys above they are read by surl alone, so the writer is
`ssh-keygen`, from OpenSSH_10.3p1 (`C:\Windows\System32\OpenSSH\ssh-keygen.exe`, Windows' OpenSSH
client, with OpenSSL 3.5.7). The CA's private key and the unused keys' private and public halves
were deleted once the certificates were written; `ca_ed25519.pub` is the CA's public half. Written
on 2026-09-30, in Git Bash:

| File | Command line |
| --- | --- |
| `ca_ed25519.pub` | `ssh-keygen -q -t ed25519 -N "" -C surl-test-ca -f ca_ed25519` |
| `host_rsa`, `host_ecdsa_p256`, `host_ed25519` | `ssh-keygen -q -t <rsa -b 2048, ecdsa -b 256, ed25519> -N "" -C surl-test-host -f <file>` |
| `<key>-cert.pub` for those three and `other_ecdsa_p384` (an ECDSA P-384 key made the same way) | `ssh-keygen -q -s ca_ed25519 -h -I surl-test-<key> -n localhost -V 20260101:20360101 -z 1 <key>.pub` |
| `user_ed25519-cert.pub`, a user certificate of `host_ed25519`'s key | `ssh-keygen -q -s ca_ed25519 -I surl-test-user -n tester -z 2 user_ed25519.pub` |

`Surl.Console.UnitTests`' `TestSshCertificateFiles` holds copies of `host_ed25519`, its host and
user certificates and `other_ecdsa_p384-cert.pub`.
