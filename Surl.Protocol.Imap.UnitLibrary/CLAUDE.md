# Surl.Protocol.Imap.UnitLibrary

Phase 3.

The IMAP4rev1 server (RFC 3501; not IMAP4rev2, ADR-0055 decision 1), as upstream curl uses it.
ADR-0055 decides every response. `ImapProtocolServer` answers `LOGIN`, `CAPABILITY`, `NOOP`,
`LOGOUT`, `ID`, `NAMESPACE`, `SELECT`, `EXAMINE`, `LIST`, `LSUB`, `STATUS`, `CHECK`, `CLOSE` and
`UNSELECT` from the shared mail store (BL-201); `FETCH` and `SEARCH` (BL-202), `APPEND`, `STORE`,
`COPY`, `MOVE`, `EXPUNGE` and the mailbox changes (BL-203), `STARTTLS` and `AUTHENTICATE`
(BL-204) are still answered `BAD Command not recognized`. It is not yet registered in
`Surl.Console` (BL-208).

**URL schemes answered:** `imap`, `imaps`

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
