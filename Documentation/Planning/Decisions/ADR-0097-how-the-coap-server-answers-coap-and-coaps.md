# ADR-0097 — How the CoAP server answers `coap` and `coaps`

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-470. BL-471 builds the message codec and the server's shape (decisions 0, 2 and 3's
  message-layer rejections), BL-472 resources, methods, options, response codes, reliable
  messaging, `/.well-known/core` and the `coap` access rules (decisions 1, 2, 3, 6 and 7), BL-473
  block-wise transfers (decisions 4 and 7), BL-474 observation (decisions 5 and 7), BL-475 `coaps`
  over `Surl.Dtls` (decision 6), and BL-476 the command line and the composition (decisions 0, 6
  and 7). BL-477 runs the Curl port's client against a live `surl`.
- **Amends:** [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5 (CoAP answers a
  limit in CoAP's own words, decision 7).
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 4's table gains a Coap row: CoAP has no login message, and a `coaps` peer is
  authenticated by its DTLS handshake alone (decision 6).
  [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md),
  [ADR-0087](ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md) and
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) apply as
  written.

## Context

`Surl.Extended.Protocol.Coap` (in `Protocols/Http/`, ADR-0083's table) answers the `coap` and
`coaps` schemes the Curl port adds. Upstream curl has no CoAP, so by ADR-0083 decision 1 the
server is judged by its specifications, not by upstream curl and not by the Curl port. Default
ports are already fixed: 5683 and 5684, RFC 7252 sections 6.1 and 6.2, in
`ExtendedSchemeDefaultPorts` (ADR-0083 decision 7). The requirement matrix
[`Documentation/Conformance/Extended/Coap.md`](../../Conformance/Extended/Coap.md) traces each
server-side statement of the specifications below to the task that proves it.

| Specification | Fixes for a server |
| --- | --- |
| RFC 7252 section 3 | The message format: version, type, token length, code, Message ID, token, options (delta and length, the 13 and 14 extensions, 15 reserved), the payload marker |
| RFC 7252 section 4 | The message layer: CON, NON, ACK, RST; rejecting; Message IDs; duplicates; message size; congestion control; the transmission parameters of section 4.8 |
| RFC 7252 section 5 | Requests and responses: piggybacked and separate responses, tokens, options (critical, elective, repeatable, lengths), payloads, caching (Max-Age, ETag), methods, response codes, each option |
| RFC 7252 sections 6, 7, 8 | `coap` and `coaps` URIs and the URI built from options; default ports and `/.well-known/core`; multicast |
| RFC 7252 section 9 | NoSec, PreSharedKey, RawPublicKey and Certificate modes; CoAP over DTLS |
| RFC 7252 section 11 | Security considerations: amplification, IP spoofing |
| RFC 7959 | Block1, Block2, Size1, Size2; `2.31 Continue`, `4.08 Request Entity Incomplete`, `4.13` with Size1 |
| RFC 7641 | Observe: registration, notifications, sequence numbers, Max-Age freshness, deregistration, congestion |
| RFC 6690 | The CoRE link format `/.well-known/core` is written in, and its query filtering |
| RFC 8132 | FETCH, PATCH and iPATCH: optional, and declined (decision 1) |

The port's matrices `Coap.md` and `Coap-BlockwiseObserve.md` and its ADR-1047, ADR-1057 and
ADR-1064 were read only to
learn what a client sends, so that the server accepts it. Its client sends one CON request per
transfer, `GET`, `POST`, `PUT` or `DELETE` and no other method, with a random 4-byte token,
Uri-Host (the host in lower case unless an IP literal), one Uri-Path per segment, one Uri-Query
per argument, and never Uri-Port; it retransmits from ACK_TIMEOUT 2 s and takes piggybacked or
separate responses. A payload over 1024 bytes goes in Block1 blocks of 1024 bytes (SZX 6), the
next after each `2.31`, following a smaller SZX the server asks for, and restarting at block 0
after a `4.13` that asks for one. It sends no Block2 on a first request and fetches later blocks
at the server's size, sending neither Size1 nor Size2. `--coap-observe` registers with Observe 0,
acknowledges each CON notification, re-registers with a NON GET (same token, Observe 0) 5 seconds
after a notification's Max-Age lapses, deregisters with a NON GET carrying Observe 1, and fetches
a block-wise notification's later blocks with GETs without Observe under another token, answering
a CON notification that arrives meanwhile with RST. Over `coaps` it uses certificate mode only,
offering the suites ADR-0087 records. Nothing below is taken from the port's output, and no
expected byte comes from it.

Surl's own rules this ADR applies: ADR-0004's `IDatagramProtocolServer` and `IDatagramFlow` (one
flow per remote endpoint, as TFTP uses them); ADR-0006's limits and exposure defaults
(`--allow-uploads`, `--list-directories`, `--serve-dot-files`); ADR-0032's secure by default (a
write needs a login, a loosening only by an explicit option); ADR-0084 decision 5's content store
as every file-serving extended server's namespace; ADR-0087's DTLS 1.2 server; and
`ExchangeContext.TimeProvider` as the one clock.

## Decision

### 0. What the server is

- **Two server types, one library.** `CoapProtocolServer` (planning name) serves `coap` and
  implements `IDatagramProtocolServer` and `IDatagramRefusalWriter`. `SecureCoapProtocolServer`
  (planning name) serves `coaps`: it runs ADR-0087's `Surl.Dtls` server session over the flow and
  hands the decrypted `IDatagramFlow` to the same request answering, so every rule below holds over
  both schemes through one code path (ADR-0084 decision 3). It implements no
  `IDatagramRefusalWriter`, as ADR-0087 decision 9 requires of a DTLS listener. Two types, because
  `Surl.Core` decides by a type test whether a server writes refusals (ADR-0006 section 6).
- **A flow is one client endpoint.** Each `IDatagramFlow` is one remote endpoint (ADR-0004
  section 3); the server never calls `MoveToNewLocalPortAsync`, since a CoAP response must come from
  the endpoint the request was sent to (RFC 7252 section 5.3.2). `ServeAsync` serves every message
  from that endpoint until `Surl.Core` cancels the exchange (decision 7). Its message-layer state -
  the duplicate cache, its own Message IDs, outstanding CON messages, Block1 assemblies and
  observations - belongs to the flow and ends with it.
- **Shared state.** The list of observers (decision 5) is one object, `CoapObservations` (planning
  name), owned by the composition and given to both server types, never a static, so a `PUT` over
  `coap` notifies an observer over `coaps` and the other way round. BL-476 creates it once, as
  ADR-0093 decision 0 has BL-501 create the MQTT 5 broker state.
- **Isolation.** The library references `Surl.Protocol.Abstractions`, `Surl.Content` (BL-471) and
  `Surl.Dtls` (BL-475), the references ADR-0084 decisions 3 and 5 allow, and no other server. The
  port's `CoapReferenceServer` and its codec may be copied (ADR-0083 decision 4); their tests are
  not.
- **Randomness and time.** Message IDs, the ACK_RANDOM_FACTOR draw and the names of resources a
  `POST` creates come from an injected random source (`RandomNumberGenerator` in production); every
  timer runs on `ExchangeContext.TimeProvider`. No `Thread.Sleep`, no wall clock.

### 1. Resources, methods, options and representations (RFC 7252 sections 5.8, 5.10, 6.5; RFC 8132)

- **The resource tree is the content store.** A request's Uri-Path options, each percent-encoded
  as RFC 7252 section 6.5 step 6 builds a path and joined with `/`, are handed to
  `ContentStore.MapRequestPath`, so every exposure rule of ADR-0006 section 2 applies unchanged:
  dot-files, `/.surl`, links that are not followed and paths outside the root are answered as
  absent, `4.04`. A resource is a visible regular **file**; a directory is not a resource and is
  answered `4.04` to every method but `POST` (below). Uri-Query is ignored on a file, as a query is
  on a static HTTP file. Uri-Host and Uri-Port are accepted and ignored: one `surl` listener is one
  origin, whatever authority the client names (section 5.10.1 lets the client omit both).
- **`/.well-known/core`** is answered by the server itself, before the content store, so a
  `.well-known` directory on disk is never served at that path (decision 1's link format below).
- **Methods.** Codes 0.01 to 0.04 are served:

  | Method | Answer on success | Needs |
  | --- | --- | --- |
  | `GET` | `2.05 Content` with the file's bytes, Content-Format, ETag and Max-Age; `2.03 Valid` with ETag and Max-Age and no payload when a request ETag matches (section 5.10.6.2) | read access (decision 6) |
  | `PUT` | the payload replaces the file through `ContentStore.WriteUploadAsync`: `2.04 Changed` when a file was there, `2.01 Created` when not (section 5.8.3) | write access and `--allow-uploads` |
  | `POST` to a file | the payload is appended through `ContentStore.AppendUploadAsync`: `2.04 Changed`, or `2.01 Created` when the file did not exist (section 5.8.2 leaves the effect to the server; appending makes `POST` the non-idempotent write it is defined to be) | write access and `--allow-uploads` |
  | `POST` to a directory | a new file named by 32 lower-case hex digits of 16 random bytes is created inside it: `2.01 Created` with one Location-Path option per segment of the new path (sections 5.8.2 and 5.10.7) | write access and `--allow-uploads` |
  | `DELETE` | the file is deleted through `ContentStore.DeleteFile`: `2.02 Deleted`, also when nothing was there or the path is hidden (section 5.8.4, which makes the absent case a success); `4.05` for a visible directory | write access and `--allow-uploads` |

  Every other method code (0.05 to 0.31) is `4.05 Method Not Allowed`, piggybacked (section 5.8).
  That includes RFC 8132's **FETCH, PATCH and iPATCH, which are not served**: a file has no query
  language for FETCH to select with and no patch format for PATCH to apply, RFC 8132 is optional,
  and no client in scope sends them. `5.01 Not Implemented` is therefore never sent: section 5.8
  names `4.05` as the answer to an unsupported method. A write refused because `--allow-uploads` is
  off is `4.05` too, ADR-0006 section 2's "not permitted" answer, mirroring HTTP's `405`.
- **Content-Format** (sections 5.10.3, 12.3) is chosen from the file name's extension, compared
  ordinally ignoring case: `.txt` 0 (`text/plain;charset=utf-8`), `.xml` 41, `.json` 50, `.cbor`
  60, `.exi` 47, every other file 42 (`application/octet-stream`); `/.well-known/core` is 40
  (`application/link-format`). The store keeps bytes only, so a `PUT` or `POST`'s Content-Format is
  not stored and is not checked against the extension: `4.15` is never sent. A request **Accept**
  naming any other format than the resource's is `4.06 Not Acceptable` (section 5.10.4).
- **ETag** (section 5.10.6): the first 8 bytes of SHA-256 over the file's length and its
  last-write time in UTC ticks, each as 8 big-endian bytes, so it changes whenever
  `ContentStore.GetFileStatus` says the file changed, and costs no read of the file. For
  `/.well-known/core`, the first 8 bytes of SHA-256 over the link-format payload. One ETag per
  response, on every `2.05` and `2.03`.
- **Max-Age** (section 5.10.5): **60**, sent explicitly on every `2.05` and `2.03`, because RFC 7641
  section 4.3.1 requires notifications to carry it and one value for all responses keeps a cached
  `GET` and a notification alike. 60 is the option's own default, so a cache sees what it would
  have assumed. `5.03` carries its own Max-Age (decision 7).
- **Conditional requests** (section 5.10.8): If-Match and If-None-Match are honoured on every
  method; a condition that fails is `4.12 Precondition Failed` and the method is not performed. An
  empty If-Match matches any existing file. On a Block1 transfer they are checked on block 0 only
  (RFC 7959 section 2.10).
- **`/.well-known/core`** (RFC 7252 section 7.2, RFC 6690) is answered only under
  `--list-directories`; without it the resource is `4.04`, ADR-0006 section 2's refused listing
  answered as absent, so discovery is off by default as every other listing is. With it, a `GET`
  answers `2.05` with Content-Format 40 and one link per visible file of the store, in ordinal
  order of path, each `</path>;ct=<format>;sz=<length>;obs` (RFC 6690 section 3.1 `sz`, RFC 7641
  section 6 `obs` without a value), path segments percent-encoded as RFC 3986 requires, at most
  **1000 links**, the first 1000 in that order (ADR-0006: every answer a peer can grow is bounded).
  The query filter of RFC 6690 section 4.1 is supported: one `name=value` or `name=prefix*`, on
  `href` or any attribute above. The resource is not observable (decision 5) and a write to it is
  `4.05`.

### 2. The message layer (RFC 7252 sections 2, 4 and 5.2)

- **Transmission parameters** are RFC 7252 section 4.8's defaults, constants with no option:
  ACK_TIMEOUT 2 s, ACK_RANDOM_FACTOR 1.5, MAX_RETRANSMIT 4, NSTART 1, and the derived
  EXCHANGE_LIFETIME 247 s and NON_LIFETIME 145 s (section 4.8.2). Section 4.8.1 forbids lowering
  ACK_TIMEOUT or raising NSTART without congestion-control measurements the server does not keep.
- **CON request.** Answered by a **piggybacked** response in the ACK carrying the request's Message
  ID and token (section 5.2.1) when the answer is ready within **1 second**. Otherwise an Empty ACK
  goes at 1 second - half ACK_TIMEOUT, so before the client's first retransmission - and the answer
  follows as a **separate CON response** with the request's token and a new Message ID (section
  5.2.2). Only slow content-store work (a large write or commit, a slow disk) takes the separate
  path; the 1-second wait is on the `TimeProvider`.
- **NON request.** Answered with a NON response, never an ACK (sections 4.3 and 5.2.3).
- **Retransmission of the server's CON messages** (separate responses and notifications, section
  4.2): the first timeout is a random duration between ACK_TIMEOUT and ACK_TIMEOUT ×
  ACK_RANDOM_FACTOR, doubled after each of up to MAX_RETRANSMIT retransmissions; a matching ACK or
  RST stops it (section 5.2.2), any code or payload in the ACK ignored. After the last timeout the
  message is given up: a separate response is dropped, an unacknowledged notification removes its
  observer (decision 5). At most **one** CON is outstanding to a flow at a time (NSTART 1, section
  4.7; RFC 7641 section 4.5.1); the next waits until it is acknowledged, reset or given up.
- **Message IDs** the server originates start at a random 16-bit value per flow and increase by one
  per new message, so none is reused within EXCHANGE_LIFETIME (section 4.4).
- **Duplicates** (section 4.5): a CON whose Message ID the flow saw within EXCHANGE_LIFETIME gets the
  same ACK or RST again, byte for byte, and its request is not served twice; once an Empty ACK was
  sent for it, a duplicate gets another Empty ACK and the response stays separate (section 5.2.2).
  A duplicate NON within NON_LIFETIME is silently ignored. The cache holds the bytes of each answer
  and is bounded (decision 7).
- **Rejecting** (sections 4.2 and 4.3): a CON the server lacks context for - an Empty CON (CoAP
  ping), a code of class 1, 6 or 7, a response code, or a message format error - is answered with
  an Empty RST echoing its Message ID. The same NON is silently ignored: **the server never sends RST
  to a NON**, which section 4.3 allows and section 8.1 requires for multicast, so the server need
  not know how a datagram was addressed. A message with a version other than 1 is silently ignored
  whatever its type (section 3). A datagram too short for the 4-byte header is ignored.
- **ACK and RST from the client** match the server's CON by Message ID within the flow (section
  4.4). An unmatched one, an ACK carrying a request or a reserved class, and a RST that is not Empty
  are silently ignored (section 4.2). A RST matching a notification removes its observer (decision
  5).
- **Format errors** (sections 3, 3.1, 4.1): token length 9 to 15, option delta or length nibble 15
  outside the payload marker, a payload marker with no payload, an option running past the
  datagram, and an Empty message with a token or any byte after the Message ID.

### 3. Options and the error codes (RFC 7252 sections 5.4, 5.5, 5.9 and 5.10)

- **Recognised options**: If-Match (1), Uri-Host (3), ETag (4), If-None-Match (5), Observe (6),
  Uri-Port (7), Uri-Path (11), Content-Format (12), Uri-Query (15), Accept (17), Block2 (23),
  Block1 (27), Size2 (28), Proxy-Uri (35), Proxy-Scheme (39) and Size1 (60). Any other option, an
  option not defined for the request's method (section 5.4: Observe on anything but `GET`, ETag as
  a request option on anything but `GET`, Block1 on `GET` or `DELETE`), an option whose length is
  outside its range (section 5.4.3), and each supernumerary occurrence of an option that is not
  repeatable (section 5.4.5) are **unrecognised**: elective ones (even numbers) are ignored
  (section 5.4.1); a critical one (odd) in a CON request is `4.02 Bad Option`, and in a NON request
  rejects the message, which is silently ignored (sections 5.4.1 and 4.3). A request carrying a
  reserved Location-* option (128, 132, 136, 140) is `4.02` (section 5.10.7). Leading zero bytes in
  a uint are read (section 3.2), and the server writes every uint in the fewest bytes.
- **The order of checks**, first match answers: message layer (decision 2); `4.02` for an
  unrecognised critical option; `4.00 Bad Request` for a Uri-Path of `.` or `..` (section 5.10.1),
  a Block option with SZX 7 (RFC 7959 section 2.2) or a Block1 block with M set whose payload is
  not its size (RFC 7959 section 2.3); `5.05 Proxying Not Supported` for Proxy-Uri or Proxy-Scheme
  (section 5.10.2) unless its scheme, host and port name this listener, in which case the request
  is served as local (section 5.7.2); `4.05` for an unsupported method; `4.01 Unauthorized`
  (decision 6); `4.05` for a write without `--allow-uploads`; then the resource: `4.04`, `4.12`,
  `4.06`, `4.13`, `4.08`, and success. A peer refused at `4.01` learns nothing about the resource,
  as ADR-0032 section 4 orders HTTP's checks.
- **Response codes sent**, and nothing else: `2.01`, `2.02`, `2.03`, `2.04`, `2.05`, `2.31`, `4.00`,
  `4.01`, `4.02`, `4.04`, `4.05`, `4.06`, `4.08`, `4.12`, `4.13`, `5.00 Internal Server Error` (a
  content-store failure the server cannot map, with an empty payload), `5.03 Service Unavailable`
  (decision 7) and `5.05`. Never sent: `4.03` (no per-resource access exists), `4.15` (decision 1),
  `5.01` (decision 1), `5.02` and `5.04` (no proxy).
- **Diagnostic payloads** (sections 5.5.2, 5.9.2, 5.9.3): fixed UTF-8 text from the server's own
  table, never a path, an exception or an account (ADR-0006 section 3). `4.02` carries
  `Unrecognized option <number>`, naming the first unrecognised critical option's number; every
  other error has an empty payload, as section 5.5.2 asks when the code says it all.
- **`4.13 Request Entity Too Large`** always carries Size1, the largest entity accepted
  (`--max-filesize`, RFC 7252 section 5.9.2.9, RFC 7959 section 4).

### 4. Block-wise transfers (RFC 7959)

- **Preferred block size: 1024 bytes, SZX 6** (RFC 7959 section 2.2's largest). It keeps a block
  plus header and options inside RFC 7252 section 4.6's 1152-byte message bound and ADR-0087
  decision 7's 1200-byte DTLS datagram; over `coaps`, when the client's `max_fragment_length`
  leaves less room, the server uses the largest SZX whose block and 64 bytes of header and options
  fit, which `Surl.Dtls`'s session reports (BL-475). The amplification a 1024-byte answer to a
  short request gives (RFC 7252 section 11.3) is accepted: section 4.6 itself names 1024 bytes as
  the payload to assume, the port's client asks for no smaller size, and `surl` binds only the
  address its listen URL names (ADR-0006 section 2).
- **Block2** (sections 2.3, 2.4): a representation longer than the block size is answered in
  blocks; the first response carries Block2 NUM 0 with M set, and Size2 with the whole length
  (section 4's "preferably with the first block"). A request's Block2 is control usage: its M bit is
  ignored; its SZX, when smaller than the server's, is used, so all later blocks share one size; its
  NUM selects the bytes at `NUM << (SZX + 4)`, read from the store as a byte range. Answering is
  **stateless**: each block is read afresh and carries the file's current ETag, so a client sees a
  change between blocks (sections 2.4 and 7.1). A NUM whose first byte lies past the end is `4.02`,
  because the option names no part of the representation (RFC 7252 section 5.9.2.3). A request
  with **Size2 0** gets Size2 with the length, with or without Block2 (section 4).
- **Block1** (sections 2.3, 2.5): uploads are **atomic**. Block 0 with M set opens an upload session
  (`ContentStore.OpenUploadAsync`, a temporary file beside the target) keyed by the flow and the
  request's Uri-Path and Uri-Query; each block with M set is written at its offset and answered
  `2.31 Continue` with Block1 echoing NUM, M set, and the SZX in use; the block with M unset commits
  the session and gets the method's final code with Block1 (M unset). A new block 0 for the same key
  replaces the old session (section 2.5). A block not at the next offset, a block arriving with no
  session, or a block whose Content-Format differs from block 0's is `4.08 Request Entity
  Incomplete` and the session is discarded (sections 2.3 and 2.5). A retransmitted block with a new
  Message ID repeating the last NUM is answered `2.31` again and not written twice. A session with
  no block for EXCHANGE_LIFETIME is discarded (section 2.5), as is every session when its flow ends;
  the temporary file is deleted either way.
- **Size and refusals**: a Size1 on block 0 above `--max-filesize`, or a running total passing it,
  is `4.13` with Size1 and the session discarded (section 2.9.3, ADR-0006 section 1's "say the
  limit before the body arrives"). A single-message `PUT` or `POST` payload is held to the same
  limit.
- **Block1 and Block2 together** (section 2.7) never arise: no `PUT` or `POST` response here has a
  body.
- **Observe and Block2** (section 2.6): decision 5.

### 5. Observe (RFC 7641)

- **What is observable:** every resource a `GET` answers `2.05` for, that is every visible file.
  `/.well-known/core` is not: a registration on it, as on a missing file, gets the plain response
  without Observe (section 4.1).
- **Registration** (section 4.1): a `GET` with Observe 0, and no Block2 or Block2 NUM 0, adds an
  entry keyed by the flow (the client endpoint) and the token; an entry with the same endpoint and
  token is replaced, never duplicated, and keeps its sequence numbers. The response is the `2.05`
  with an Observe option. A registration past the observer limits (decision 7) is served as a plain
  `GET`, without Observe (section 4.1's "unable or unwilling").
- **Notifications** (section 4.2): `2.05` with the registration's token, Observe, Content-Format
  (unchanged, since it follows the file name), ETag, Max-Age 60, and the representation, or only its
  first block, at the registration's block size, with Size2, when it is larger (RFC 7959 section
  2.6). The client fetches later blocks with ordinary `GET`s. `2.03` notifications (section 4.3.2)
  are not sent.
- **When** (sections 4.3.1 and 4.5):
  - at once after a `PUT`, `POST` or `DELETE` through either server type changes the file, from any
    flow (the shared `CoapObservations`, decision 0);
  - when the last notification's Max-Age (60 s) lapses: the file's status is read again and a
    notification sent whether or not it changed, so the client's representation stays fresh and a
    change made outside CoAP (by another protocol server or on disk), which the content store raises
    no event for, reaches the observer within 60 seconds. This meets section 4.5's "eventually
    observes the latest state".
  - A deleted file gets one `4.04` notification without Observe and the entry is removed
    (section 4.2).
- **Every notification is CON.** That gives section 4.5's "a CON at least every 24 hours" with
  room to spare, the acknowledged interest RFC 7641 section 7 asks for between notifications when
  the client is not authenticated, and one rule for all. With NSTART 1 (decision 2) a change while a
  notification is outstanding is held, and only the newest state is sent once it is acknowledged
  (section 4.5 allows skipping intermediate states).
- **Sequence numbers** (section 4.4): per entry, 24 bits, the registration response's number 1,
  then one more for each new notification, assigned when the notification is first sent, so a
  retransmission carries the number still current. One outstanding CON per flow keeps the rate far
  below 2^23 in 256 seconds; the counter wraps modulo 2^24, which section 3.4's comparison handles.
- **Deregistration** (sections 3.6, 4.1, 4.5): an RST matching a notification; a CON notification
  given up after MAX_RETRANSMIT; a `GET` with Observe 1 and the entry's token, which is then served
  as a plain `GET` without Observe; a non-2.xx notification; and the end of the observer's flow
  (decision 7), which sends nothing.
- **`coaps`**: notifications go in the same DTLS session and epoch as the registration (RFC 7641
  section 7, RFC 7252 section 9.1.2); since ADR-0087 never leaves epoch 1, this holds by
  construction.

### 6. Security: `coap`, `coaps`, and who may read and write (RFC 7252 section 9)

- **`coap` is NoSec** (section 9): no peer is authenticated, and a source address can be spoofed
  (section 11.4). Under ADR-0032's secure by default, CoAP is treated as HTTP is (ADR-0032 section 4),
  not as the four protocols "insecure by design" (TFTP, Gopher, DICT, TELNET), which are a closed
  list:

  | Peer | Reads (`GET`, `/.well-known/core`) | Writes (`PUT`, `POST`, `DELETE`) |
  | --- | --- | --- |
  | Any, under `--allow-anonymous` | served | served under `--allow-uploads`, else `4.05` |
  | Authenticated (below), no `--allow-anonymous` | served | served under `--allow-uploads`, else `4.05` |
  | Unauthenticated, no account configured, no `--allow-anonymous` | served | `4.01 Unauthorized` |
  | Unauthenticated, an account configured, no `--allow-anonymous` | `4.01` | `4.01` |

  So **a write over `coap` needs both `--allow-uploads` and `--allow-anonymous`**: a NoSec write is
  an anonymous write, and anonymity is loosened only by the option that says so. Configuring an
  account makes the server private, as it does HTTP, so plain `coap` then serves nothing without
  `--allow-anonymous`. `4.01` is section 5.9.2.2's "not authorized"; no challenge exists in CoAP.
- **`coaps`** runs ADR-0087's handshake in its three modes: Certificate and RawPublicKey from
  `--cert`/`--key` or `--self-signed`, PreSharedKey from the `--user` and `--user-file` accounts
  (identity the name's UTF-8 bytes, key the password's), starting only with a certificate or an
  account (ADR-0087 decision 3, refused otherwise with `CertificateProblem` (58)). **A peer is
  authenticated** - the question ADR-0087 decision 3 leaves to this ADR - when its handshake used a
  PSK identity (it can only finish with a matching account) or presented a client certificate that
  chains to a `--cacert` anchor (ADR-0010 section 5). A certificate or RawPublicKey handshake
  without a client certificate encrypts but does not authenticate: that peer is unauthenticated in
  the table above. A PSK peer's account name is written in the `-v` log as ADR-0032 section 8's
  `Login accepted: <method> <user>` note, with the method named `psk` (a new method name; no
  `--auth` word is added, since `--auth` governs only the methods ADR-0032 section 3 lists).
- **How the server learns the rules.** CoAP has no login message, so the server calls no
  `IAuthenticationPolicy` member. BL-476 gives it a `CoapAccessRules` record (planning name) built
  from the command line: whether `--allow-anonymous` is on and whether any account is configured;
  `--allow-uploads` is already `ContentExposureOptions.AllowUploads`, which the content store
  enforces too. The PSK lookup is the caller-given one ADR-0087 decision 3 and BL-415 define in
  `Surl.Dtls`; BL-476 builds it in `Surl.Console` from the same `Account` list it gives
  `Surl.Authentication`. This is ADR-0084 decision 4's new Coap row: no contract is added to
  `Surl.Protocol.Abstractions`, and the library never references `Surl.Authentication`.
- **Before the handshake** (BL-475): a datagram on a `coaps` flow before the handshake finishes is
  handled by `Surl.Dtls` alone; no CoAP message is read from it. When a client's new hello replaces
  an association (ADR-0087 decision 6), the old association's CoAP state - duplicates, outstanding
  CONs, Block1 sessions and observations - ends with it, since a response must use the request's
  session and epoch (RFC 7252 sections 9.1.1 and 9.1.2).
- **Multicast** (section 8): the server joins no multicast group and serves only the address its
  listen URL names; it never answers a NON with RST (decision 2), so section 8.1's rule holds
  however a datagram arrived.

### 7. Limits (ADR-0006) per flow

| Limit | How the CoAP server applies it | Answer when hit |
| --- | --- | --- |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s; a flow is one client endpoint | `coap`: `CoapProtocolServer.WriteRefusalAsync` answers a first datagram that decodes as a CON request with an ACK carrying `5.03 Service Unavailable` and Max-Age 5 (retry after 5 s, section 5.9.3.4), a NON request with a NON `5.03`; anything else gets nothing. `coaps`: nothing (ADR-0087 decision 9). |
| `--idle-timeout` (120 s) | `Surl.Core`'s; the server's sends restart it, so an observed flow stays alive on its 60-second Max-Age notifications | the flow ends with no bytes; its observations, Block1 sessions and duplicate cache go with it. A duplicate that arrives later starts a new flow and is served again (below). |
| `--max-time` (3600 s) | `Surl.Core`'s, a flow's whole life | the flow ends with no bytes; an observer re-registers on a new flow. |
| `--head-timeout` (30 s) | `coap`: does not apply, a request is one whole datagram. `coaps`: bounds the DTLS handshake (ADR-0087 decision 9) | as ADR-0087 decision 9 says. |
| `--max-message` (1 MiB) | the largest CoAP message accepted, the whole datagram (after DTLS decryption on `coaps`); the default is above UDP's 65507 bytes, so it bites only when set lower | a CON: `4.13` with Size1 (`--max-filesize`) and, when a block of at least 16 bytes and 128 bytes of header and options fit in the limit, a Block1 option with the largest such SZX as the hint RFC 7959 section 2.9.3 describes; a NON: ignored. |
| `--max-filesize` (100 MiB) | a single-message payload, a Block1 total, a Size1 announced on block 0 | `4.13` with Size1 (decision 4); the partial upload is deleted. |
| `--max-request-head`, `--max-line` | do not apply: CoAP has no text head or lines | none |

The server's own per-flow and per-server bounds, constructor parameters with these defaults and
**no new option**, as ADR-0093 decision 8 decides for the MQTT 5 broker's stores:

| Bound | Default | Why | When hit |
| --- | --- | --- | --- |
| Duplicate cache entries per flow | 128 | each holds an answer of at most about 1.2 KB; 128 bound a flow near 150 KiB and 1024 flows near 150 MiB | the oldest entry is dropped early; its Message ID is then served again if it repeats, which section 4.5 tolerates for the idempotent methods |
| Requests in progress per flow (exchanges in flight) | 8 | a client keeps NSTART 1 (section 4.7), plus block fetches beside an observation; 8 leaves room without letting one endpoint queue work | a CON: ACK with `5.03`, Max-Age 5; a NON: ignored |
| Outstanding server CON messages per flow | 1 | NSTART (section 4.7, RFC 7641 section 4.5.1) | the next waits (decision 2) |
| Block1 upload sessions per flow | 4 | each holds a temporary file up to `--max-filesize` | `4.13` with Size1 (RFC 7959 section 2.5: no resources to store blocks) |
| Observations per flow | 16 | one endpoint watching many files is unusual; each entry holds a timer | the registration is served as a plain `GET` (decision 5) |
| Observations per server | 1000 | the shared list is a store a peer can fill (ADR-0006) | the same |

- **Duplicates across a flow's end.** The idle timeout (120 s) is shorter than EXCHANGE_LIFETIME
  (247 s), so a CON retransmitted after its flow idled out reaches a new flow and is served again.
  RFC 7252 section 4.5 makes duplicate detection a SHOULD and lets it relax for idempotent requests;
  only a repeated `POST` append is affected, and a client that retransmits for over two minutes
  without an answer has already met MAX_TRANSMIT_WAIT (93 s). Accepted rather than lengthening every
  flow's life.
- No limit ends the process or changes its exit code (ADR-0006 section 5).

## Alternatives considered

- **Serve FETCH, PATCH and iPATCH (RFC 8132).** Rejected (decision 1): a file has no query or
  patch semantics, the RFC is optional, and no client in scope sends them.
- **Writes over `coap` with `--allow-uploads` alone, as TFTP's.** Rejected (decision 6): ADR-0032's
  "insecure by design" list is closed, CoAP is not on it, and a NoSec write is anonymous and its
  source spoofable (RFC 7252 section 11.4); `--allow-anonymous` is the option that says so.
- **Writes only over `coaps`.** Rejected: it would make `coap` writes impossible even when the
  operator asks for them explicitly, and the explicit options already carry the decision.
- **NON notifications with a CON every 24 hours.** Rejected (decision 5): RFC 7641 section 7 asks
  for acknowledged interest between notifications to an unauthenticated client anyway, and all-CON
  is one rule with no rate estimate to keep.
- **Stateful Block2 (caching the representation per client).** Rejected (decision 4): stateless
  blocks with ETags cost no memory a peer can fill, which RFC 7959 section 7.1 recommends.
- **`/.well-known/core` always on.** Rejected (decision 1): it is a listing of the store, and
  ADR-0006 section 2 keeps listings off until `--list-directories`; RFC 7252 section 7.2's SHOULD
  allows "fully manual configuration".
- **A new `IAuthenticationPolicy` member for CoAP access.** Rejected (decision 6): there is no
  credential to check; two booleans and ADR-0087's PSK lookup are the whole of it.

## Consequences

- `surl coap://127.0.0.1:0/ --directory <path>` serves the files under `<path>` to `GET`, with
  block-wise transfers and observation; `--allow-uploads --allow-anonymous` add `PUT`, `POST` and
  `DELETE`; `--list-directories` adds `/.well-known/core`. `surl coaps://...` does the same over
  DTLS 1.2, where a PSK or client-certificate peer writes without `--allow-anonymous`.
- BL-477's integration `PUT` over `coap` passes `--allow-uploads --allow-anonymous`.
- The requirement matrix [`Coap.md`](../../Conformance/Extended/Coap.md) traces every server
  statement of RFC 7252, RFC 7959 and RFC 7641, and the RFC 6690 rules `/.well-known/core` relies
  on, to BL-471 to BL-476; each task replaces its ID with the named test, and BL-477 fills the rest.
- BL-476 adds the `coap` help category and `--aihelp` topic (both schemes), naming RFC 7252, RFC
  7959 and RFC 7641 as the oracle and saying the server is not validated against upstream curl
  (ADR-0083 decision 8).
- BL-475 needs `Surl.Dtls`'s session to report its usable record size and the peer's PSK identity
  or verified client certificate; BL-414 and BL-415 build those reports (ADR-0087 decision 3, "Who
  the peer is").
