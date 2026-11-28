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
the `IAuthenticationPolicy`, the `ISaslAuthenticationPolicy`, a `TimeProvider`, the optional
`isTlsUpgradeAvailable` and an optional `ISurlEventPublisher` the directory publishes its changes
to - and runs one `LdapSession` per connection. `LdapBindJudge` decides each bind into an
`LdapBindAnswer`: simple binds through `IAuthenticationPolicy.CheckPasswordLoginAsync`, the
name mapped by `LdapBindNames`; SASL binds and `WinLDAP`'s Sicily binds
(`LdapSicilyAuthentication`, `LdapSicilyChoice`) through `LdapSaslBindJudge`, which runs the
policy's `ISaslExchange` and holds the exchange in progress between binds. The session answers
searches (the root DSE with the policy's `supportedSASLMechanisms` and, on a connection not
yet TLS while a TLS upgrade is available - a certificate is configured or TLS-SRP is served,
`Surl.Composition`'s `ServerTlsComposition.IsTlsUpgradeAvailable` - `StartTLS` in
`supportedExtension`), compares, writes (refused), `StartTLS` (the upgrade, after discarding
what `LdapMessageFrameReader` read past the request), other extended operations, abandon and
unbind; once a bind installs an `ISaslSecurityLayer` every message both ways is one
4-byte-length buffer the layer protects. It sends the Notice of Disconnection for what it
cannot read (`LdapDiagnostics`) and for a limit, and notes each decision (`LdapLogText`). `ldaps` is the
same exchange inside the engine's implicit TLS: `Surl.Composition`'s
`ProtocolServerRegistrations.Original` registers the server a second time behind
`ImplicitTlsSchemeServer` (`ProtocolServerRegistration.ImplicitTls`; BL-310). Its tests replay request bytes recorded from the
pinned Windows build (`Protocols/Messaging/Surl.Protocol.Ldap.UnitTests/Fixtures/README.md`), the SASL policy and
security layer faked.

The chosen-error fault (ADR-0124 decision 4, ADR-0139; `LdapSession.Faults.cs`): the exchange's
`IFaultPlan` is asked at each `search` (the base DN as its path) and `compare` (the entry DN) once the
bind check lets it read, and at each write (no path), with the account the last bind proved
(`LdapBindAnswer.Account`), before the directory. A matching `error` rule's `<resultCode> <text>` is the
operation's response, with no entry and an empty `matchedDN`, and publishes one `FaultAppliedEvent`. A
`delay` or `drop` rule there is applied through `CommandStageFaults` (ADR-0141, BL-920): a delay holds the
response its `ms` with the idle clock paused, a drop aborts the connection with no response and ends the
session. A reply that does not fit, or another kind, is noted and not applied.

The directory's file (BL-307, ADR-0072 decision 1): the public `LdapDirectoryFile` names
`directory.ldif` in the state folder it is given (`<path>/.surl/ldap`) and reads it once
through `Surl.Content`'s `IContentFileSystem`; a missing file is an empty directory, and the
file is never written. `LdifReader` reads RFC 2849 LDIF content records into
`LdifRecord`s; a file it or `LdapDirectory` refuses, or one that cannot be read, ends in the
public `LdapDirectoryLoadException` carrying the file path and `line <n>: <what>` (the texts in
`LdifFaultText`) or the read failure's message, which `Surl.Composition`'s
`SurlServerComposition.LoadLdapServerAsync` turns into `(37) Could not read <file>: <message>`,
and its start into `CouldNotReadFile` (37), before any listener binds (BL-310).

LDAP operations cannot change the directory (writes are refused); the host can, in memory only
(ADR-0158). `LdapProtocolServer.SeedDirectoryEntries` adds LDIF content records, all or none;
`ResetDirectory` returns to the configured entries - those the server was built with (the file's
as read by `LoadAsync`, none from the public constructor) plus those of every seed made with
`keepOnReset: true` - dropping other seeded ones. A kept seed must be addable to the live entries
and to the configured ones (parent present, no second DN, bounds), and an entry that has a parent
only among the live entries is refused. A refusal changes neither set. `TakeDirectorySnapshot`
copies the live entries and `RestoreDirectory` puts a snapshot of this server's back; neither
changes the configured entries. A seed the directory cannot add, or another server's snapshot, is
an `InvalidOperationException`. Seed, reset and restore publish `StoreChange.Seeded` (one per
entry), `Reset` and `Restored`.

What this library does not hold: the SASL mechanisms, the NTLM, `DIGEST-MD5` and Kerberos
security layers and the password check live in `Surl.Authentication`, reached only through the
`Surl.Protocol.Abstractions` contracts. That includes Kerberos inside `GSS-SPNEGO` (ADR-0072
Amendment 1): with `--keytab`, a bind whose token selects Kerberos is accepted in one leg by
`GssSpnegoSaslExchange` for the service `ldap` - Kerberos must be the client's first mechanism
and the AP-REQ its optimistic token, the ticket good, and its principal an account unless
`--allow-anonymous` - with an RFC 4121 security layer (`KerberosSaslSecurityLayer`) when the
client asks for confidentiality or integrity; without `--keytab` such a token is answered by
ADR-0040's rule, NTLM selected.

`UpstreamCurlSearchesSurlOverLdapTests` in `Surl.Conformance.UnitTests` proves the server against
the pinned Windows build over `WinLDAP` (ADR-0072 decision 10), and
`UpstreamCurlBindsToSurlOverLdapWithKerberosTests` there proves the Kerberos bind and a search
through its sealed layer against that build. `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`,
also there, proves the server against the OpenLDAP-backed build ADR-0076 pins - `STARTTLS`,
`ldaps`, the root-DSE SASL discovery, SASL binds and the anonymous bind. That build is pinned
for `linux-x64` only; elsewhere those tests are Inconclusive.

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
