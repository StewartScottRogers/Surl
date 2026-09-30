# Surl.Protocol.Ftp.UnitLibrary

Phase 2.

The FTP server: a control channel and a separate data channel, passive (`PASV`, `EPSV`)
and active (`PORT`, `EPRT`), explicit TLS (`AUTH TLS`) and implicit TLS (`ftps://`),
serving and receiving files through the content store.

**URL schemes answered:** `ftp`, `ftps` when finished; `FtpProtocolServer.Schemes` is `ftp`
alone until `ftps` is built (BL-181).

Built so far (BL-177, ADR-0052 decisions 1 to 3 and 10): `FtpProtocolServer`, the control
connection - the greeting, bounded command lines (`FtpLineReader`), logins through
`IAuthenticationPolicy` (`FtpCommandResponder`), the current directory checked through the
content store (`FtpPath`), and the commands that need no data connection. BL-178 (decisions 4
and 6) added downloads: passive (`EPSV`, `PASV`) and active (`EPRT`, `PORT`) data connections
through `ExchangeContext.DataConnections` (`FtpDataConnections`, `FtpActiveTargetParser`), and
`SIZE`, `MDTM`, `REST`, `RETR` and `ABOR` in `FtpCommandResponder`. BL-179 (decision 7)
added listings: `LIST`, `NLST` and `MLSD` over a data connection, only with
`--list-directories` (a directory listing is otherwise answered as a missing directory), and
`MLST` on the control connection, in the forms `FtpListingFormat` writes. BL-180 (decision 8)
added uploads and file management, each only with `--allow-uploads` (`550 Not permitted`
otherwise): `STOR` and `APPE` (and `REST` before them) written through the content store's
temporary-file upload path, reading the data connection through `DataConnectionUploadStream`,
which opens it only at the store's first read, so a refused upload never uses one; an upload
past `--max-filesize` is `552` with nothing left behind. `MKD`/`XMKD`, `RMD`/`XRMD`, `DELE`,
`RNFR`/`RNTO` and `SITE` (always `504`) are answered in `FtpCommandResponder`. The TLS commands
answer `502 Command not implemented` until BL-181 builds them.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
