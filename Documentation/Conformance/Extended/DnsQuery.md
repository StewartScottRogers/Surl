# DnsQuery requirement matrix

The server-side requirements `Surl.Extended.Protocol.DnsQuery` answers, for the `dns` scheme
(ADR-0083 decision 9). Written from the specifications
[ADR-0092](../../Planning/Decisions/ADR-0092-how-the-dns-server-answers-dns-and-its-encrypted-transports.md)
cites, never from the Curl port's client matrix. Each row binds a server; "Covered by" names the
task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

## Messages, records and names (RFC 1035, RFC 3596, RFC 2782, RFC 8659, RFC 3597)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC1035-4.1.1-1 | MUST | A response carries the query's ID, QR = 1 and the query's opcode, and copies RD. | BL-479, BL-481 |
| DnsQuery:RFC1035-4.1.1-2 | MUST | AA is set in a response from a zone the server is authoritative for, and only then. | BL-480; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC1035-4.1.1-3 | MUST | RA says whether recursion is available; this server offers none, so RA = 0. | BL-480 |
| DnsQuery:RFC1035-4.1.1-4 | MUST | A query that cannot be interpreted is answered RCODE 1, Format error. | BL-481 |
| DnsQuery:RFC1035-4.1.1-5 | MUST | A kind of query the server does not support (an opcode other than QUERY) is answered RCODE 4, Not Implemented. | BL-481 |
| DnsQuery:RFC1035-4.1.1-6 | MUST | A query the server will not answer for policy reasons is answered RCODE 5, Refused. | BL-480 |
| DnsQuery:RFC9619-1 | MUST | A QUERY whose QDCOUNT is not 1 is answered FORMERR. | BL-481 |
| DnsQuery:RFC1035-4.1.2-1 | MUST | The response echoes the question section. | BL-479, BL-481 |
| DnsQuery:RFC1035-4.1.4-1 | MAY | Names in a response are compressed with pointers to earlier occurrences. | BL-479 |
| DnsQuery:RFC1035-4.1.4-2 | MUST | A compressed name in a received message is read; a pointer that loops or leaves the message is refused. | BL-479 |
| DnsQuery:RFC1035-2.3.4-1 | MUST | A label is at most 63 octets and a name at most 255 octets. | BL-479 |
| DnsQuery:RFC1035-3.4.1-1 | MUST | An A record's RDATA is a 32-bit address. | BL-479; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC3596-2.2-1 | MUST | An AAAA record's RDATA is a 128-bit address. | BL-479; live: `CurlPortClientQueriesSurlOverDnsTests.Aaaa_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC1035-3.3-1 | MUST | NS, CNAME, PTR, MX, TXT and SOA RDATA are written in their section 3.3 forms. | BL-479; live: `CurlPortClientQueriesSurlOverDnsTests.Mx_IsAnsweredWithTheZonesExchangeAndItsAddress` |
| DnsQuery:RFC2782-1 | MUST NOT | An SRV record's Target is not compressed. | BL-479 |
| DnsQuery:RFC8659-4.1-1 | MUST | A CAA record's RDATA is the Flags octet, the Tag length and Tag, then the Value. | BL-479 |
| DnsQuery:RFC3597-4-1 | MUST NOT | Names inside the RDATA of a type later than RFC 1035's, or unknown, are not compressed. | BL-479 |
| DnsQuery:RFC3597-5-1 | MUST | A record of a type the server does not know is served from its `\#` presentation form, its RDATA as given. | BL-479, BL-480 |
| DnsQuery:RFC4343-4.1-1 | MUST | Names are compared without regard to ASCII case, and the question's case is kept in the response. | BL-480 |

## The authoritative answer (RFC 1034, RFC 1035 section 5, RFC 2181, RFC 2308, RFC 4592, RFC 6604)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC1035-5.1-1 | MUST | A master file's `$ORIGIN`, `$TTL`, `@`, relative names, blank owners, parentheses, comments and escapes are read. | BL-480 |
| DnsQuery:RFC1035-5.2-1 | MUST | A zone has exactly one SOA, at its top. | BL-480 |
| DnsQuery:RFC2181-10.1-1 | MUST NOT | A name with a CNAME holds no other data; such a zone is refused. | BL-480 |
| DnsQuery:RFC2181-5.2-1 | MUST | The records of one RRset share one TTL; a zone where they do not is refused. | BL-480 |
| DnsQuery:RFC2181-5.1-1 | MUST | An RRset is sent whole or not at all. | BL-480, BL-481 |
| DnsQuery:RFC1034-4.3.2-1 | MUST | A name and type that exist are answered with the RRset. | BL-480; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC1034-4.3.2-2 | MUST | A CNAME met before the type asked for is put in the answer and the lookup continues at its target. | BL-480 |
| DnsQuery:RFC1034-4.3.2-3 | MUST | A name at or below a delegation is answered with the delegation's NS RRset in authority, its glue in additional, and AA = 0. | BL-480 |
| DnsQuery:RFC1034-3.6.2-1 | MUST | A CNAME chain that loops is not followed forever. | BL-480 |
| DnsQuery:RFC6604-2-1 | MUST | After a CNAME chain the RCODE is the last name's. | BL-480 |
| DnsQuery:RFC2308-2.1-1 | MUST | A name that does not exist is answered NXDOMAIN. | BL-480; live: `CurlPortClientQueriesSurlOverDnsTests.MissingName_IsNxdomainWithTheZonesSoa` |
| DnsQuery:RFC2308-3-1 | MUST | A negative answer (NXDOMAIN or NODATA) carries the zone's SOA in the authority section. | BL-480; live: `CurlPortClientQueriesSurlOverDnsTests.MissingName_IsNxdomainWithTheZonesSoa` |
| DnsQuery:RFC2308-3-2 | MUST | That SOA's TTL is the lower of its own TTL and its MINIMUM field. | BL-480; live: `CurlPortClientQueriesSurlOverDnsTests.MissingName_IsNxdomainWithTheZonesSoa` |
| DnsQuery:RFC4592-2.2.2-1 | MUST | An empty non-terminal exists: a query for it is NODATA, not NXDOMAIN. | BL-480 |
| DnsQuery:RFC4592-3.3.1-1 | MUST | A wildcard answers a name below its parent only when no closer name exists, with the question's name as owner. | BL-480 |
| DnsQuery:RFC4592-4.2-1 | SHOULD NOT | A wildcard NS is not served. | BL-480 |
| DnsQuery:RFC1035-3.3.9-1 | SHOULD | MX, NS and SRV targets' in-zone addresses go in the additional section. | BL-480; live: `CurlPortClientQueriesSurlOverDnsTests.Mx_IsAnsweredWithTheZonesExchangeAndItsAddress` |

## EDNS(0) (RFC 6891, RFC 3225, RFC 7830, RFC 8467)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC6891-6.1.1-1 | MUST | A query with more than one OPT record is answered FORMERR. | BL-481 |
| DnsQuery:RFC6891-6.1.2-1 | MUST | Unknown EDNS options are ignored. | BL-481 |
| DnsQuery:RFC6891-6.1.3-1 | MUST | A query with an EDNS version the server does not implement is answered BADVERS with the version it does. | BL-481 |
| DnsQuery:RFC6891-6.2.5-1 | MUST | A requestor payload size below 512 is treated as 512. | BL-481 |
| DnsQuery:RFC6891-7-1 | MUST | A response to a query with OPT carries an OPT; a response to a query without one carries none. | BL-481 |
| DnsQuery:RFC3225-3-1 | MUST | The DO bit of the query is copied to the response. | BL-481 |
| DnsQuery:RFC8467-4.1-1 | SHOULD | Over an encrypted transport, a response to a padded query is padded to a multiple of 468 octets. | BL-483, BL-484 |
| DnsQuery:RFC8467-4-1 | SHOULD NOT | Cleartext UDP and TCP responses are not padded. | BL-481, BL-482 |

## UDP (RFC 1035 section 4.2.1, RFC 2181 section 9)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC1035-4.2.1-1 | MUST | Without EDNS, a UDP response is at most 512 octets. | BL-481 |
| DnsQuery:RFC6891-6.2.3-1 | MUST | With EDNS, a UDP response is at most the requestor's payload size. | BL-481 |
| DnsQuery:RFC2181-9-1 | MUST | TC is set only when required data does not fit; dropping additional records alone does not set it. | BL-481 |
| DnsQuery:RFC1035-4.2.1-2 | MUST | A UDP response goes to the address and port the query came from. | BL-481 |

## TCP (RFC 1035 section 4.2.2, RFC 7766)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC7766-5-1 | MUST | A server answering over UDP also answers over TCP. | BL-485; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC1035-4.2.2-1 | MUST | Every message over TCP is preceded by its two-octet length. | BL-482 |
| DnsQuery:RFC7766-6.2.1-1 | MUST | Several queries are answered on one connection, which stays open after each answer. | BL-482 |
| DnsQuery:RFC7766-6.2.1.1-1 | SHOULD | Pipelined queries are read and answered without waiting for the client to read earlier answers. | BL-482 |
| DnsQuery:RFC7766-6.2.3-1 | SHOULD | Idle connections are closed after a timeout. | BL-482 |
| DnsQuery:RFC7766-8-1 | SHOULD | A response's length field and message are written together. | BL-482 |
| DnsQuery:RFC7766-6.2.1.1-2 | MAY | Pipelined answers are sent in any order. | n/a: ADR-0092 decision 4 answers in arrival order, which the RFC also allows |
| DnsQuery:RFC5936-2.2-1 | MUST | An AXFR answer starts and ends with the zone's SOA. | BL-482 |
| DnsQuery:RFC5936-4.2-1 | MUST NOT | AXFR is not served over UDP. | BL-481 |
| DnsQuery:RFC5936-2.2.1-1 | MAY | A server refuses a zone transfer by policy (REFUSED). | BL-482 |
| DnsQuery:RFC1995-4-1 | MAY | An IXFR the server cannot answer incrementally is answered with the whole zone; over UDP, with the SOA alone. | BL-481, BL-482 |

## DNS over TLS (RFC 7858, RFC 8310, RFC 9103)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC7858-3.1-1 | MUST | DoT is served on TCP port 853 by default. | BL-485 |
| DnsQuery:RFC7858-3.3-1 | MUST | Messages over TLS carry the two-octet length of RFC 1035 section 4.2.2. | BL-485; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC7858-3.4-1 | SHOULD | The TLS connection is kept for later queries and follows RFC 7766's idle guidance. | BL-482, BL-485 |
| DnsQuery:RFC9103-7.1-1 | MUST | A DoT server that agrees ALPN agrees `dot`. | BL-485 |
| DnsQuery:RFC8310-9-1 | MUST | TLS is at least version 1.2. | BL-485 |

## DNS over HTTPS (RFC 8484)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC8484-4.1-1 | MUST | A server answers GET with the `dns` parameter, base64url-encoded without padding. | BL-483 |
| DnsQuery:RFC8484-4.1-2 | MUST | A server answers POST whose body is the query and whose `Content-Type` is `application/dns-message`. | BL-483; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC8484-4.2-1 | MUST | A server implements the `application/dns-message` media type; a request of another type is not answered as DNS (415). | BL-483 |
| DnsQuery:RFC8484-4.2.1-1 | MUST | A DNS answer of any RCODE is sent with a 2xx status. | BL-483 |
| DnsQuery:RFC8484-4.2.1-2 | MUST | A request that holds no DNS query is answered with an HTTP error status (400). | BL-483 |
| DnsQuery:RFC8484-5.1-1 | SHOULD | The response's freshness lifetime is no longer than the smallest TTL in the answer, or the negative TTL. | BL-483 |
| DnsQuery:RFC8484-5.2-1 | SHOULD | DoH is served over HTTP/2. | BL-483 |
| DnsQuery:RFC8484-5-1 | MUST | DoH is served only over HTTPS. | BL-485 |
| DnsQuery:RFC8484-4.1-3 | MUST | The DNS ID of a request is answered as sent, 0 included. | BL-483 |

## DNS over QUIC (RFC 9250)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC9250-4.1-1 | MUST | The QUIC handshake agrees ALPN `doq`. | BL-484 |
| DnsQuery:RFC9250-4.1.1-1 | MUST | DoQ is served on UDP port 853 by default. | BL-485 |
| DnsQuery:RFC9250-4.1.1-2 | MUST NOT | DoQ is not served on port 53. | BL-485 |
| DnsQuery:RFC9250-4.2-1 | MUST | The response goes on the query's stream, length-prefixed, and the stream is ended with FIN. | BL-484; live: `CurlPortClientQueriesSurlOverDnsTests.A_IsAnsweredWithTheZonesAddress` |
| DnsQuery:RFC9250-4.2-2 | MUST | Each query is answered on its own client-initiated bidirectional stream, independently. | BL-484 |
| DnsQuery:RFC9250-4.2.1-1 | MUST | A query with a non-zero Message ID is a protocol error. | BL-484 |
| DnsQuery:RFC9250-4.3.3-1 | SHOULD | A protocol error closes the connection with `DOQ_PROTOCOL_ERROR`. | BL-484 |
| DnsQuery:RFC9250-4.3-1 | MAY | Excessive load closes the connection with `DOQ_EXCESSIVE_LOAD`. | BL-484; a flow refused before the handshake gets `CONNECTION_REFUSED` instead (ADR-0092 amendment 2, BL-832) |
| DnsQuery:RFC9250-4.3.1-1 | MUST | A stream the client cancels is not answered. | BL-484 |
| DnsQuery:RFC9250-4.2.2-1 | MUST | A zone transfer over DoQ sends all its responses on the query's stream. | BL-484 |
| DnsQuery:RFC9250-4.5-1 | MUST NOT | A query that changes state is not answered from 0-RTT data. | n/a: ADR-0082 issues no session tickets and accepts no 0-RTT, so no early data ever arrives |
| DnsQuery:RFC9250-5.4-1 | SHOULD | Responses are padded to resist traffic analysis. | BL-484 |

## The command line (ADR-0083, ADR-0092 decisions 5 to 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| DnsQuery:RFC4501-2-1 | MUST | A `dns:` listen URL's authority names the address and port served. | BL-485 |
| DnsQuery:ADR0083-6-1 | MUST | `SURL_EXTENSIONS=0` leaves `dns` unregistered. | BL-485 |
