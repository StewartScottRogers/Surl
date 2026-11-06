# Surl.Extended.Protocol.Xmpp.UnitLibrary

An extended server (ADR-0083) for the `xmpp` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-540, so `surl` does not serve it yet.
ADR-0114 decides how. It is judged by RFC 6120 and the specifications ADR-0114 lists, never
by the Curl port's output, and its unit tests replay streams written by hand from RFC 6120's
sections and examples. `Documentation/Conformance/Extended/Xmpp.md` traces each requirement to
its task.

It references `Surl.Protocol.Abstractions` only. SASL and resource binding (one stream's) came
with BL-538; stanza routing, IQs and service discovery with BL-539; the resource
conflict, the per-JID resource cap, `invalid-authzid` and the login deadline with BL-885; BL-540
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
  is the full JID; then `XmppStanzaRouter` takes the stream's stanzas. `<starttls/>`, `<auth/>` or
  another non-stanza after login ends the stream (`policy-violation`, or `unsupported-stanza-type`
  in `jabber:client`).

Stanzas (BL-539, ADR-0114 decision 5):

- `XmppServer` holds one `XmppSessionRegistry` per instance (never a static); a stream is added as
  an `XmppBoundSession` once bound and removed when its connection ends. `XmppServerConnection`
  chains its writes, so its own answers and stanzas other streams route to it never interleave;
  a routed write that fails is noted in the sender's log and nothing else.
- `XmppStanzaRouter`: an IQ with no `id` or an unknown `type` is `invalid-xml`; a `from` other
  than the stream's full or bare JID (`XmppJid.HasBare`, the localpart without case) is the stream
  error `invalid-from`; every routed stanza is written as received with `from` stamped
  (`XmppStanzaText.Routed`). No `to` is the sender's own bare JID; a `to` that `XmppJid.Parse`
  refuses is `jid-malformed`, one on a domain not served `remote-server-not-found`.
- Messages: to a bound full JID, delivered; to a bare JID or an unbound full one, `normal`, `chat`
  and unknown types go to every available stream with priority 0 or more, else
  `service-unavailable` (the same for an account that does not exist), `headline` dropped,
  `groupchat` `service-unavailable`, `error` never answered; to a served domain, `normal` and `chat`
  are `service-unavailable`, the rest dropped. Presence: undirected sets the priority (`sbyte`, else
  `bad-request`), `unavailable` takes the stream out of bare-JID delivery, anything directed or
  other is dropped.
- IQs: `get` and `set` need exactly one child (`bad-request`). To a bound full JID, delivered, and
  replies routed back; otherwise the server answers: a second bind `not-allowed`, the legacy session
  an empty result, a served domain's disco#info (`server`/`im`, features disco#info, disco#items,
  ping), disco#items (empty) and ping, an account with a bound stream's disco#info
  (`account`/`registered`) and disco#items, one's own roster (empty); a disco `node` is
  `item-not-found`; anything else `service-unavailable`. Results use XEP-0030 example 2's shape
  (`XmppStanzaText.Result`); stanza errors swap `to` and `from`, keep an IQ's children and drop a
  message's or presence's (`XmppStanzaText.Error`). An IQ `result` or `error` to anything but a
  bound full JID is dropped. The 16 MiB unsent-output limit comes with BL-887.
