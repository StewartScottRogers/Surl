# ADR-0114 — How the XMPP server answers `xmpp` and `xmpps`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-535. BL-536 builds the server with its XML stream reader, stream headers, stream errors
  and limits (decisions 0, 1, 6 and 7), BL-537 negotiates STARTTLS and serves `xmpps` (decision 2),
  BL-538 runs SASL and binds resources (decisions 3 and 4), BL-539 routes stanzas and answers IQs
  (decisions 5 and 6), and BL-540 serves both schemes from `surl`.
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; port 5222 for `xmpp`, 5223 for
  `xmpps`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 4 (an XMPP login reads the shared accounts through `ISaslAuthenticationPolicy`, authcid =
  name), [ADR-0101](ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md)
  and [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  (`--mail-domain`), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (secure by default, the refusal delay, refusals that name nothing),
  [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) (the SASL contracts and
  `--allow-anonymous` judging credentials unchecked), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)
  (limits and fixed error text) and [ADR-0010](ADR-0010-the-server-side-tls-contract.md) with
  [ADR-0069](ADR-0069-one-name-for-the-tls-upgrade-flag-istlsupgradeavailable.md) (TLS and its in-band upgrade
  through the listener seam).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.Xmpp` (in `Protocols/Messaging/`, ADR-0083's table) answers the `xmpp` and
`xmpps` schemes the Curl port adds, on ports 5222 and 5223 (ADR-0083 decision 7). By ADR-0083
decision 1 it is judged by its specifications, not by upstream curl, which has no XMPP, and not by
the Curl port. The specifications are:

- **RFC 6120** (XMPP Core): section 4 (streams, stream errors), 5 (STARTTLS), 6 (SASL), 7 (resource
  binding), 8 (stanzas, stanza errors), 9 (examples), 10 (server rules for processing stanzas), 11
  (XML usage) and 13.12 (stanza size);
- **RFC 6121** (XMPP IM): section 5 and 8.5 (message delivery), section 4 for the little presence
  served, section 2.1.3 for the roster get answered;
- **RFC 7622** (XMPP address format) for what a valid localpart and resourcepart are;
- **XEP-0030** (service discovery) sections 3 and 4, **XEP-0199** (ping) section 4.2, and
  **XEP-0368** (direct TLS) for `xmpps`;
- **RFC 4616** (PLAIN), **RFC 4505** (ANONYMOUS), **RFC 5802** (SCRAM-SHA-1) and **RFC 7677**
  (SCRAM-SHA-256), whose exchanges `Surl.Authentication` already owns (BL-416 added SCRAM).

The port's `Curl.Extended.Protocol.Xmpp.UnitLibrary` and its matrix `Xmpp.md` were read only to
learn what a client sends, so the server accepts it: a stream header with `to` and `version='1.0'`
and no `id`, `<starttls/>` when offered (required or not), `<auth/>` choosing SCRAM-SHA-256 over
SCRAM-SHA-1 over PLAIN, `=` for an empty initial response with ANONYMOUS, a stream restart after
TLS and after `<success/>`, an empty `<bind/>` asking the server for the resource, then a
`<message/>` to the path's JID or a disco#info `<iq type='get'/>`, then `</stream:stream>`; on
`xmpps` the TLS handshake from its first byte. Nothing below is taken from the port's output, and
no expected byte comes from it.

## Decision

### 0. What the server is

- **One XMPP client-to-server service**, RFC 6120 and the message half of RFC 6121. No
  server-to-server federation (a stanza for another domain is `remote-server-not-found`, decision
  5), no multi-user chat, no rosters with subscriptions, no offline storage, no stream management
  (XEP-0198), no BOSH or WebSocket transport, no in-band registration.
- **Sessions are the server instance's own.** Routing is between the streams one `XmppServer`
  holds, in a session registry it is constructed with (not a static). XMPP is not on the message
  bus: ADR-0100 decision 3 maps no XMPP address onto it, and a chat message addressed to a JID has
  no topic to fan out to.
- **The library** references `Surl.Protocol.Abstractions` only, never another protocol server.
  SASL mechanisms are `Surl.Authentication`'s, reached through `ISaslAuthenticationPolicy`
  (ADR-0049 section 6). XML is read with the BCL's `XmlReader` (`DtdProcessing.Prohibit`,
  `XmlResolver` null, `ConformanceLevel.Fragment`, `Async`, `IgnoreComments` false and
  `IgnoreProcessingInstructions` false so decision 6 can see and refuse them) and written with
  `XmlWriter`; time comes from `ExchangeContext.TimeProvider`; stream IDs and generated names from
  an injected random source.

### 1. The domain and the stream header (RFC 6120 4.2, 4.3, 4.7)

- **The domains served** are, lower-cased:
  - the listen URL's host, when it is a name or a specific address (an IP literal is a valid
    domainpart, RFC 7622 3.2);
  - when the listen host is a wildcard address (`0.0.0.0`, `::`), instead the local address the
    connection arrived on, as an IP literal (brackets around IPv6, as RFC 7622 3.2 writes it);
  - when the listen host is `localhost` or a loopback address, both `localhost` and that loopback
    literal, so `xmpp://localhost` and `xmpp://127.0.0.1` reach the same server;
  - every `--mail-domain` (ADR-0101, ADR-0102), so one domain serves mail and chat, and a
    `--mailbox` account `alice@example.com` is the JID `alice@example.com` (decision 3).
- **`to`.** A stream header whose `to` names a domain served opens a stream for that domain. A
  `to` naming any other domain is the stream error `host-unknown` (RFC 6120 4.9.3.6). A header
  with no `to` opens a stream for the first domain in the list above (RFC 6120 4.7.2 makes `to` a
  SHOULD, and a client that leaves it out has one domain to mean).
- **The response header** (RFC 6120 4.7) carries `from` = the stream's domain, `id` = 128 random
  bits as 32 lower-case hex digits, new for every stream including each restart (4.7.3), `version`
  = `1.0`, `xml:lang` = the client's `xml:lang` when it gave one, else `en` (4.7.4), the stream
  namespace `http://etherx.jabber.org/streams` and the default namespace `jabber:client`. No `to`:
  the client has no address until it binds (4.7.2 says the server SHOULD echo a `from` the client
  gave; a client stream header's `from` is accepted and not echoed, because before authentication
  it is only a claim). The header is preceded by `<?xml version='1.0'?>`.
- **`version`.** `1.0`, or a higher major or minor, opens a `1.0` stream (4.7.5: the lower of the
  two). A missing `version` or a major below 1 is `unsupported-version` (4.9.3.25): this server
  speaks no pre-RFC protocol.
- **Namespaces.** A stream element in a namespace other than `http://etherx.jabber.org/streams`,
  or a default namespace other than `jabber:client`, is `invalid-namespace` (4.9.3.10). A first
  element that is not `stream` is `not-well-formed` (4.9.3.13): it is not a stream at all.
- **Closing** (4.4): `</stream:stream>` from the client is answered `</stream:stream>`, then the
  connection is closed gracefully once written; after the server sends a stream error it sends
  `</stream:stream>` and closes the same way (4.9.1.2).

### 2. Stream features and TLS (RFC 6120 4.3.2, 5; XEP-0368)

| Phase | Features sent |
| --- | --- |
| `xmpp`, before TLS, a certificate configured | `<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'><required/></starttls>` alone |
| the same, under `--allow-plaintext-auth` | `<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>` (no `<required/>`) and the SASL `<mechanisms/>` decision 3 offers in plain text |
| `xmpp` with no certificate (only allowed under `--allow-plaintext-auth` or `--allow-anonymous`, below) | the SASL `<mechanisms/>` decision 3 offers in plain text; no `<starttls/>` |
| after TLS (`xmpp` after STARTTLS, `xmpps` from the start) | `<mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>` (decision 3); never `<starttls/>` again (5.4.3.3) |
| after SASL success | `<bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>` alone; no legacy `<session/>` (RFC 6121 appendix E) |
| after binding | nothing more: features are only sent after a stream header |

- **STARTTLS is required by default on `xmpp`.** The plain-text explicit option is the existing
  `--allow-plaintext-auth` (FR-028, ADR-0032), the option every server already uses to let a login
  cross plain text; no new option is added. Under it STARTTLS is still offered, as voluntary.
- **Before TLS when it is required** (5.3.1, 5.4.3.1): `<auth/>` is answered
  `<failure xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><encryption-required/></failure>` and the
  stream kept (6.5.4); any stanza is the stream error `not-authorized` (4.9.3.12); any other
  first-level element is `policy-violation` (4.9.3.14).
- **`<starttls/>`** (5.4.2): answered `<proceed xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>`, then
  `UpgradeToTlsAsync` through the listener seam (ADR-0069). If the seam cannot upgrade, or
  `<starttls/>` arrives a second time or after TLS, the answer is
  `<failure xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>`, then `</stream:stream>` and close (5.4.2.2).
  A failed handshake closes with no bytes (5.4.3.2). After TLS the client opens a new stream
  without closing the old one, and the server answers a new header with a new `id` (5.4.3.3).
- **`xmpps`** (XEP-0368): the TLS handshake runs from the first byte, by the listener seam's
  implicit TLS (ADR-0010), before any XML; ALPN `xmpp-client` is accepted when offered and not
  required. The stream then starts at the "after TLS" phase; STARTTLS is never offered.
- **Certificates.** TLS 1.2 and 1.3 (ADR-0006 section 4), `--cert` or `--self-signed`, no client
  certificate asked for. `xmpps` without a certificate fails at start with `CertificateProblem`
  (58). `xmpp` without a certificate fails at start the same way unless `--allow-plaintext-auth`
  or `--allow-anonymous` is given: without either, no login could ever succeed, and a server that
  can only refuse is a configuration error caught early.

### 3. SASL (RFC 6120 6; ADR-0100 decision 4)

- **Mechanisms offered**, in this order: `SCRAM-SHA-256` (RFC 7677), `SCRAM-SHA-1` (RFC 5802),
  `PLAIN` (RFC 4616), then `ANONYMOUS` (RFC 4505) only under `--allow-anonymous`. The list is
  `ISaslAuthenticationPolicy.GetSaslMechanisms` for the connection's TLS state, filtered to those
  four: so `PLAIN` and the SCRAM mechanisms appear in plain text only under
  `--allow-plaintext-auth`, as for every server. No `-PLUS` (channel-binding) variants: the SCRAM
  exchanges advertise no channel binding (gs2 header `n` or `y` is accepted, `p` refused). Neither
  `DIGEST-MD5` nor `EXTERNAL` is offered.
- **The exchange** (6.4): `<auth mechanism='…'>` with an optional base64 initial response (`=` for
  an empty one, 6.4.2), then `<challenge/>` and `<response/>` as the mechanism needs, ending in
  `<success/>` carrying the SCRAM server-final message as base64 (6.3.10), or empty for PLAIN and
  ANONYMOUS. After `<success/>` the client restarts the stream (6.4.6) and decision 2's
  post-SASL features follow.
- **Accounts.** The authcid is an account of `--user`, `--user-file` or the layer-2 options,
  through the policy (ADR-0100 decision 4); no private roster of credentials exists. The bare JID
  is:
  - for an account name without `@`, `<name>@<stream domain>`;
  - for an account name `<local>@<domain>` (a `--mailbox` account), that JID, only when `<domain>`
    is the stream's domain; on any other domain the login is `not-authorized`;
  - for `ANONYMOUS`, `anon-` and 16 random lower-case hex digits `@<stream domain>`, new per login.
  A name that is not a valid RFC 7622 localpart (empty, holding `"&'/:<>@` or whitespace, or over
  1023 bytes) cannot log in over XMPP: `not-authorized`. The localpart keeps the account's case;
  JIDs are compared by RFC 7622's case-mapping of the localpart, so `Alice` and `alice` are one JID
  for routing.
- **authzid.** Empty, or equal to the bare JID above, is served; any other is
  `<invalid-authzid/>` (6.5.6): no account acts for another.
- **`<failure/>` conditions** (6.5), each in `urn:ietf:params:xml:ns:xmpp-sasl`, with no `<text/>`:

  | Cause | Condition |
  | --- | --- |
  | wrong password, unknown account, refused login, an account name unusable as a JID | `not-authorized`, after the policy's refusal delay (ADR-0032 section 8); never names which |
  | a mechanism not offered on this connection | `invalid-mechanism` |
  | before TLS while it is required | `encryption-required` |
  | base64 that does not decode | `incorrect-encoding` |
  | `<auth/>` without `mechanism`, or a mechanism message that does not parse | `malformed-request` |
  | an authzid other than the account's own | `invalid-authzid` |
  | the client's `<abort/>` | `aborted` |

- **Attempts.** Three failures on one stream (any condition but `aborted`) end it with the stream
  error `policy-violation` (6.4.5: the server SHOULD allow a configurable number of retries, at
  least two, then closes the stream). `<auth/>` after a success is `policy-violation` too.

### 4. Resource binding (RFC 6120 7)

- **The request** (7.6, 7.7): an `<iq type='set'>` holding `<bind
  xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>`, empty or with one `<resource/>`.
- **Empty `<bind/>`**: the server generates `surl-` and 16 random lower-case hex digits (7.6.1).
- **A client's resource** is used as given when it is a valid RFC 7622 resourcepart (1 to 1023
  bytes, no control characters); otherwise the error `bad-request` (modify) (7.7.2.1).
- **Conflict** (7.7.2.2): when another live stream of the same bare JID holds the resource, the
  new stream wins: the older stream gets the stream error `conflict` (4.9.3.3) and is closed, and
  the new one is bound to the resource it asked for. This is the first of the three choices 7.7.2.2
  allows; chosen because a client that reconnects after losing its connection otherwise waits out
  the old stream's idle timeout.
- **Limits.** At most 10 bound resources per bare JID; an eleventh bind is `resource-constraint`
  (wait) (7.6.2.1). A second bind on one stream is `not-allowed` (cancel) (7.6.2.2, the
  client is not allowed to bind another resource; one stream holds one). A bind before SASL success is the stream
  error `not-authorized`.
- **The answer** is `<iq type='result' id='…'><bind
  xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>local@domain/resource</jid></bind></iq>` (7.6.1).
- **Legacy session** (RFC 3921's `urn:ietf:params:xml:ns:xmpp-session`): not advertised, and an
  IQ set for it is answered with an empty `result` (RFC 6121 appendix E lets a server keep doing
  so), because older clients still send it.

### 5. Stanzas (RFC 6120 8 and 10; RFC 6121 5 and 8.5; XEP-0030; XEP-0199)

- **Before binding**, any stanza is the stream error `not-authorized` (4.9.3.12). A first-level
  element that is not `message`, `presence` or `iq` in `jabber:client` is `unsupported-stanza-type`
  (4.9.3.24).
- **`from` is stamped** (8.1.2.1): every stanza the server routes carries `from` = the sender's
  full JID. A client `from` that is neither its full JID nor its bare JID is the stream error
  `invalid-from` (4.9.3.9).
- **`to`.** A `to` that is not a valid JID (RFC 7622) is the stanza error `jid-malformed`
  (modify). A `to` whose domain is not one this stream may reach (any domain served) is
  `remote-server-not-found` (cancel) (10.4.3): there is no federation. No `to` means the sender's
  own bare JID (10.3.3).
- **Available resources.** surl serves no rosters, so no presence is broadcast and none needs to
  be sent first: a bound stream counts as available at priority 0 until it sends presence. Its
  undirected `<presence/>` sets its priority (RFC 6121 4.7.2.3, -128 to 127; out of range is
  `bad-request`); `<presence type='unavailable'/>` makes it unavailable until it sends presence
  again. Presence with a `to` (directed or subscription presence) is accepted and dropped: with no
  rosters there is no subscription to manage (RFC 6121 3.1.3 lets a server ignore a subscription
  request it cannot route to a contact).
- **Messages** (RFC 6121 8.5):
  - to a full JID whose resource is bound: delivered to that stream (8.5.3.1);
  - to a bare JID, or a full JID whose resource is not bound: `normal` and `chat` (and an unknown
    `type`, treated as `normal`, RFC 6121 5.2.2) go to every available stream of that bare JID with
    non-negative priority (8.5.2.1.1). With none, there is no offline storage: the sender gets
    `service-unavailable` (cancel) (8.5.2.2.1). `headline` with none is dropped; `groupchat` to a
    user is `service-unavailable`; `error` is never answered (8.5.2.2);
  - the same `service-unavailable` whether the account exists or merely has no stream, so routing
    never reveals which accounts exist (ADR-0032 section 5);
  - to a domain served itself: `normal` and `chat` are `service-unavailable` (the server handles no
    messages, 10.3.1), the rest dropped.
  - Delivery writes the stanza as received, with `from` stamped and the default namespace
    `jabber:client`; nothing is added.
- **IQ** (8.2.3): `get` and `set` must hold exactly one child element, else `bad-request`
  (modify). An IQ with no `id`, or with a `type` other than `get`, `set`, `result` and `error`, is
  the stream error `invalid-xml` (4.9.3.11): RFC 6120 8.1.3 and 8.2.3 make both schema violations,
  and a stanza error cannot be matched to a request that has no `id`. Each `get` and `set` gets
  exactly one `result` or `error`:

  | Addressed to | Namespace | Answer |
  | --- | --- | --- |
  | a domain served | `http://jabber.org/protocol/disco#info` (get, no `node`) | `<identity category='server' type='im'/>`; features `http://jabber.org/protocol/disco#info`, `http://jabber.org/protocol/disco#items`, `urn:xmpp:ping` (XEP-0030 3.1). No `name` attribute: no product leaks (ADR-0006 section 3) |
  | a domain served | `http://jabber.org/protocol/disco#items` (get, no `node`) | an empty `query` (XEP-0030 4.1): there are no components |
  | a domain served | `urn:xmpp:ping` (get) | an empty `result` (XEP-0199 4.2) |
  | a domain served | `urn:ietf:params:xml:ns:xmpp-session` (set) | an empty `result` (decision 4) |
  | a bare JID with a bound stream, or the sender's own bare JID | `jabber:iq:roster` (get, only to one's own) | an empty `<query xmlns='jabber:iq:roster'/>` (RFC 6121 2.1.3): clients ask for it at login, and there are no rosters |
  | a bare JID with a bound stream | `http://jabber.org/protocol/disco#info` (get) | `<identity category='account' type='registered'/>`; features disco#info and disco#items (XEP-0030 3.1) |
  | a bare JID with a bound stream | `http://jabber.org/protocol/disco#items` (get) | an empty `query` |
  | either, with a `node` attribute | disco#info or disco#items | `item-not-found` (cancel) (XEP-0030 3.2) |
  | a bare JID with no bound stream | any | `service-unavailable` (cancel) (RFC 6121 8.5.1, 8.5.2.2.3) |
  | a full JID that is bound | any | delivered to that stream; its `result` or `error` routed back to the sender (RFC 6121 8.5.3.1) |
  | a full JID not bound | any | `service-unavailable` (cancel) (RFC 6121 8.5.3.2.2) |
  | anything above, any other namespace or type (`jabber:iq:version`, roster `set`, `jabber:iq:private`, ...) | | `service-unavailable` (cancel) (8.4) |

  An IQ `result` or `error` addressed to the server, or to a JID with no bound stream, is dropped
  (8.2.3: never answered).
- **Stanza errors** (8.3) are the stanza returned with `type='error'`, `to` and `from` swapped,
  the original child kept for an IQ, and `<error type='…'><condition
  xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error>`, with no `<text/>`. A stanza of type
  `error` never gets one (8.3.1).

### 6. Stream errors and XML restrictions (RFC 6120 4.9, 11)

Each stream error is `<stream:error><condition
xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error>`, with no `<text/>`, then
`</stream:stream>`, then close. When the error comes before the client's stream header is
complete, the server first sends its own response header (4.9.1.1).

| Cause | Stream error |
| --- | --- |
| bytes that are not well-formed XML, or a first element that is not a stream header | `not-well-formed` (4.9.3.13) |
| a DTD, a comment, a processing instruction (an XML declaration at a stream header's start is allowed), or an entity reference other than the five predefined and character references (11.1) | `restricted-xml` (4.9.3.18) |
| an XML declaration naming an encoding other than UTF-8, or bytes that are not UTF-8 (11.6) | `unsupported-encoding` (4.9.3.22) |
| a stream namespace or content namespace other than decision 1's | `invalid-namespace` (4.9.3.10) |
| a `to` naming a domain not served | `host-unknown` (4.9.3.6) |
| a missing `version`, or a major below 1 | `unsupported-version` (4.9.3.25) |
| a stanza before authentication or before binding | `not-authorized` (4.9.3.12) |
| a first-level element in `jabber:client` that is not a stanza | `unsupported-stanza-type` (4.9.3.24) |
| a client `from` not its own | `invalid-from` (4.9.3.9) |
| an IQ with no `id` or an unknown `type` | `invalid-xml` (4.9.3.11) |
| an element the phase does not allow (decision 2), SASL failures past three, a stanza over the size bound, nesting past the depth bound, too slow a stream (decision 7) | `policy-violation` (4.9.3.14) |
| the same resource bound by a newer stream | `conflict` (4.9.3.3), to the older stream |
| no data for `--idle-timeout` | `connection-timeout` (4.9.3.4) |
| `--max-connections`, `--max-connections-per-address` | `resource-constraint` (4.9.3.17), after a response header and before any feature |
| the server stopping | `system-shutdown` (4.9.3.20) |

`internal-server-error` (4.9.3.8) is sent only for an unexpected failure, and nothing about the
exception follows it (ADR-0006 section 3). Never sent: `bad-format`, `bad-namespace-prefix`,
`improper-addressing`, `remote-connection-failed`, `reset`, `see-other-host`,
`undefined-condition`. Whitespace between
first-level elements (4.6.1, whitespace keepalives) is accepted and resets the idle timer.

### 7. Limits (ADR-0006; RFC 6120 13.12)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| a stream header (the first one and each restart) | arrives within `--head-timeout` (30 s) of accept, of `<proceed/>` or of `<success/>`, TLS handshake included | `connection-timeout`, then close |
| login | SASL success and a bound resource within twice `--head-timeout` of accept, so a slow client cannot hold an unauthenticated stream | `policy-violation`, then close |
| a stanza (any first-level element), counted in bytes as read | `--max-message` (1048576; RFC 6120 13.12 asks for at least 10000) | `policy-violation`, then close |
| element nesting inside a stanza | 32 levels | `policy-violation`, then close |
| attributes on one element | 64 | `policy-violation`, then close |
| a single attribute value or text node | `--max-message` | `policy-violation`, then close |
| SASL attempts | 3 failures a stream (decision 3) | `policy-violation`, then close |
| resources a bare JID | 10 (decision 4) | `resource-constraint` stanza error |
| unsent output to a stream (a slow reader) | 16 MiB of stanzas routed to it and not yet taken by the transport | that stream ends with `policy-violation`; the sender is not told (the stanza counts as delivered) |
| `--idle-timeout` (120 s) | no byte from the client, whitespace keepalives included | `connection-timeout`, then close |
| `--max-time` (3600 s) | `Surl.Core`'s | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | decision 6's `resource-constraint` |

No new option is added: ADR-0006's options cover what a test harness tunes.

## Alternatives considered

- **A new `--xmpp-domain` option.** Rejected: the listen URL already names the host the client
  connects to, which is the domain a client puts in `to`, and `--mail-domain` already names the
  domains a deployment owns.
- **A new option for STARTTLS-optional.** Rejected: `--allow-plaintext-auth` already means "let a
  login cross plain text" for every server; a second option for the same intent would let them
  disagree.
- **Offline storage of messages** (RFC 6121 8.5.2.2.1 allows it). Rejected for now: a test server
  that answers `service-unavailable` tells the sender at once, and storing needs a store that
  survives the session; the mail store is for mail.
- **Routing over `Surl.MessageBus`.** Rejected: a JID is an address, not a topic, and ADR-0100
  decision 3 maps no XMPP address; one server instance's session registry routes exactly what
  RFC 6121 8.5 asks.
- **Refusing a conflicting bind** (7.7.2.2's second choice). Rejected: a reconnecting client would
  wait for its old stream's idle timeout.
- **Treating a stream without presence as unavailable** (RFC 6121 strictly). Rejected: with no
  rosters there is nothing presence would buy, and a client that only sends one message (the
  port's does) would never receive a reply.

## Consequences

- `surl xmpp://localhost:0/ --self-signed --user alice:secret` is an XMPP server that requires
  STARTTLS, logs `alice@localhost` in with SCRAM or PLAIN, binds her resource, routes her messages
  to other connected users, and answers disco#info, disco#items and ping; `xmpps` does the same
  behind direct TLS.
- BL-536 to BL-539 build it, BL-540 serves it from `surl`, and the Curl port's client may connect
  to it as a live client in `Integration` tests (ADR-0083).
- The requirement matrix [`Xmpp.md`](../../Conformance/Extended/Xmpp.md) traces each server-side
  requirement to BL-536, BL-537, BL-538 or BL-539; each task replaces its ID with the named test.
