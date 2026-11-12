# Surl.Protocol.Telnet.UnitLibrary

Phase 1, alongside HTTP.

The TELNET server (RFC 854 and its option RFCs): negotiates options as upstream curl
does (`-t` sets curl's side), then carries the session.

**URL schemes answered:** `telnet`

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

**Echo-and-close mode** (ADR-0080 amendment 2026-11-11, BL-1050): `new TelnetProtocolServer(TelnetUpstreamTestCaseReplies)`
sends no banner, negotiates as usual, echoes every data byte verbatim (no CRLF, no `quit`), records every byte read in
`ReceivedBytes`, and closes on the client's half-close or, once it has sent something, after the seam's `IdlePeriod`
(on `ExchangeContext.TimeProvider`). Never composed by `Surl.Console`; the parameterless constructor keeps ADR-0025.

**Chosen-error fault** (ADR-0138, BL-798): `TelnetSession` holds each completed line
(`LineAwaitingAnswer`) until `TelnetProtocolServer` has asked `ExchangeContext.Faults` through
`TelnetLineFaults` - scheme only, TELNET has no command, path or account - and a matching `error` rule's
text is sent as one line in place of the echo, then the session ends, publishing `FaultAppliedEvent`
with target `line <n>`. A reply that does not fit, or a rule of another kind, is noted and not applied. A `delay` rule
holds the line's answer for its `ms` on `ExchangeContext.TimeProvider` with the idle clock paused, and a `drop` rule
aborts the connection with nothing written for the line (`TelnetSession.EndWithoutAnswer`), each through
`CommandStageFaults.FindAndApplyAsync` with one `FaultAppliedEvent` (BL-914).
