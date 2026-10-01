# Surl.Protocol.Ldap.UnitLibrary

Phase 5, built. How the server answers is decided in ADR-0072 (as ADR-0076 amends decision 4's
`GSSAPI` line); what it answers, and what pinned upstream curl has proven against it, is in
`Documentation/Product/Product-Overview.md`, "Built for Phase 5: LDAP, SMB and RTSP", and its
terms are in `Documentation/Wiki/Glossary.md`, section "LDAP, SMB and RTSP".

The LDAP server (RFC 4511): answers the search upstream curl encodes in an `ldap://` URL
- base DN, attributes, scope and filter - from a read-only directory it serves.

**URL schemes answered:** `ldap`, `ldaps`

The BER codec (BL-289, ADR-0072 decision 9), internal and with no transport of its own:
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

On top of both is the server (BL-308 and BL-309, ADR-0072 decisions 2 to 7): the public
`LdapProtocolServer` answers `ldap` - its public constructor over an empty directory (in-memory
mode), its public `LoadAsync` over the directory read from an `LdapDirectoryFile`, each taking
the `IAuthenticationPolicy`, the `ISaslAuthenticationPolicy` and `isTlsUpgradeAvailable` - and
runs one `LdapSession` per connection. `LdapBindJudge` decides each bind into an
`LdapBindAnswer`: simple binds through `IAuthenticationPolicy.CheckPasswordLoginAsync`, the
name mapped by `LdapBindNames`; SASL binds and `WinLDAP`'s Sicily binds
(`LdapSicilyAuthentication`, `LdapSicilyChoice`) through `LdapSaslBindJudge`, which runs the
policy's `ISaslExchange` and holds the exchange in progress between binds. The session answers
searches (the root DSE with the policy's `supportedSASLMechanisms` and, with a certificate,
`StartTLS`), compares, writes (refused), `StartTLS` (the upgrade, after discarding what
`LdapMessageFrameReader` read past the request), other extended operations, abandon and unbind;
once a bind installs an `ISaslSecurityLayer` every message both ways is one 4-byte-length buffer
the layer protects. It sends the Notice of Disconnection for what it cannot read
(`LdapDiagnostics`) and for a limit, and notes each decision (`LdapLogText`). `ldaps` is the
same exchange inside the engine's implicit TLS: `Surl.Console` registers the server a second
time behind `ImplicitTlsSchemeServer` (BL-310). Its tests replay request bytes recorded from the
pinned Windows build (`Surl.Protocol.Ldap.UnitTests/Fixtures/README.md`), the SASL policy and
security layer faked.

The directory's file (BL-307, ADR-0072 decision 1): the public `LdapDirectoryFile` names
`directory.ldif` in the state folder it is given (`<path>/.surl/ldap`) and reads it once
through `Surl.Content`'s `IContentFileSystem`; a missing file is an empty directory, and the
directory is never written. `LdifReader` reads RFC 2849 LDIF content records into
`LdifRecord`s; a file it or `LdapDirectory` refuses, or one that cannot be read, ends in the
public `LdapDirectoryLoadException` carrying the file path and `line <n>: <what>` (the texts in
`LdifFaultText`) or the read failure's message, which `Surl.Console` turns into
`CouldNotReadFile` (37) before any listener binds (BL-310).

What this library does not hold: the SASL mechanisms, the NTLM and `DIGEST-MD5` security layers
and the password check live in `Surl.Authentication`, reached only through the
`Surl.Protocol.Abstractions` contracts. Kerberos inside `GSS-SPNEGO` is answered with NTLM
selected (ADR-0040's rule); BL-327 is to decide and build Kerberos there.

`UpstreamCurlSearchesSurlOverLdapTests` in `Surl.Conformance.UnitTests` proves the server against
the pinned Windows build over `WinLDAP` (ADR-0072 decision 10). Nothing yet proves it against the
OpenLDAP-backed build ADR-0076 pins - `STARTTLS`, the root-DSE SASL discovery, SASL binds and
the anonymous bind; that is BL-312's.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`
(for `IContentFileSystem`), one of the horizontal libraries in ADR-0002 decision 3's table, as
later ADRs amend it - nothing else. Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
