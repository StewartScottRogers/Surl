# Surl.Protocol.Tftp.UnitLibrary

Phase 1, alongside HTTP.

The TFTP server (RFC 1350) over a datagram flow: read and write requests with the
`blksize`, `tsize` and `timeout` options (RFC 2347 to RFC 2349) upstream curl sends, served
from and written to the content store. Its transport is a datagram flow, not a connection.

**URL schemes answered:** `tftp` (`TftpProtocolServer.Schemes`)

`TftpProtocolServer` answers the request that opened a flow from a new transfer port, as
ADR-0013 decides: `TftpRequest` parses the RRQ, `TftpFileName` maps its file name onto
a content-store request path, `TftpNegotiation` answers its options, and
`TftpReadTransfer` sends the OACK and DATA blocks in lock step, retransmitting on the
exchange's `TimeProvider`. `TftpRequest` also parses a WRQ, and `TftpWriteTransfer`
receives it through `TftpUploadStream`, which ACKs each DATA block as `ContentStore.WriteUploadAsync`
reads it; both transfers share `TftpLockStep`'s send, wait and resend. Missing and refused files get
ERROR 1; a write gets ERROR 2 while uploads are off, and ERROR 3 past the upload limit.
Share access (ADR-0102 decisions 2.2 and 2.3, BL-662): TFTP has no login, so every request maps
with `ContentStore.MapRequestPathForCaller` as `ContentCaller.Anonymous` on `tftp`. A share closed
to anonymous, or limited to other schemes, is a missing file (ERROR 1); a write in a share
anonymous may only read, or a `readonly` share, is the store's `NotPermitted` (ERROR 2).
Command-stage faults (ADR-0124 decision 4, ADR-0141, BL-794, BL-907): `TftpCommandStageFault` asks the exchange's fault
plan about each well-formed request at a `Command`-stage point (`RRQ` or `WRQ`, the decoded file
name as path, no account, the listen URL in the engine's form from `FaultListenUrl`); a matching
`error` rule's `<code> <text>` reply is sent as that ERROR from the transfer port before the store
is touched, and one `FaultAppliedEvent` is published. A `delay` rule holds the request for its `ms` on
`ExchangeContext.TimeProvider` with `ExchangeContext.IdleClock` paused, then serves it as usual; a
`drop` rule sends nothing and ends the exchange (TFTP has no `IConnection`, so this class applies
both itself, mirroring `CommandStageFaults`); each publishes one `FaultAppliedEvent`.
`TftpProtocolServer` is also the `IDatagramRefusalWriter`: ERROR 0 for a flow past a
connection limit. ADR-0013 and its amendment record every answer. Its fixtures and the
commands that recorded them are in `Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests/Fixtures/README.md`.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with datagrams measured from pinned upstream curl, with no network.

The server half of upstream curl's test cases (ADR-0080 decision 2, BL-396), never composed by
the shipped `surl`: `TftpUpstreamTestCaseServer` logs each request in runtests' `opcode = 1` form
in `TftpUpstreamTestCaseReplies`, then answers it with `TftpProtocolServer` over an in-memory
content store of its own holding the case's `<data>`/`<dataNUM>` for the requested name; a write
is allowed only to a name carrying a case number, and its upload is kept. `writedelay:` wraps the
flow in `WriteDelayingDatagramFlow`.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
