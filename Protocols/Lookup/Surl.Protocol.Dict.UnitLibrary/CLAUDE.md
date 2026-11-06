# Surl.Protocol.Dict.UnitLibrary

Phase 1, alongside HTTP.

The DICT server (RFC 2229): answers the `CLIENT` line, `DEFINE`, `MATCH` and `SHOW`
requests upstream curl sends for `dict://` URLs, and the rest of RFC 2229's commands.
`DictProtocolServer` serves the files in the content store's served root as its one
database, `surl`; ADR-0011 records where definitions come from, the banner and every
reply.

**URL schemes answered:** `dict`

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

**Chosen-error fault** (ADR-0138, BL-798): `DictCommandResponder` asks `ExchangeContext.Faults`
through `DictCommandFaults` once per recognized command, at the `Command` stage, with the command in
capitals and the word (`DEFINE`, `MATCH`) or database (`SHOW INFO`) as the path; a matching `error`
rule's status line answers the command and the connection stays open, publishing `FaultAppliedEvent`.
A reply that does not fit, or a `delay` or `drop` rule, is noted and not applied (BL-912).
