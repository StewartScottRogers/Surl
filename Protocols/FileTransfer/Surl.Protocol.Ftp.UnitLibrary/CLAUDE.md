# Surl.Protocol.Ftp.UnitLibrary

Phase 2. The FTP server, built as
[ADR-0052](../Documentation/Planning/Decisions/ADR-0052-how-the-ftp-server-answers-and-the-ftp-data-connection-seam.md)
decides, with
[ADR-0059](../Documentation/Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
for how it tells a limit from shutdown. Terms (control connection, data connection, passive
and active mode, explicit and implicit FTPS, protection level) are the glossary's, section
"FTP".

**URL schemes answered:** `ftp` and `ftps`; the engine completes `ftps`'s implicit TLS
handshake before `ServeAsync`, so `FtpProtocolServer` claims `ftps` itself.

## What it holds

- `FtpProtocolServer`: the control connection - the greeting, command lines bounded by
  `--max-line` and `--head-timeout` (`FtpLineReader`), and the limit replies of decision 10:
  a limit's cancellation (`ExchangeContext.IsCancelledForALimit`) is answered
  `421 Timeout, closing` after any data connection is closed, and shutdown ends the exchange
  with no farewell (ADR-0059).
- `FtpCommandResponder`: every command. Logins by `USER`/`PASS` through
  `IAuthenticationPolicy` (decision 3); the current directory and paths through the content
  store (`FtpPath`, decision 2); downloads (`SIZE`, `MDTM`, `REST`, `RETR`, `ABOR`, decision 4);
  listings (`LIST`, `NLST`, `MLSD` over a data connection and `MLST`, only with
  `--list-directories`, in the forms `FtpListingFormat` writes, decision 7); uploads and file
  management (`STOR`, `APPE`, `MKD`/`XMKD`, `RMD`/`XRMD`, `DELE`, `RNFR`/`RNTO`, only with
  `--allow-uploads`, `550 Not permitted` otherwise; `SITE` always `504`, decision 8); and TLS
  (`AUTH TLS`/`AUTH SSL` only when the server is constructed with `isTlsUpgradeAvailable`,
  `PBSZ`, `PROT C`/`P`, `CCC` always `534`, decision 5).
- Share access (ADR-0102 decisions 2.2 and 2.3, BL-658): `FtpCommandResponder` maps every path
  with `ContentStore.MapRequestPathForCaller`, the caller being the account a checked login
  (`PasswordLoginVerdict.Accepted`) named, or anonymous - before a login and after one
  `--allow-anonymous` accepted unchecked - on the exchange's scheme (`ftp` or `ftps`). A share the
  caller may not read answers as a missing path (`550 No such file` / `550 No such directory`);
  `STOR` or `APPE` in a share it may only read is `550 Not permitted` before any data connection.
- Chosen-error faults (ADR-0124 decision 4, BL-794; `FtpCommandResponder.Faults.cs`): every
  implemented command the login check lets through is asked about at a `Command`-stage
  `FaultPoint` before it runs - its verb, the path it names resolved against the current directory
  (`LIST`/`NLST` without ls options; none for a command naming no path), the account a checked
  login named (none after an unchecked anonymous login), the scheme and the listen URL in the
  engine's form (`FaultListenUrl`). A matching `error` rule's reply is sent in place of running it,
  so a transfer command opens no data connection and the store is untouched; the log notes it and
  one `FaultAppliedEvent` is published. Other kinds a plan returns here are not acted on.
- `FtpDataConnections`: one control connection's passive listener or active target
  (`EPSV`, `PASV`, `EPRT`, `PORT`, target parsed by `FtpActiveTargetParser`, decision 6), and
  `ProtectAsync`, which upgrades a data connection to TLS under `PROT P`. Every listener and
  data connection comes from `ExchangeContext.DataConnections` (decision 9).
- `DataConnectionUploadStream` and `DataConnectionWriteStream`: the data connection as the
  content store reads an upload and copies a download onto it; an upload's data connection is
  opened only at the store's first read, so a refused upload never uses one.
- The server half of upstream curl's test cases (ADR-0080 decision 2, BL-394), never composed by
  the shipped `surl`: `FtpUpstreamTestCaseServer` is `FtpProtocolServer` with the case's
  `FtpUpstreamTestCaseReplies` (parts `data`, `dataNUM`, `size`, `mdtm` and the `<servercmd>`
  lines, read by `FtpUpstreamTestCaseServerCommands`), its control connection wrapped in
  `ReceivedBytesRecordingConnection`. `FtpCommandResponder.UpstreamTestCase.cs` holds the commands
  the case answers in place of the content store (`CWD`, `DELE`, `SIZE`, `MDTM`, `RETR`, `LIST`,
  `NLST`, `STOR`, `APPE`); a `REPLY`'d command is answered with the case's text and not run, and
  no command needs a login first. `Surl.Conformance.UnitTests` composes it.

## Rules

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`
(one of the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it) -
nothing else. Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here - not for a data connection either. The control connection comes from the listener seam
and every data connection from `IDataConnectionOpener` in `Surl.Protocol.Abstractions`, so the
tests in the matching `.UnitTests` project drive the server with request bytes measured from
pinned upstream curl through `InMemoryConnection` and `InMemoryDataConnections`, with no
network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003). Pinned upstream curl's
proof against a live `surl` lives in `Surl.Conformance.UnitTests`
(`UpstreamCurlLogsInToSurlOverFtpTests`, `UpstreamCurlFetchesFromSurlOverFtpTests`,
`UpstreamCurlUploadsToSurlOverFtpTests`, the cases of ADR-0052 decision 12).
