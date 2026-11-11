# XMPP requirement matrix

The server-side requirements `Surl.Extended.Protocol.Xmpp` answers, for the `xmpp` and `xmpps`
schemes (ADR-0083 decision 9). Written from RFC 6120 (XMPP Core), RFC 6121 (XMPP IM), RFC 7622
(address format), XEP-0030 (service discovery), XEP-0199 (ping), XEP-0368 (direct TLS), and the
SASL mechanisms' RFC 4616, RFC 4505, RFC 5802 and RFC 7677, the specifications
[ADR-0114](../../Planning/Decisions/ADR-0114-how-the-xmpp-server-answers-xmpp-and-xmpps.md) decides
by, never from the Curl port's client matrix. Each row binds the server (the receiving entity);
"Covered by" names the task that proves it, until that task replaces its ID with the named test, or
says why the row needs no test.

IDs are `Xmpp:<source>-<section>-<n>` for a specification's requirement, and
`Xmpp:Surl-<Decision>-<n>` for a rule ADR-0114 adds where the specifications leave the server a
choice.

## Streams and XML (RFC 6120 sections 4 and 11)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-4.1-1 | MUST | The stream is read as XML however its bytes are split across reads, one first-level element at a time. | BL-536 |
| Xmpp:RFC6120-4.2-1 | MUST | The server answers the client's stream header with its own response header. | BL-536 |
| Xmpp:RFC6120-4.7.1-1 | MUST | The response header's `from` is the stream's domain. | BL-536 |
| Xmpp:RFC6120-4.7.3-1 | MUST | The response header carries a new, unpredictable `id` for every stream, each restart included. | BL-536 |
| Xmpp:RFC6120-4.7.4-1 | SHOULD | The response header's `xml:lang` is the client's, else `en`. | BL-536 |
| Xmpp:RFC6120-4.7.5-1 | MUST | The response `version` is `1.0`, the lower of the two; a missing version or major below 1 is `unsupported-version`. | BL-536 |
| Xmpp:RFC6120-4.8-1 | MUST | A stream namespace other than `http://etherx.jabber.org/streams` or a content namespace other than `jabber:client` is `invalid-namespace`. | BL-536 |
| Xmpp:RFC6120-4.4-1 | MUST | `</stream:stream>` from the client is answered `</stream:stream>`, then the connection is closed. | BL-536 |
| Xmpp:RFC6120-4.6.1-1 | MUST | Whitespace between first-level elements is accepted as a keepalive. | BL-536 |
| Xmpp:RFC6120-4.9.1.1-1 | MUST | A stream error before the client's header is complete is preceded by the server's response header. | BL-536 |
| Xmpp:RFC6120-4.9.1.2-1 | MUST | After a stream error the server sends `</stream:stream>` and closes. | BL-536 |
| Xmpp:RFC6120-4.9.3.13-1 | MUST | Bytes that are not well-formed XML, or a first element that is not a stream, are `not-well-formed`. | BL-536 |
| Xmpp:RFC6120-11.1-1 | MUST | A DTD, comment, processing instruction (other than a header's XML declaration) or non-predefined entity reference is `restricted-xml`. | BL-536 |
| Xmpp:RFC6120-11.6-1 | MUST | An encoding other than UTF-8 is `unsupported-encoding`. | BL-536 |
| Xmpp:RFC6120-4.9.3.24-1 | MUST | A first-level element in `jabber:client` that is not a stanza is `unsupported-stanza-type`. | BL-536 |
| Xmpp:Surl-Errors-1 | MUST NOT | No stream or stanza error carries `<text/>`, an account name, a path, an exception or a version. | BL-536 |

## The domain (RFC 6120 4.7.2, RFC 7622)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-4.9.3.6-1 | MUST | A `to` naming a domain not served is `host-unknown`. | BL-536 |
| Xmpp:Surl-Domain-1 | MUST | The listen URL's host is served; on a wildcard listen address, the connection's local address; on a loopback listen, both `localhost` and the loopback literal. | BL-536 |
| Xmpp:Surl-Domain-2 | MUST | Every `--mail-domain` is served too. | BL-536 |
| Xmpp:Surl-Domain-3 | MUST | A header with no `to` opens a stream for the first domain served. | BL-536 |

## STARTTLS and direct TLS (RFC 6120 section 5; XEP-0368)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-5.3.1-1 | MUST | On `xmpp` with a certificate, the first features are `<starttls><required/></starttls>` alone. | `ServeAsync_CertificateConfigured_OffersRequiredStartTlsAlone` |
| Xmpp:Surl-Tls-1 | MUST | Under `--allow-plaintext-auth`, STARTTLS is offered without `<required/>`, beside the plain-text mechanisms. | `ServeAsync_AllowPlaintextAuthentication_OffersVoluntaryStartTlsBesideThePlainTextMechanisms` |
| Xmpp:RFC6120-6.5.4-1 | MUST | `<auth/>` before TLS when TLS is required is `<failure><encryption-required/></failure>`, the stream kept. | `ServeAsync_SaslAuthBeforeTlsWhileRequired_IsEncryptionRequiredAndTheStreamKept` |
| Xmpp:RFC6120-5.3.1-2 | MUST | A stanza before TLS is `not-authorized`; any other element is `policy-violation`. | `ServeAsync_OtherElementBeforeTls_EndsTheStreamAsSection5Says` |
| Xmpp:RFC6120-5.4.2.3-1 | MUST | `<starttls/>` is answered `<proceed/>`, then the TLS handshake through the listener seam. | `ServeAsync_StartTls_IsAnsweredProceedThenTheUpgrade` |
| Xmpp:RFC6120-5.4.2.2-1 | MUST | When the upgrade is unavailable, or `<starttls/>` is repeated, the answer is the TLS `<failure/>`, then `</stream:stream>` and close. | `ServeAsync_StartTlsWithoutACertificate_IsTheTlsFailureAndClose`, `ServeAsync_StartTlsRepeatedAfterTls_IsTheTlsFailureAndClose` |
| Xmpp:RFC6120-5.4.3.2-1 | MUST | A failed handshake closes the connection with no bytes. | `ServeAsync_HandshakeFails_ClosesWithNoMoreBytes` |
| Xmpp:RFC6120-5.4.3.3-1 | MUST | After TLS the client's new stream header is answered with a new `id`, and STARTTLS is not offered again. | `ServeAsync_StreamRestartedAfterTls_HasANewIdAndOffersSaslAndNotStartTls` |
| Xmpp:XEP0368-1 | MUST | On `xmpps` the TLS handshake runs from the first byte, before any XML, and STARTTLS is never offered. | BL-540 (composition wraps `xmpps` in `ImplicitTlsSchemeServer`); server half: `ServeAsync_ConnectionTlsFromItsFirstByte_OffersSaslAndRefusesStartTls` |
| Xmpp:Surl-Tls-2 | MUST | `xmpps` without a certificate, and `xmpp` without one and without `--allow-plaintext-auth` or `--allow-anonymous`, fail at start with `CertificateProblem` (58). | BL-540 (composition checks certificates at start) |

## SASL (RFC 6120 section 6; RFC 4616, RFC 4505, RFC 5802, RFC 7677)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-6.4.1-1 | MUST | `<mechanisms/>` lists `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN`, then `ANONYMOUS` only under `--allow-anonymous`, as the policy offers them for the TLS state. | BL-538 |
| Xmpp:RFC6120-6.4.2-1 | MUST | An initial response of `=` is an empty one. | BL-538 |
| Xmpp:RFC6120-6.4.3-1 | MUST | Challenges and responses carry base64; SCRAM runs to `<success/>` holding the server-final message. | BL-538 |
| Xmpp:RFC6120-6.4.6-1 | MUST | After `<success/>` the client's new stream header is answered with the bind feature. | BL-538 |
| Xmpp:RFC6120-6.5-1 | MUST | Refused logins are `not-authorized` after the refusal delay; an unoffered mechanism `invalid-mechanism`; bad base64 `incorrect-encoding`; an `<auth/>` with no mechanism `malformed-request` (a mechanism message that does not parse is refused by the policy, so `not-authorized`); `<abort/>` `aborted`. | BL-538 |
| Xmpp:RFC6120-6.5.6-1 | MUST | An authzid other than empty or the account's own bare JID is `invalid-authzid`. | BL-885 |
| Xmpp:RFC6120-6.4.5-1 | SHOULD | Three failed attempts on one stream end it with `policy-violation`. | BL-538 |
| Xmpp:RFC4616-2-1 | MUST | PLAIN's message is authzid NUL authcid NUL password, judged through `ISaslAuthenticationPolicy`. | BL-538 |
| Xmpp:RFC5802-5-1 | MUST | SCRAM-SHA-1 verifies the client proof and returns the server signature; gs2 header `p` is refused. | BL-538 |
| Xmpp:RFC7677-3-1 | MUST | SCRAM-SHA-256 is SCRAM over SHA-256. | BL-538 |
| Xmpp:RFC4505-2-1 | MUST | ANONYMOUS, under `--allow-anonymous` only, logs in as a new `anon-<16 hex>` localpart. | BL-538 |
| Xmpp:Surl-Sasl-1 | MUST | An account `name` logs in as `name@<stream domain>`; an account `local@domain` only on that domain; a name invalid as a localpart is `not-authorized`. | BL-538 |

## Resource binding (RFC 6120 section 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-7.6.1-1 | MUST | An empty `<bind/>` binds a generated `surl-<16 hex>` resource and answers the full JID. | BL-538 |
| Xmpp:RFC6120-7.7.1-1 | MUST | A client's valid resource is bound as given; an invalid one is `bad-request`. | BL-538 |
| Xmpp:RFC6120-7.7.2.2-1 | MUST | A resource already bound for the same bare JID is taken by the new stream; the older gets the stream error `conflict`. | BL-885 |
| Xmpp:RFC6120-7.6.2.1-1 | MUST | An eleventh resource for one bare JID is `resource-constraint`. | BL-885 |
| Xmpp:RFC6120-7.6.2.2-1 | MUST | A second bind on one stream is `not-allowed`. | BL-538 |
| Xmpp:RFC6120-4.9.3.12-1 | MUST | A stanza or bind before SASL success, or a stanza before binding, is `not-authorized`. | BL-538 |
| Xmpp:RFC6121-E-1 | MAY | A legacy session IQ set is answered with an empty `result`; the session feature is not advertised. | BL-538 |

## Stanzas (RFC 6120 sections 8 and 10; RFC 6121 sections 4, 5 and 8.5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-8.1.2.1-1 | MUST | Every routed stanza carries `from` = the sender's full JID. | BL-539 |
| Xmpp:RFC6120-4.9.3.9-1 | MUST | A client `from` other than its full or bare JID is `invalid-from`. | BL-539 |
| Xmpp:RFC6120-8.3.3.8-1 | MUST | A `to` that is not a valid JID is `jid-malformed`. | BL-539 |
| Xmpp:RFC6120-10.4.3-1 | MUST | A `to` on a domain not served is `remote-server-not-found`. | BL-539 |
| Xmpp:RFC6120-10.3.3-1 | MUST | A stanza with no `to` is handled as addressed to the sender's bare JID. | BL-539 |
| Xmpp:RFC6121-8.5.3.1-1 | MUST | A message to a bound full JID is delivered to that stream. | BL-539 |
| Xmpp:RFC6121-8.5.2.1.1-1 | MUST | A `normal` or `chat` message to a bare JID goes to each available stream with non-negative priority. | BL-539 |
| Xmpp:RFC6121-8.5.2.2-1 | MUST | With no available stream, `normal` and `chat` are `service-unavailable`, `headline` and `error` are dropped, `groupchat` is `service-unavailable`. | BL-539 |
| Xmpp:RFC6121-5.2.2-1 | MUST | A message of unknown `type` is treated as `normal`. | BL-539 |
| Xmpp:Surl-Stanza-1 | MUST | A bound stream is available at priority 0 until it sends presence; undirected presence sets its priority, `unavailable` removes it; directed presence is dropped. | BL-539 |
| Xmpp:RFC6121-4.7.2.3-1 | MUST | A presence priority outside -128 to 127 is `bad-request`. | BL-539 |
| Xmpp:RFC6120-8.2.3-1 | MUST | An IQ `get` or `set` without exactly one child is `bad-request`; each gets exactly one `result` or `error`. | BL-539 |
| Xmpp:RFC6120-8.1.3-1 | MUST | An IQ with no `id` or an unknown `type` is the stream error `invalid-xml`. | BL-539 |
| Xmpp:RFC6120-8.3.1-1 | MUST | A stanza error swaps `to` and `from`, keeps an IQ's child, carries a defined condition and no `<text/>`, and is never sent for a stanza of type `error`. | BL-539 |
| Xmpp:RFC6120-8.4-1 | MUST | An IQ in a namespace the server does not serve is `service-unavailable`. | BL-539 |
| Xmpp:RFC6121-8.5.3.2.2-1 | MUST | An IQ to an unbound full JID, or to a bare JID with no bound stream, is `service-unavailable`. | BL-539 |
| Xmpp:RFC6121-8.5.3.1-2 | MUST | An IQ to a bound full JID is delivered, and its `result` or `error` routed back. | BL-539 |
| Xmpp:RFC6121-2.1.3-1 | MUST | A roster get to one's own bare JID is answered with an empty roster. | BL-539 |
| Xmpp:Surl-Stanza-2 | MUST NOT | Routing answers the same whether an account exists or has no stream. | BL-539 |

## Service discovery and ping (XEP-0030; XEP-0199)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:XEP0030-3.1-1 | MUST | disco#info to the server answers identity `server`/`im` and features disco#info, disco#items and `urn:xmpp:ping`, with no `name`. | BL-539 |
| Xmpp:XEP0030-3.1-2 | MUST | disco#info to a bare JID with a bound stream answers identity `account`/`registered` and features disco#info and disco#items. | BL-539 |
| Xmpp:XEP0030-4.1-1 | MUST | disco#items to the server or a bare JID answers an empty `query`. | BL-539 |
| Xmpp:XEP0030-3.2-1 | MUST | A disco query with a `node` is `item-not-found`. | BL-539 |
| Xmpp:XEP0199-4.2-1 | MUST | A ping to the server is answered with an empty `result`. | BL-539 |
| Xmpp:XEP0199-4.1-1 | MAY | The server pings clients. | n/a: the server never sends a ping; whitespace and `--idle-timeout` bound idle streams (ADR-0114 decision 7) |

## Limits (ADR-0006; RFC 6120 13.12)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-13.12-1 | MUST | A stanza over `--max-message` bytes is `policy-violation`, then close. | BL-536 |
| Xmpp:Surl-Limits-1 | MUST | Nesting past 32 levels, more than 64 attributes on an element, or a value over `--max-message` is `policy-violation`. | BL-536 |
| Xmpp:Surl-Limits-2 | MUST | A stream header not complete within `--head-timeout` is `connection-timeout`. | BL-536 |
| Xmpp:Surl-Limits-3 | MUST | No bound resource within twice `--head-timeout` of accept is `policy-violation`. | BL-885 |
| Xmpp:RFC6120-4.9.3.4-1 | MUST | No byte for `--idle-timeout` is `connection-timeout`. | BL-536 |
| Xmpp:RFC6120-4.9.3.17-1 | MUST | A connection over `--max-connections` or `--max-connections-per-address` gets a response header and `resource-constraint`. | BL-536 |
| Xmpp:Surl-Limits-4 | MUST | A stream with 16 MiB of routed stanzas unsent ends with `policy-violation`. | BL-887 |
| Xmpp:RFC6120-4.9.3.20-1 | MUST | A server stopping ends each stream with `system-shutdown`. | BL-536 |
