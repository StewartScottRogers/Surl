# SMB fixtures

SMB exchanges recorded from upstream curl 8.21.0, the static-curl Windows build pinned in
`UpstreamCurlBuilds.json` (`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`,
SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, ADR-0030), with
`Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400` (BL-296). Never from the Curl port
(ADR-0003).

Each case was fed, through `-RawReply`, the exact bytes `SmbProtocolServer` sends for it - with
the challenge `0123456789ABCDEF` and the clock at 2026-09-30 12:00:00 UTC, as
`SmbTestExchange` fixes them - before any test pinned them, and curl ended each with the exit
code and `stderr.txt` ADR-0073 records for that step. The LM and NT responses in each
`request.bin` answer that challenge for the password in the command line (`secret`, or `wrong`
for the refused login), so a test's account has that password.

Each folder holds the recorder's files - `request.bin` (every SMB message curl sent, NetBIOS
headers included), `transcript.txt` (both directions, `> ` for curl and `< ` for the server),
`stdout.bin`, `stderr.txt` and `exitcode.txt` - and `response.bin`, the replies given to
`-RawReply`, concatenated. They are embedded resources of `Surl.Protocol.Smb.UnitTests`.
`RecordedExchangeTests` replays each `request.bin`, whole and one byte per read, and asserts
the bytes surl writes equal `response.bin`.

Recorded on 2026-09-30 from the repository root, in PowerShell, with `$r` holding the case's
replies as `\xHH` text, one reply per element:

| Folder | What curl did | Exit | Command line |
| --- | --- | --- | --- |
| `login-tree-connect` | negotiate, session setup (accepted, UID 1), tree connect to `\127.0.0.1\share` (TID 1), NT create `dir\file.txt` (`ERRDOS/ERRbadfile`), tree disconnect | 78 `Remote file not found` | `.\Record-CurlExchange.ps1 -Port 18445 -Raw -RawIdleMilliseconds 400 -RawReply $r -Curl <pin> -CurlArgs '-sS','-m','15','-u','alice:secret','smb://127.0.0.1:18445/share/dir/file.txt' -OutDirectory ...\login-tree-connect` |
| `login-refused` | negotiate, session setup (`ERRSRV/ERRbadpw`), nothing more | 67 `Login denied` | the same with `-u alice:wrong`, `-OutDirectory ...\login-refused` |
| `unknown-share` | negotiate, session setup, tree connect to `\127.0.0.1\nosha` (`ERRSRV/ERRinvnetname`), nothing more | 78 `Remote file not found` | the same with `smb://127.0.0.1:18445/nosha/dir/file.txt`, `-OutDirectory ...\unknown-share` |
