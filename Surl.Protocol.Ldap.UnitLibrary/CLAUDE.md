# Surl.Protocol.Ldap.UnitLibrary

Phase 5.

The LDAP server (RFC 4511): answers the search upstream curl encodes in an `ldap://` URL
- base DN, attributes, scope and filter - from a directory it serves.

**URL schemes answered:** `ldap`, `ldaps`

What is here so far is the BER codec (BL-289), internal and with no transport of its own:
`LdapMessageFrameReader` reads one whole `LDAPMessage` off an `IConnection` under the
message limit, `LdapMessageDecoder` (with `LdapFilterDecoder` and `LdapBerFieldReader`)
decodes it into an `LdapMessage` or an `LdapDecodeOutcome` carrying the message ID, and
`LdapMessageEncoder` writes the responses. All of it uses the BCL's `System.Formats.Asn1`.

Beside it is the directory (BL-306, ADR-0072 decision 1), internal too: `LdapDirectory`
holds `LdapEntry`s in their source's order, enforces the bounds (refusing a list it cannot
hold with an `LdapDirectoryException` naming the `LdapDirectoryFault` and the entry), and
answers an `LdapSearchRequest` with an `LdapSearchOutcome` - base, one-level and subtree
scopes, the root DSE for the empty base (`LdapRootDseFacts` carry the per-connection
part), `noSuchObject` with the nearest superior, the size and time limits.
`LdapDistinguishedName` parses RFC 4514 DNs (`LdapDistinguishedNameParser`) and compares
them in normal form; `LdapMatchingRules` picks and applies each type's RFC 4517 rule;
`LdapFilterEvaluator` evaluates filters three-valued; `LdapAttributeSelection` picks the
attributes returned.

On top of both is the server (BL-308, ADR-0072 decisions 2, 3, 6 and 7): the public
`LdapProtocolServer` answers `ldap` - its public constructor over an empty directory, an
internal one over an `LdapDirectory` until BL-307 and BL-310 load one - and runs one
`LdapSession` per connection. `LdapBindJudge` decides each bind (simple binds through
`IAuthenticationPolicy.CheckPasswordLoginAsync`, the name mapped by `LdapBindNames`; Sicily
and SASL binds `authMethodNotSupported` until BL-309); the session answers searches,
compares, writes (refused), extended operations, abandon and unbind, sends the Notice of
Disconnection for what it cannot read (`LdapDiagnostics`) and for a limit, and notes each
decision (`LdapLogText`). Its tests replay request bytes recorded from the pinned Windows
build (`Surl.Protocol.Ldap.UnitTests/Fixtures/README.md`).

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
