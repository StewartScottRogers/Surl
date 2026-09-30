# Surl.Protocol.Ftp.UnitLibrary

Phase 2.

The FTP server: a control channel and a separate data channel, passive (`PASV`, `EPSV`)
and active (`PORT`, `EPRT`), explicit TLS (`AUTH TLS`) and implicit TLS (`ftps://`),
serving and receiving files through the content store.

**URL schemes answered:** `ftp` and `ftps`; the engine completes `ftps`'s implicit TLS
handshake before `ServeAsync`, so the server claims it directly (BL-181).

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
`RNFR`/`RNTO` and `SITE` (always `504`) are answered in `FtpCommandResponder`. BL-181
(decision 5) added TLS: `AUTH TLS`/`AUTH SSL` (`234`, the bytes buffered after the line thrown
away by `FtpLineReader.DiscardBuffered`, then `IConnection.UpgradeToTlsAsync`) only when the
server is constructed with `isAuthTlsAvailable` (a listener certificate), `534` otherwise;
`PBSZ` after TLS, `PROT C`/`P` after `PBSZ`, `CCC` always `534`. Under `PROT P` - the default on
`ftps` - `FtpDataConnections.ProtectAsync` upgrades each data connection after its `150`, and a
failed handshake is `425`. BL-230 (decision 10) answers the engine's cancellation of the
exchange - its idle timeout or maximum duration - with `421 Timeout, closing`, written after any
data connection is closed and within `LimitReplyWriteDeadline` on its own deadline, since the
exchange's token is already cancelled. BL-246 (ADR-0059) answers only a limit's cancellation
(`ExchangeContext.IsCancelledForALimit`) that way: shutdown ends the exchange with no farewell,
the cancellation propagating, and the limit reply's deadline is linked to
`ExchangeContext.ShutdownToken`, so shutdown cuts a limit reply off too.

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
