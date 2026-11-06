# Surl.TlsSrp.UnitTests fixtures

Every byte here was sent by pinned upstream curl 8.21.0 - the supplementary static Windows build,
`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256
`589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, OpenSSL 4.0.1, the same
unpatched tag and OpenSSL as the Linux and macOS reference pins (ADR-0122, Context) - against
Surl.TlsSrp's server, recorded on 2026-11-05 by BL-566 with `Record-CurlExchange.ps1 -TlsSrp`,
which runs `Run-TlsSrpRelay.cs`. Never from the Curl port (ADR-0003).

The relay's one account is `jsmith` with password `abc`, salt `10 11 ... 1F` (16 bytes), in RFC
5054's 2048-bit group; every byte the server drew (its random, `b` and each explicit IV) is `20 21
... 3F` repeated; the simulation secret for unknown users is 32 zero bytes. The tests replay each
`client.bin` with the same values, so the client's `Finished` verifies only if Surl's server
derives every secret as curl did.

| Case | Command | curl's outcome |
| --- | --- | --- |
| `CurlTlsSrpJsmithAbc` | `-sS -k --max-time 20 --tlsauthtype SRP --tlsuser jsmith --tlspassword abc https://127.0.0.1:18566/`, `-Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nFINE\n'` | exit 0, stdout `FINE`; `request.bin` is the plaintext request the relay forwarded |
| `CurlTlsSrpJsmithBadpass` | the same with `--tlspassword badpass` and no response | exit 35, `tls alert bad record mac` |
| `CurlTlsSrpBaduserBadpass` | the same with `--tlsuser baduser --tlspassword badpass` | exit 35, `tls alert bad record mac` |

`client.bin` is every byte curl sent on the connection: the ClientHello record, the
ClientKeyExchange record, `ChangeCipherSpec`, the encrypted `Finished` and, in the passing case,
the encrypted request and `close_notify`.

Without `-k` (upstream case 324) the same build completes the handshake and exits 60, `SSL: could
not get peer certificate`. A salt that starts with a zero byte (`00 01 ... 0F`) failed with
`bad_record_mac` for the right password: OpenSSL's client reads `srp_s` as a number, so it hashes
the salt without its leading zero bytes.
