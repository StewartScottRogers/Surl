# ADR-0092 — How the DNS server answers `dns` and its encrypted transports

- **Status:** Accepted
- **Date:** 2026-11-04; Amendment 1 (BL-1036) 2026-11-11
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-478. BL-479 builds the message and record codec (decision 2), BL-480 the zones and the
  authoritative answer (decisions 1 and 2), BL-481 UDP (decision 3), BL-482 TCP and zone transfer
  (decision 4), BL-483 DNS over HTTPS and BL-484 DNS over QUIC (decision 5), and BL-485 the
  command line and the composition (decisions 1, 5 and 6).
- **Amends:** [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 1 (the `dns`
  server joins the rows of `--max-message`, decision 6) and section 5 (the DNS answers to a limit,
  decision 6). [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  and [ADR-0086](ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md) apply as
  written.

## Context

`Surl.Extended.Protocol.DnsQuery` (in `Protocols/Lookup/`, ADR-0083's table) answers the `dns`
scheme the Curl port adds. By ADR-0083 decision 1 it is judged by its protocol's specifications,
not by upstream curl, which has no `dns` scheme, and not by the Curl port. The port's
`Curl.Extended.Protocol.DnsQuery.UnitLibrary` and its matrices `DnsQuery.md` and
`DnsQuery-Encrypted.md` were read only to learn which queries a client sends - one question,
`TYPE=` and `CLASS=` from an RFC 4501 URL, over UDP, TCP, DNS over TLS, DNS over HTTPS
(`POST` to `/dns-query`) or DNS over QUIC, with EDNS(0) padding on the encrypted ones - so that the
server accepts them. Nothing below is taken from the port's output.

The specifications, and what each fixes for a server:

| Specification | Fixes |
| --- | --- |
| RFC 1034, RFC 1035 | The message (section 4.1), name compression (4.1.4), UDP and TCP (4.2), the authoritative lookup (RFC 1034 section 4.3.2), master files (RFC 1035 section 5), the RDATA of A, NS, CNAME, SOA, PTR, MX and TXT (section 3.3) |
| RFC 2181 | RRsets (section 5), TC (section 9), CNAME beside other data (section 10.1) |
| RFC 2308 | Negative answers carry the SOA, with the negative TTL (sections 3 and 5) |
| RFC 3596, RFC 2782, RFC 8659, RFC 3597 | AAAA; SRV; CAA; unknown types and the `\#` form |
| RFC 4343, RFC 4592 | Case-insensitive names with case kept; wildcards |
| RFC 6891, RFC 3225, RFC 7830, RFC 8467 | EDNS(0) and its OPT record; the DO bit; the Padding option and its policy |
| RFC 7766 | DNS over TCP: support, persistent connections, pipelining, idle timeouts |
| RFC 5936, RFC 1995 | AXFR and IXFR |
| RFC 4501 | The `dns:` URI |
| RFC 7858, RFC 8310, RFC 9103 | DNS over TLS on port 853, its profiles, and the ALPN ID `dot` (RFC 9103 section 7.1) |
| RFC 8484 | DNS over HTTPS |
| RFC 9250 | DNS over QUIC |
| RFC 9619 | A query's QDCOUNT is 1 |

The seams are ADR-0004's: `IDatagramProtocolServer` and `IDatagramFlow` for UDP,
`IConnectionProtocolServer` for TCP. HTTP/1.1 comes from `Surl.HttpServing` (ADR-0084 decision 1,
BL-407), HTTP/2 from `Surl.Http2` (ADR-0086 decision 1, BL-409) and QUIC from `Surl.Quic` through
`QuicDatagramFlowServer` and `IQuicApplicationProtocol` (ADR-0086 decision 4, BL-572).

## Decision

### 1. The zones: master files under the data directory

- **Where they come from.** Every file matching `<path>/.surl/dns/*.zone` under `--directory`
  (ADR-0031), read once at start after the data-directory lock and before any listener binds -
  ADR-0072 decision 1's rule for the LDAP directory, applied to DNS. `Surl.Console` lists and
  reads the files through `Surl.Content`'s `IContentFileSystem` (BL-485); the server parses them
  (BL-480). **No option names another folder**, and **no zone is built in**. A missing folder, and
  in-memory mode (no `--directory`), is **no zone at all**: every query is answered `REFUSED`
  (decision 2). The `.surl` folder is never served (ADR-0031 decision 5), so the zone files are
  never readable through another server.
- **Why not an option.** The task's planning assumption was a folder named by an option. ADR-0007
  adds no option where a file under the data directory does, ADR-0031 decision 8 keeps every file
  surl reads or writes under the data directory, and ADR-0072 already chose a file there over an
  option for LDAP's seed. One rule for both read-only stores beats a second way to say the same.
- **The format** is RFC 1035 section 5.1's master file, UTF-8 without a byte order mark, lines
  ending in LF or CRLF: `$ORIGIN`, `$TTL` (RFC 2308 section 4), `$INCLUDE` refused (it would read
  a file the operator did not name under `.surl/dns`), `@`, relative names, a blank owner meaning
  the previous owner, `(` `)` continuing an entry over lines, `;` comments, quoted
  character-strings with `\X` and `\DDD` escapes (RFC 1035 section 5.1), the class `IN` only, and
  TTL and class in either order. Every record type is written in its RFC presentation form, or
  as RFC 3597 section 5's `TYPE<n>` with `\# <length> <hex>`.
- **The origin.** A file's initial origin is its file name without `.zone` (`example.com.zone`
  is `example.com.`); `$ORIGIN` may change it for the lines after it. The zone's apex is the
  initial origin. One file is one zone.
- **What a zone must hold** (RFC 1035 section 5.2; RFC 2181 sections 5.2 and 10.1): exactly one
  SOA, at the apex; every owner at or below the apex; no CNAME beside other data at one owner; one
  TTL per RRset; no two identical records. NS at the apex is not required (a test zone may omit
  it); without one, no NS is ever added to the authority section.
- **Overlapping zones** (`example.com` and `sub.example.com` both served) are allowed; a query is
  answered from the zone with the longest apex that contains its name. Two files with the same
  apex are refused.
- **Malformed at start** - surl refuses to start with `CouldNotReadFile` (37) and stderr
  `surl: (37) Could not read <file path>: <reason>` (ADR-0050 decision 7's form, as ADR-0072
  decision 1 uses it), `<reason>` the exception message when the file cannot be read, and
  otherwise `line <n>: <what>`, `<n>` the first physical line of the fault. `<what>` is one of a
  fixed list BL-480 pins (for example `not UTF-8`, `a class other than IN`, `an owner outside the
  zone`, `$INCLUDE is not served`, `no SOA at the apex`, `a second SOA`, `a CNAME beside other
  data`, `records of one set with different TTLs`, `bad RDATA for <type>`, `past the zones'
  bounds`, `a second zone for <apex>`). Starting with fewer zones instead would hide the
  operator's mistake.
- **Bounds** (ADR-0006: every store is bounded), constructor parameters with these defaults and
  no option, as ADR-0072 decision 1's:

  | Constant | Default |
  | --- | --- |
  | `DnsZoneSet.DefaultMaxZones` | 1000 zone files |
  | `DnsZoneSet.DefaultMaxRecords` | 1000000 records, all zones together |
  | `DnsZoneSet.DefaultMaxTotalBytes` | 268435456 bytes (256 MiB) of owner names and RDATA, as LDAP's |
  | `DnsZoneSet.MaxRecordDataBytes` | 65535 bytes, one RDATA (RFC 1035 section 3.2.1's RDLENGTH) |

- **Read-only while serving.** Zones are never written; dynamic update (RFC 2136, opcode 5) is
  `NOTIMP` (decision 2). A changed file is read at the next start; no reload while running.

### 2. The record types and the answer for every outcome

**Record types served** with their RDATA read and written field by field (BL-479): A (RFC 1035
section 3.4.1), NS (3.3.11), CNAME (3.3.1), SOA (3.3.13), PTR (3.3.12), MX (3.3.9), TXT
(3.3.14), AAAA (RFC 3596 section 2.2), SRV (RFC 2782), CAA (RFC 8659 section 4.1). **Every other
type** is served as RFC 3597 section 5's opaque RDATA: loaded from its `\#` form (or, for a type
above with a known form, either form), answered when asked for by its number, and never
compressed (RFC 3597 section 4). DNSSEC types (DNSKEY, RRSIG, NSEC, DS and the rest) load and
answer only that way: the server signs nothing and adds no RRSIG for DO (below). The meta-types
OPT (41), AXFR (252), IXFR (251) and ANY (255) cannot be zone records (refused at load).

**Every response** (RFC 1035 section 4.1.1): the query's ID; QR = 1; the query's opcode; RD copied
from the query; **RA = 0** always (no recursion is offered); Z = 0; the question echoed with its
own case (RFC 4343 section 4.1); AA = 1 exactly when the answer comes from a served zone. Owner
names in the answer use the question's case for the question's name, the zone's case elsewhere.
Names are compressed (RFC 1035 section 4.1.4) in owners and in the RDATA of NS, CNAME, SOA, PTR
and MX only - RFC 3597 section 4 forbids it in every later type, and RFC 2782 in SRV's target.

**The authoritative lookup** follows RFC 1034 section 4.3.2 steps 2 and 3 for a QUERY (opcode 0)
whose one question has class IN (or ANY, answered as IN, RFC 1035 section 3.2.5):

| Outcome | RCODE | AA | Answer | Authority | Additional |
| --- | --- | --- | --- | --- | --- |
| The name and type exist | `NOERROR` (0) | 1 | the RRset | the apex NS RRset, if any | A and AAAA of in-zone NS, MX and SRV targets (RFC 1035 sections 3.3.9 and 3.3.11, RFC 2782), as room allows |
| QTYPE ANY (255) | `NOERROR` | 1 | every RRset at the name | as above | as above |
| The name exists, not the type: NODATA, an empty non-terminal included (RFC 4592 section 2.2.2) | `NOERROR` | 1 | none | the zone's SOA, its TTL the lower of the SOA's TTL and MINIMUM (RFC 2308 sections 3 and 5) | none |
| The name does not exist | `NXDOMAIN` (3) | 1 | none | the SOA, as NODATA | none |
| A CNAME at the name, QTYPE not CNAME | as the chain's end | 1 | the CNAME, then the chain followed through every served zone (RFC 1034 section 3.6.2, RFC 1034 section 4.3.2 step 3a), the RCODE and authority those of the last name (RFC 6604 section 2) | as the last name | as the last name |
| A chain leaving every served zone | `NOERROR` | 1 | the CNAMEs up to the last served one | none | none |
| A chain that revisits a name, or passes 16 CNAMEs | `SERVFAIL` (2) | 1 | the CNAMEs read so far | none | none |
| A name at or below a delegation (an NS RRset below the apex; RFC 1034 section 4.3.2 step 3b) | `NOERROR` | 0 | none | the delegation's NS RRset | its in-zone A and AAAA glue |
| A name below a wildcard's parent, with no closer name (RFC 4592 sections 3.3.1 and 4) | as the wildcard's records | 1 | the wildcard's RRset (or CNAME, chased) with the question's name as owner | as above | as above |
| The name is in no served zone, or the class is not IN or ANY | `REFUSED` (5) | 0 | none | none | none |

- **Wildcards are served** (RFC 4592): an owner whose first label is `*` matches a name below its
  parent when no closer name, empty non-terminals included, exists (section 3.3.1); it never
  matches the name of an existing node or across a delegation; a `*` label elsewhere in a name is
  an ordinary label (section 2.1.3). A wildcard NS is refused at load (section 4.2).
- **Why `SERVFAIL` for a loop.** RFC 1034 section 3.6.2 makes a CNAME loop an error in the data;
  16 is far longer than any chain a correct zone needs, and
  answering `NOERROR` with a partial chain would hide the operator's mistake.
- **Errors in the request** (RFC 1035 section 4.1.1):

  | Request | Answer |
  | --- | --- |
  | Fewer than 12 bytes: no readable header | **dropped**, nothing sent (there is no ID to answer) |
  | QR = 1 (a response) | **dropped**, nothing sent: answering a response can loop two servers |
  | An opcode other than QUERY (0): IQUERY (1, RFC 3425), STATUS (2), NOTIFY (4), UPDATE (5), any other | `NOTIMP` (4), the header echoed, the question echoed when it can be read |
  | QDCOUNT other than 1 (RFC 9619) | `FORMERR` (1), no question echoed |
  | A question or record that cannot be read: a name past 255 bytes or a label past 63 (RFC 1035 section 2.3.4), a compression pointer past the message or not strictly backwards (so no loop can be followed), a count past the records present, bytes left over | `FORMERR`, the question echoed when it was read |
  | More than one OPT record, or an OPT whose owner is not the root (RFC 6891 section 6.1.1) | `FORMERR` |
  | An OPT with version above 0 (RFC 6891 section 6.1.3) | `BADVERS` (extended RCODE 16), with an OPT of version 0 |
  | QTYPE AXFR or IXFR | decision 4 |

  Records in the answer, authority and additional sections of a QUERY, other than OPT, are read
  and ignored (RFC 1035 section 4.1.1 leaves them meaningless in a query).
- **EDNS(0)** (RFC 6891 section 7): a response carries an OPT exactly when its query did, with the
  server's payload size (decision 3), extended RCODE and version 0, and the DO bit copied from the
  query (RFC 3225 section 3). Unknown options are ignored (RFC 6891 section 6.1.2). The Padding
  option (RFC 7830) is answered on the encrypted transports only, as RFC 8467 section 4.1's
  recommended policy says: a response to a query that carried it is padded to a multiple of 468
  bytes; on UDP and TCP no padding is sent (RFC 8467 section 4: padding cleartext is pointless).
  Cookies (RFC 7873) and the TCP keepalive option (RFC 7828) are ignored, which both RFCs allow.

### 3. UDP: 512 bytes, the EDNS(0) payload size, and TC

- **Limit** (RFC 1035 section 4.2.1, RFC 6891 section 6.2.3 to 6.2.5): 512 bytes without OPT; with
  OPT, the query's payload size, a value below 512 taken as 512, and never more than the server's
  own **1232 bytes**, which its OPT advertises. 1232 is the size that avoids IP fragmentation on
  every IPv6 path (a 1280-byte minimum MTU less 48 bytes of headers), the figure DNS Flag Day 2020
  chose, and RFC 9715 asks responders to avoid fragmentation; fragmented UDP answers are a known
  spoofing surface.
- **Truncation** (RFC 2181 section 9): the additional section is cut first, without TC; then the
  authority section, without TC unless it held the negative answer's SOA; then whole RRsets of the
  answer section are kept while they fit and TC = 1 is set once one does not. An RRset is never
  split. A message cut to its header and question with TC = 1 is what a client then repeats over
  TCP (RFC 7766 section 5).
- **One response per query**, sent to the datagram's source through the flow
  (`IDatagramFlow.SendAsync`). An unanswerable datagram (decision 2's "dropped") sends nothing.
- **Zone transfer over UDP** is decision 4's.

### 4. TCP: persistent connections, pipelining, the idle timeout, and zone transfer

- **Framing** (RFC 1035 section 4.2.2): each message is a two-byte length then the message; a
  response's length prefix and message go to the connection in one write (RFC 7766 section 8).
- **Persistent and pipelined** (RFC 7766 sections 6.2.1 and 6.2.1.1): the server reads queries one
  after another on one connection and answers each as it is read, in arrival order, which the RFC
  allows; the connection stays open after each answer. A client may send several queries before
  reading any answer.
- **Never truncated.** A TCP answer carries everything decision 2 puts in it up to 65535 bytes;
  past that, which only ANY at a very large node can reach, RRsets are dropped as decision 3 drops
  them and TC is set, since the length prefix can carry no more.
- **Timeouts.** ADR-0006's `--idle-timeout` (120 s) closes a connection on which no byte has moved
  (RFC 7766 section 6.2.3 asks for "at least a few seconds" and lets the server choose; one
  idle-timeout concept for every server, as the glossary has it, beats a DNS-only one). Once a
  length prefix has begun, the rest of its message must arrive within `--head-timeout` (30 s), or
  the connection closes with no bytes: the slow-sender defence of ADR-0006 section 1. `--max-time`
  bounds the connection's whole life.
- **Lengths.** A length of 0, or above `--max-message` (whose 1 MiB default exceeds the prefix's
  65535, so it bites only when set lower), closes the connection with no bytes. A length of 1 to
  11 holds no header and is answered as decision 2's "dropped": the connection closes.
- **The client closes first**, normally; on shutdown the server closes after the answer it is
  writing (ADR-0059's rule: a limit and shutdown are told apart and neither is an error).
- **Zone transfer** (AXFR, RFC 5936; IXFR, RFC 1995) is **refused by default**: `REFUSED` (5), the
  question echoed, on every transport. A whole zone is exposure, so it is opened only by an
  explicit option, as ADR-0006 section 2 opens directory listings: **`--allow-zone-transfer`**
  (a negatable flag, categories `dns` and `security`, default off; BL-485 adds it). With it:
  - AXFR over TCP, DoT and DoQ: the zone in one or more messages, the apex SOA first and last,
    every other record once between them (RFC 5936 sections 2.2 and 4.1; RFC 9250 section 4.2.2
    for DoQ, all on the query's stream); each message as full as 65535 bytes allows; the question
    in the first message only.
  - IXFR over TCP, DoT and DoQ: the server keeps no history, so it answers with the whole zone in
    AXFR form (RFC 1995 section 4 allows it).
  - Over UDP: AXFR is `NOTIMP` (RFC 5936 section 4.2: AXFR over UDP is not defined); IXFR is
    answered with the apex SOA alone, which tells the client to use TCP (RFC 1995 section 4).
  - Over DoH: `NOTIMP` for both: RFC 8484 defines one response per request and no multi-message
    answer.
  - A name that is not a served zone's apex: `NOTAUTH` (9) (RFC 5936 section 2.2.1).

### 5. The encrypted transports and how a listener asks for them

The `dns` scheme names the server; the transport is chosen by one option, which mirrors the Curl
port's client option of the same name so one habit serves both programs:

**`--dns-transport <list>`** - a comma-separated list of `udp`, `tcp`, `tls`, `https` and `quic`,
default `udp,tcp` (RFC 7766 section 5: a server MUST serve TCP beside UDP). Categories `dns`
and `tls`. Every `dns://` listen URL serves every transport in the list on its host:

| Transport | Bound | Default port (listen URL without a port) | ALPN | Spec |
| --- | --- | --- | --- | --- |
| `udp` | UDP | 53 | none | RFC 1035 section 4.2.1 |
| `tcp` | TCP | 53 | none | RFC 7766 |
| `tls` | TCP + TLS from the first byte | 853 | `dot` | RFC 7858 sections 3.1 to 3.3, RFC 9103 section 7.1 |
| `https` | TCP + TLS, HTTP/2 or HTTP/1.1 | 443 | `h2`, `http/1.1` (ADR-0081 decision 2's order) | RFC 8484 |
| `quic` | UDP + QUIC version 1 | 853 | `doq` | RFC 9250 sections 4.1 and 4.1.1 |

- **Ports.** A listen URL with a port (`dns://127.0.0.1:5300`) puts every listed transport on that
  port; a URL without one puts each transport on its own default. Port 0 gives each transport its
  own ephemeral port, each reported on its own status line (ADR-0007 section 7). Two listed
  transports that would bind the same port with the same protocol (`tcp` with `tls` or `https`,
  `tls` with `https`, `udp` with `quic`) on an explicit non-zero port are refused before any
  listener binds: `FailedInit` (2), `surl: --dns-transport <a> and <b> cannot share port <n>`.
  `quic` on port 53 is refused the same way, `surl: --dns-transport quic cannot use port 53`
  (RFC 9250 section 4.1.1: DoQ MUST NOT use port 53). A value outside the five, an empty list or a
  repeated value is `FailedInit` as ADR-0007 section 5 refuses any bad value.
- **Certificates.** `tls`, `https` and `quic` need ADR-0010's `--cert`, or `--self-signed`, as an
  implicit-TLS listen URL does (ADR-0032 section 10's refusal and text, `CertificateProblem`
  (58)). TLS 1.2 and 1.3 for `tls` and `https` (ADR-0006 section 4; the BCP 195 profile RFC 7858 and
  RFC 8310 build on asks for TLS 1.2 at least), TLS 1.3 for `quic` (RFC 9001).
- **DoT ALPN.** The handshake agrees `dot` when the client offers it, accepts a client that offers
  no ALPN (RFC 7858 predates the ID and most stub clients send none), and ends with
  `no_application_protocol` (120) a client whose list lacks `dot` (RFC 7301 section 3.2). After
  the handshake the connection is decision 4's TCP connection exactly (RFC 7858 section 3.3).
- **DoH** (RFC 8484), through `Surl.HttpServing`'s `IHttpRequestHandler` for HTTP/1.1 and
  `Surl.Http2`'s `IHttp2StreamHandler` for HTTP/2, one handler deciding both (ADR-0084 decision 1,
  ADR-0086 decision 1):
  - **The path is `/dns-query`**, fixed: RFC 8484 leaves the URI template to configuration and uses
    `/dns-query` in every example (section 4.1.1), and it is the path a client derived from a
    `dns:` URL can know without being told. Any other path is `404 Not Found`.
  - `GET /dns-query?dns=<base64url>` (section 4.1, RFC 4648 section 5 without padding; a padded
    or invalid value, a missing or repeated `dns`, or a decoded message under 12 bytes is
    `400 Bad Request`); other query parameters are ignored.
  - `POST /dns-query` with `Content-Type: application/dns-message` (section 4.1; another type is
    `415 Unsupported Media Type`); a body past 65535 bytes is `413 Content Too Large`.
  - Another method is `405 Method Not Allowed` with `Allow: GET, POST` (RFC 9110 section 15.5.6).
  - `Accept` is not checked: the one media type is always sent (RFC 9110 section 12.5.1 lets a
    server answer without honouring it).
  - **The answer** is `200 OK` with `Content-Type: application/dns-message`, the DNS response
    whatever its RCODE (section 4.2.1), and `Cache-Control: max-age=<n>`, `<n>` the smallest TTL
    in the answer section, or for a negative answer the SOA's negative TTL (section 5.1, RFC 2308
    section 5); a response with no record has `max-age=0`. A query decision 2 "drops" is
    `400 Bad Request`, since HTTP must answer. The DNS ID is echoed as sent, 0 or not (section
    4.1's "SHOULD use 0" binds the client).
  - **Logins** (ADR-0084 decision 4): with no account configured, DoH asks for none, as DNS on
    every other transport has no login. With `--user` or `--user-file` given, every DoH request
    is checked through `IHttpAuthenticationSession` with ADR-0032's rules and `--auth` methods,
    so an operator can fence a DoH listener.
  - **No DoH over HTTP/3.** ADR-0086 decision 6 gives `dns` no HTTP/3 route, no client of the port
    asks for one, and RFC 8484 section 5.2 names HTTP/2 as the minimum. A later ADR adds it if a
    client needs it.
- **DoQ** (RFC 9250), an `IQuicApplicationProtocol` on `QuicDatagramFlowServer` with
  `QuicServerConnectionOptions` naming ALPN `doq` only (ADR-0086 decision 4) and these transport
  parameters: `initial_max_streams_bidi` 100 (concurrent queries per connection),
  `initial_max_streams_uni` 0 (the protocol uses none), `max_idle_timeout` `--idle-timeout`, and no
  `max_datagram_frame_size`:
  - each query on its own client-initiated bidirectional stream: the two-byte length and the
    message (section 4.2). The server answers once the whole message has arrived, writes the
    length-prefixed response on that stream and ends it with FIN; it does not wait for the client's
    FIN (section 4.2 lets a server answer before it) but bytes after the message are
    `DOQ_PROTOCOL_ERROR`;
  - a query whose Message ID is not 0 closes the connection with `DOQ_PROTOCOL_ERROR` (2)
    (section 4.2.1); so does a client-initiated unidirectional stream, a server-initiated stream
    used by the client, or a stream whose length prefix is 0 or larger than its bytes before FIN
    (section 4.3.3). A stream the client resets is dropped without an answer (section 4.3.1);
  - a stream past the 100 is impossible (QUIC's `STREAMS_LIMIT_ERROR` is `Surl.Quic`'s); a
    connection past ADR-0006's limits is closed with `DOQ_EXCESSIVE_LOAD` (4) (section 4.3), an
    internal fault with `DOQ_INTERNAL_ERROR` (1), shutdown and the idle timeout with
    `DOQ_NO_ERROR` (0);
  - no 0-RTT: ADR-0082 issues no session tickets and accepts no 0-RTT, so section 4.5's replay rules are met by
    never accepting early data;
  - responses to padded queries are padded (decision 2; section 5.4).

### 6. Limits

Every ADR-0006 limit applies to the DNS server, with these answers (ADR-0006 section 5's DNS
column):

| Limit (ADR-0006 section 1) | UDP | TCP, DoT | DoH | DoQ |
| --- | --- | --- | --- | --- |
| `--max-connections`, `--max-connections-per-address` | the flow is not created; the datagram dropped | close with no bytes | as the HTTP server: `503` and close (HTTP/1.1), `GOAWAY` (HTTP/2, ADR-0081 decision 5) | `CONNECTION_CLOSE` `DOQ_EXCESSIVE_LOAD` |
| `--idle-timeout` (120 s) | the flow ends | close with no bytes | as the HTTP server | `DOQ_NO_ERROR` (and QUIC's own idle timeout) |
| `--max-time` (3600 s) | the flow ends | close with no bytes after the answer being written | as the HTTP server | `DOQ_NO_ERROR` |
| `--head-timeout` (30 s) | n/a: a datagram arrives whole | a message begun and not finished: close with no bytes; the TLS handshake, as ADR-0010 | the request head, as the HTTP server | the QUIC handshake, as ADR-0082 decision 7 |
| `--max-message` (1 MiB, capped by 65535) | n/a: a datagram is at most 65507 bytes and is read whole | close with no bytes (decision 4) | `413` (decision 5) | `DOQ_PROTOCOL_ERROR` |
| `--max-request-head` | n/a | n/a | the HTTP request head, as the HTTP server | n/a |
| `--max-line`, `--max-filesize` | n/a: no lines, no uploads | n/a | n/a | n/a |

- **Zone bounds** are decision 1's constants.
- **No response-rate limiting.** RRL is an operational heuristic with no RFC defining its answers,
  and ADR-0006's per-address flow limit already bounds how many sources one address can be. The
  amplification an open UDP DNS server offers is bounded by decision 3's 1232-byte ceiling, by
  `REFUSED` for every name outside the served zones (a reply no larger than the query), by ANY
  answered only from served zones, and by zone transfer being off by default and never over UDP.
  The caveat stands below; RRL is added by a later ADR if an internet-facing use needs it.
- **`--max-message`** gains the category `dns` (ADR-0006 section 1's row, amended); BL-485 adds the
  category to the option's help row.

### 7. `--aihelp`, help and the composition

The `--aihelp` topic is **`dns`**, its help category `dns` (ADR-0034; ADR-0083 decision 8). Its
about text names RFC 1035 and the transport RFCs (RFC 7766, RFC 7858, RFC 8484, RFC 9250) as the
oracle and repeats ADR-0083's sentence that the server is judged by its specifications, not by
upstream curl. `--dns-transport` and `--allow-zone-transfer` are its options; `--directory`,
`--cert`, `--self-signed`, `--user`, `--user-file`, `--auth` and the limits gain the `dns`
category. BL-485 builds all of it.

## Consequences

Good:

- One zone format every DNS operator knows, read once and checked at start, from the place LDAP's
  seed already lives.
- Every outcome a stub client can meet - answer, NODATA, NXDOMAIN, CNAME, referral, wildcard,
  REFUSED, FORMERR, NOTIMP, BADVERS, TC - is pinned to an RFC section, so each test's expected
  bytes come from the RFC text.
- One option chooses the transports, so `surl --dns-transport udp,tcp,tls,https,quic --self-signed
  --directory zones dns://127.0.0.1:0` serves every transport the Curl port's client has.

Costs and caveats:

- No response-rate limiting: an internet-facing UDP listener can still be used for modest
  reflection within decision 3's bounds.
- No DNSSEC signing: signed zones are served only as the records the operator wrote, and a
  validating client sees an unsigned answer to a DO query.
- No reload: a zone change needs a restart.
- The 1232-byte UDP ceiling makes some answers that would fit a client's 4096 go over TCP.

## Alternatives considered

- **An option naming the zone folder** (the task's planning assumption). Rejected: decision 1's
  reasons - ADR-0007, ADR-0031 decision 8 and ADR-0072's precedent.
- **A zone built in** (a fixed `localhost` or `example.` zone). Rejected: a server that answers
  names nobody configured surprises, and in-memory mode means empty everywhere else (ADR-0031).
- **New schemes `dns+tls`, `doh`, `doq`.** Rejected: ADR-0083 fixes the scheme list at `dns`, and
  the client mates by `--dns-transport`; an option of the same name keeps the two in step.
- **One listen URL per transport through a URL query or fragment.** Rejected: RFC 4501 gives the
  `dns:` URI no such element, and inventing one breaks the URI's own grammar.
- **AXFR served by default.** Rejected: secure by default; a whole zone is exposure, opened by an
  explicit option as directory listings are (ADR-0006 section 2).
- **`NOERROR` with a partial chain for a CNAME loop.** Rejected in decision 2.
- **Advertising 4096 bytes over UDP** (RFC 6891 section 6.2.5's starting point). Rejected for
  1232: fragmentation avoidance, decision 3.
- **Response-rate limiting.** Rejected for now, decision 6.
- **DoH over HTTP/3.** Rejected for now, decision 5.

## Amendment 1 - a listen URL names the one listener it binds (2026-11-11)

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11, in
  BL-1036, found by BL-1017.
- **Amends:** decision 5's "every `dns://` listen URL serves every transport in the list" for the
  engine's part, and [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) decision 6, which
  binds a scheme's connection and datagram servers together on every listen URL of the scheme.

**Context.** `dns` has a connection server (`DnsTcpProtocolServer`) and a datagram server
(`DnsUdpProtocolServer`), so the engine bound both on every `dns` listen URL. `--dns-transport
udp,tls` needs a plain listen URL that binds UDP and no TCP beside a DoT listen URL that binds TCP
with TLS from the first byte and no UDP. Nothing on a listen URL could say so.

**Decision.**

1. `ListenUrl` gains the init-only `OnlyTransport`, a `ListenTransport?`: `Connections`,
   `Datagrams`, or `null` (the default). It is per listen URL, as `IsImplicitTls` and `IsProxy`
   are, and part of the record's equality.
2. `null` keeps the behaviour from before exactly: the connection listener, the datagram listener,
   or both on one port through `IListenerFactory.StartSharedPortListenersAsync` for a scheme with a
   server of each (`https` with `--http3`).
3. `Connections` binds only the connection listener, `Datagrams` only the datagram listener, each
   through `StartConnectionListenerAsync` or `StartDatagramListenerAsync` as a scheme with one server
   is bound.
4. A listen URL naming a listener its scheme has no server for (`tftp` with `Connections`) is
   refused before any listener binds, `UnsupportedProtocol` (1), as a scheme with no server is.
5. **The member names a transport, not a server.** BL-1018 (DoH) and BL-1019 (DoQ) add a second
   connection server and a second datagram server for `dns`, and this member does not choose
   between two servers of one transport. They will either fold the second protocol into one server
   for the transport, chosen per connection by what the listen URL and the handshake already say
   (`IsImplicitTls`, the agreed ALPN `dot` or `h2`/`http/1.1`), or add one more init-only member
   naming the server. Either way is additive: the configuration model only grows (root
   `CLAUDE.md`), so this member keeps its meaning and no caller is rewritten. A label now would
   be a guess at their design before either is planned.

**Consequences.** The composition (BL-1017) can turn one `dns://` URL and `--dns-transport` into one
listen URL per transport: `udp` with `Datagrams`, `tcp` with `Connections`, `tls` with
`Connections` and `IsImplicitTls = true`. Every other scheme is bound as before. A test sees which
listeners a listen URL asked for in the in-memory listener factory's start requests
(`ServingEngineTests.OnlyTransport.cs`).
