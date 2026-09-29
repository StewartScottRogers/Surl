# TLS fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Tls -TlsRenegotiationOff` (BL-048). Never from the Curl port
(ADR-0003).

They confirm ADR-0006 section 4's condition: upstream curl's defaults (`-sS -k`, no
version option) still complete a TLS exchange against a server that refuses
renegotiation (`SslServerAuthenticationOptions.AllowRenegotiation = false`, as
`ServerTlsSettings` sets it), whether the server accepts TLS 1.2 and TLS 1.3 (Surl's
default; curl negotiated TLS 1.3, `TLS_AES_256_GCM_SHA384`) or TLS 1.2 only (curl
negotiated TLS 1.2, `TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384`). Both exited 0 with an empty
`stderr.txt`. No reply bytes are pinned from them beyond `exitcode.txt`; no test reads
them.

Each folder holds the recorder's four files: `request.bin` (the decrypted bytes curl
sent), `stdout.bin`, `stderr.txt` and `exitcode.txt`.

Recorded on 2026-09-29 from the repository root, in Windows PowerShell:

| Folder | Server accepts | Command line |
| --- | --- | --- |
| `renegotiation-off-tls12-and-tls13` | TLS 1.2 and TLS 1.3 | `.\Record-CurlExchange.ps1 -Port 18443 -Tls -TlsRenegotiationOff -TlsProtocol Tls12AndTls13 -CurlArgs '-sS','-k','https://127.0.0.1:18443/' -OutDirectory Surl.Networking.UnitTests\Fixtures\renegotiation-off-tls12-and-tls13` |
| `renegotiation-off-tls12` | TLS 1.2 | `.\Record-CurlExchange.ps1 -Port 18444 -Tls -TlsRenegotiationOff -TlsProtocol Tls12 -CurlArgs '-sS','-k','https://127.0.0.1:18444/' -OutDirectory Surl.Networking.UnitTests\Fixtures\renegotiation-off-tls12` |

With the same relay, `--tls-max 1.1` against TLS 1.2 and TLS 1.3 still fails with exit
35 (`SEC_E_UNSUPPORTED_FUNCTION`), as ADR-0006 measured without it; that run is not kept.

## Lingering close fixtures (BL-056)

Recorded on 2026-09-29 with the same pinned build, in Windows PowerShell, to measure
ADR-0021's lingering close. curl sent a 4 MiB body of zero bytes; the recorder answered
after the head with `HTTP/1.1 413 Content Too Large`, `Content-Length: 200000`,
`Connection: close` and 200000 `x` bytes. Only `exitcode.txt`, `stderr.txt` and
`stdout.bin` (curl's `%{size_download}`) are kept; `request.bin` held the 4 MiB body. No
test reads them.

| Folder | Server close | Command line |
| --- | --- | --- |
| `lingering-close-off` | at once, body unread: exit 56, `curl: (56) Recv failure: Connection was reset`, 102323 bytes received | `.\Record-CurlExchange.ps1 -Port 18056 -RespondAfterBodyBytes 0 -CloseUnread -Response "HTTP/1.1 413 Content Too Large\r\nContent-Length: 200000\r\nConnection: close\r\n\r\n<200000 x>" -CurlArgs '-sS','-o','bl056-out.bin','-w','%{size_download}','-H','Expect:','--data-binary','@<4 MiB file>','http://127.0.0.1:18056/' -OutDirectory <folder>` |
| `lingering-close-on` | after reading until curl stops: exit 0, 200000 bytes received | the same without `-CloseUnread` |

Three runs of each were made: without the lingering close curl exited 0, 56 and 56; with
it, 0 all three times.
