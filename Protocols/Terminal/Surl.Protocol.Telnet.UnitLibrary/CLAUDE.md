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
sends no banner, negotiates as usual, echoes every data byte verbatim (no CRLF, no `quit`) except that an LF is held
back until more data follows it, so a final LF is never echoed, records every byte read in `ReceivedBytes`, and closes
on the client's half-close or, once it has sent something, after the seam's `IdlePeriod` (on
`ExchangeContext.TimeProvider`). Never composed by `Surl.Composition`; the parameterless constructor keeps ADR-0025.

**Line faults** (ADR-0138, BL-798; ADR-0141, BL-914): `TelnetSession` holds each completed line
(`LineAwaitingAnswer`) until `TelnetProtocolServer` has asked `ExchangeContext.Faults` through
`TelnetLineFaults` - its fault point, at the `Command` stage, carries the exchange ID, the listen URL and the scheme
only, with no account, command or path - and a matching `error` rule's text is sent as one line in place of the echo,
then the session ends, publishing `FaultAppliedEvent` with target `line <n>`. A `delay` rule holds the line's answer
for its `ms` on `ExchangeContext.TimeProvider` with the idle clock paused (the hold ends early if the client closes or
resets the connection), and a `drop` rule aborts the connection with nothing written for the line
(`TelnetSession.EndWithoutAnswer`), each through `CommandStageFaults.FindAndApplyAsync` with one `FaultAppliedEvent`.
Only an `error` rule whose reply does not fit, or a rule of a kind other than `error`, `delay` or `drop`, is noted and
not applied.
