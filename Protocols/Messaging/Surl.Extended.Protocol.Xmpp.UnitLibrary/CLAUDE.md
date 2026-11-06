# Surl.Extended.Protocol.Xmpp.UnitLibrary

An extended server (ADR-0083) for the `xmpp` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-540, so `surl` does not serve it yet.
ADR-0114 decides how. It is judged by RFC 6120 and the specifications ADR-0114 lists, never
by the Curl port's output, and its unit tests replay streams written by hand from RFC 6120's
sections and examples. `Documentation/Conformance/Extended/Xmpp.md` traces each requirement to
its task.

It references `Surl.Protocol.Abstractions` only. SASL and resource binding (one stream's) came
with BL-538; stanza routing, IQs and service discovery come with BL-539, and the resource
conflict, the per-JID resource cap, `invalid-authzid` and the login deadline with BL-875; BL-540
serves `xmpps` by wrapping the server in `ImplicitTlsSchemeServer`.

Streams, the XML stream reader and stream errors (BL-536, ADR-0114 decisions 1, 6 and 7):

- `XmppServer` is the `IConnectionProtocolServer` for `xmpp` and its refusal writer (a response
  header, then `resource-constraint`); its remarks list every answer.
- `XmppServerConnection` answers one connection: the stream header within the head timeout,
  checked for namespaces, `to` and version; the response header (`from`, a 32-hex-digit `id`
  from the injected `IXmppRandomSource`, `version='1.0'`, `xml:lang`) and the phase's features
  (below); then first-level elements: STARTTLS's and SASL's are served (below), and before login
  a stanza is `not-authorized`, another `jabber:client` element `unsupported-stanza-type`, and
  anything else `policy-violation`. `</stream:stream>` is answered with the server's. Every
  stream error is preceded by a response header when none was sent, carries no text, and is
  followed by `</stream:stream>`. A limit cancelling the exchange is `connection-timeout`,
  shutdown `system-shutdown`, each written within a one-second deadline.
- A major version other than 1 (`2.0` included) is `unsupported-version`; a higher minor
  version opens a `1.0` stream. RFC 6120 4.7.5 and 4.9.3.25 (whose example is `11.0`) say so;
  ADR-0114 decision 1's "a higher major or minor" is read as the minor only (BL-536 Notes).
- `XmppStreamReader` reads the stream with the BCL's `XmlReader` (DTDs prohibited, no resolver,
  fragment conformance, comments and processing instructions reported) over `XmppInputStream`,
  one first-level element at a time as an `XElement`, however its bytes are split.
  `XmppParseFailures` names a parse failure from the most recent bytes: a DTD or undeclared
  entity is `restricted-xml`, another encoding `unsupported-encoding`, the rest
  `not-well-formed`. Limits: `--max-message` per header, element and value (counted to within
  one 4096-byte read, never over), 32 levels of nesting, 64 attributes an element.
- `XmppDomains` lists the domains served: the listen host, the connection's local address on a
  wildcard listen, `localhost` and the loopback literal on a loopback listen, then each mail domain.

STARTTLS (BL-537, ADR-0114 decision 2):

- `XmppServerOptions.IsTlsUpgradeAvailable` (a certificate is configured) and
  `AllowPlaintextAuthentication` (`--allow-plaintext-auth`) choose the features: before TLS with
  a certificate, `<starttls><required/></starttls>` alone, or under the option a voluntary
  `<starttls/>` beside the plain-text mechanisms; without a certificate, or after TLS, the
  `<mechanisms/>` `AuthenticationPolicy` offers for the TLS state, ordered and filtered to
  SCRAM-SHA-256, SCRAM-SHA-1, PLAIN, ANONYMOUS. STARTTLS is never offered once TLS is in force.
- `<starttls/>` is answered `<proceed/>`, then `UpgradeToTlsAsync`; a new `XmppStreamReader`
  reads the restarted stream, so bytes pipelined after `<starttls/>` are dropped. A failed or
  unfinished handshake closes with no more bytes. The head timeout runs from `<proceed/>` to the
  new header, handshake included. `<starttls/>` without a certificate or after TLS is the TLS
  `<failure/>`, `</stream:stream>` and the close.
- While TLS is required, `<auth/>` is `<failure><encryption-required/></failure>` and the stream
  is kept; a stanza is `not-authorized`, anything else `policy-violation`.

SASL and resource binding (BL-538, ADR-0114 decisions 3 and 4):

- `XmppSaslLogin` runs one stream's SASL: `<auth/>` must name a mechanism the features offered
  (`malformed-request` without one, `invalid-mechanism` otherwise), its text is base64, `=` for an
  empty initial response and no text for none (`incorrect-encoding` when it does not decode). The
  policy's `ISaslExchange` runs the mechanism, started with `CanCarrySecurityLayer: true` so SCRAM's
  server-final message comes back as additional success data and goes in `<success/>` (RFC 6120
  6.3.10); an empty challenge or success is an empty element. `RefusedCredentials` is
  `not-authorized`, `RefusedPlaintext` `encryption-required`, `RefusedMechanism`
  `invalid-mechanism`. `<abort/>` is `aborted` and not counted; the third other failure on a stream
  (`encryption-required` before TLS included) is answered, then ends it with `policy-violation`.
  `<response/>` or `<abort/>` with no exchange under way is `policy-violation`.
- `XmppJids` makes the bare JID: `name@<stream domain>`, a `local@domain` account only on the
  stream's domain, `anon-` and 16 random hex digits for a login accepted unchecked; a name that is
  not an RFC 7622 localpart is `not-authorized`. After `<success/>` a new `XmppStreamReader` reads
  the restarted stream, whose features are `<bind/>` alone.
- `XmppResourceBinding` answers the logged-in stream's stanzas: an IQ with no `id` or an unknown
  `type` is `invalid-xml`; before bind, only an IQ `set` whose one child is `<bind/>` is served,
  anything else `not-authorized`. A `<resource/>` that is not a resourcepart (empty, over 1023
  bytes, a control character) is `bad-request`; none gives `surl-` and 16 hex digits; the answer
  is the full JID. After bind a second bind is `not-allowed`, the legacy session request an empty
  result, any other IQ `get` or `set` `service-unavailable`, and an IQ reply, message or presence is
  dropped until BL-539 routes them. `<starttls/>`, `<auth/>` or another non-stanza after login ends
  the stream (`policy-violation`, or `unsupported-stanza-type` in `jabber:client`).
