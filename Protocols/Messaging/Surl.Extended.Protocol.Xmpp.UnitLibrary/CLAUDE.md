# Surl.Extended.Protocol.Xmpp.UnitLibrary

An extended server (ADR-0083) for the `xmpp` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-540, so `surl` does not serve it yet.
ADR-0114 decides how. It is judged by RFC 6120 and the specifications ADR-0114 lists, never
by the Curl port's output, and its unit tests replay streams written by hand from RFC 6120's
sections and examples. `Documentation/Conformance/Extended/Xmpp.md` traces each requirement to
its task.

It references `Surl.Protocol.Abstractions` only. STARTTLS and `xmpps` come with BL-537, SASL
and resource binding with BL-538, and stanza routing, IQs and service discovery with BL-539.

Streams, the XML stream reader and stream errors (BL-536, ADR-0114 decisions 1, 6 and 7):

- `XmppServer` is the `IConnectionProtocolServer` for `xmpp` and its refusal writer (a response
  header, then `resource-constraint`); its remarks list every answer.
- `XmppServerConnection` answers one connection: the stream header within the head timeout,
  checked for namespaces, `to` and version; the response header (`from`, a 32-hex-digit `id`
  from the injected `IXmppRandomSource`, `version='1.0'`, `xml:lang`) and `<stream:features/>`;
  then one first-level element, which ends the stream until BL-537 to BL-539 serve one
  (a stanza is `not-authorized`, another `jabber:client` element `unsupported-stanza-type`,
  anything else `policy-violation`). `</stream:stream>` is answered with the server's. Every
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
