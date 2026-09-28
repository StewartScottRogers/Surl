# Surl.Protocol.Abstractions.UnitLibrary

Phase 0 holds `SurlExitCode`; Phase 1 adds the contracts.

The contracts every other project depends on. Today it holds only `SurlExitCode`, with
the values the placeholder executable needs. Phase 1 adds the listener seam (how a
protocol server receives an accepted connection or a datagram channel), the server-side
TLS contract and the exchange context.

This library references nothing. Never construct a `Socket`, `TcpListener`, `UdpClient` or
`SslStream` here. `SurlExitCode` reuses upstream curl's `CURLE_*` number wherever a
server-side meaning carries over; add values, never renumber one. Every contract lands
with an ADR, and a change to one touches every protocol server, so contracts land first
and the protocols fan out after.
