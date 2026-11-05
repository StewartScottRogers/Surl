# Surl.Extended.Protocol.DnsQuery.UnitLibrary

An extended server (ADR-0083): it answers the `dns` scheme the Curl port adds, which upstream
curl does not have. ADR-0092 decides how. It is judged by its specifications - RFC 1035, RFC 3596,
RFC 2782, RFC 8659, RFC 3597, RFC 6891 and the transport RFCs ADR-0092 lists - never by the Curl
port's output, and its unit tests replay bytes written by hand from those RFCs.

What is here so far is the message and record codec (BL-479, ADR-0092 decision 2):

- `DnsMessageWriter.Write` writes a `DnsMessage`, compressing names (RFC 1035 section 4.1.4) in
  owners, questions and the RDATA of NS, CNAME, SOA, PTR and MX only; SRV's target and every
  opaque type are never compressed (RFC 2782, RFC 3597 section 4).
- `DnsMessageReader.Read` reads one, and reports what it cannot read as a `DnsReadFault` with the
  header and questions read before it, so a `FORMERR` can echo them. A compression pointer must
  name an offset strictly before the labels it continues, which is how a loop is refused.
- RDATA: `DnsAddressData` (A, AAAA), `DnsDomainNameData` (NS, CNAME, PTR),
  `DnsStartOfAuthorityData`, `DnsMailExchangeData`, `DnsTextData`, `DnsServiceLocationData`,
  `DnsCertificationAuthorityAuthorizationData`, `DnsOptData` (read by meaning through
  `DnsOptRecord`) and `DnsOpaqueData` for every other type.

The zones and the authoritative answer (BL-480, ADR-0092 decisions 1 and 2):

- `DnsZoneFolder.LoadAsync` reads every `*.zone` file of a folder through `Surl.Content`'s
  `IContentFileSystem`, in ordinal order, into a `DnsZoneSet`; a fault is a
  `DnsZoneLoadException` naming the file, whose message is the `<reason>` of
  `surl: (37) Could not read <file path>: <reason>`.
- `DnsZoneSet.Add` loads one master file (RFC 1035 section 5) as the zone at its file name
  without `.zone`, bounded by `DefaultMaxZones`, `DefaultMaxRecords` and `DefaultMaxTotalBytes`.
  `DnsMasterFileTokenizer` splits entries, `DnsPresentationReader` reads names, strings, TTLs,
  types and RDATA (or RFC 3597's `\#` form), `DnsZoneFileReader` reads directives and records,
  and `DnsZone` checks what the records must hold together. Each fault is a
  `DnsZoneFileException`: `line <n>: <what>`, or `<what>` alone for the faults no line holds
  (no SOA, a second zone for one apex, a file name that is no apex, the zones' bounds). The
  `<what>` texts are pinned by `DnsZoneSetTests`.
- `DnsZoneSet.Answer` is RFC 1034 section 4.3.2's authoritative lookup (`DnsAuthoritativeLookup`):
  the RRset, ANY, NODATA and NXDOMAIN with the SOA at its negative TTL, CNAME chains through
  every zone (`SERVFAIL` on a loop or past 16), wildcards, referrals with glue, and `REFUSED`.
  `DnsAuthoritativeAnswer.ToResponse` makes the response message: RA = 0, the question echoed.
  AXFR and IXFR questions are not special-cased here; BL-482 answers them.

UDP (BL-481), TCP and zone transfer (BL-482), DoH (BL-483), DoQ (BL-484) and the command line
(BL-485) build on it.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`, and may also reference the
horizontal libraries in ADR-0002 decision 3's table as later ADRs amend it - never another
protocol server, original or extended. Never construct a `Socket`, `TcpListener`, `UdpClient`,
`SslStream` or `HttpListener` here; the transport comes from the listener seam.
