# ADR-0072 — How the LDAP server answers upstream curl, and what directory it serves

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-284 (FR-049).
- **Amended:** decision 4's `GSSAPI` line by [ADR-0076](ADR-0076-an-openldap-upstream-curl-8-21-0-build-built-reproducibly-in-ci-for-ldap-and-ldaps.md)
  (2026-09-30), which measured the OpenLDAP build (`lib/openldap.c`) and changed no other decision:
  that build has no GSS-API, so the `GSSAPI` measurement moves to BL-342; ADR-0078 (BL-342,
  2026-10-01) measured it and confirmed the line unchanged. Decision 4's Kerberos
  inside `GSS-SPNEGO` line by [Amendment 1](#amendment-1---kerberos-inside-gss-spnego-measured-and-built-bl-327-2026-09-30)
  below (BL-327, 2026-09-30): `WinLDAP` does use Kerberos for a host name, measured, and surl
  answers it with `--keytab` and RFC 4121 wrap tokens. Decisions 3 and 6 by BL-335 (2026-09-30),
  recording what `LdapProtocolServer` (BL-308) does where they left a detail open or could not be
  followed as written: decision 3's diagnostics, the bind DN no account maps from, the compare
  answers and the filter depth limit; decision 6's one diagnostic for the idle timeout and the
  maximum duration. Decisions 4, 5 and 7 by BL-341 (2026-10-01), recording what BL-309's SASL
  and Sicily binds, security-layer framing and `StartTLS` do where they left a detail open:
  decision 4's refusal codes, Sicily's package discovery, which bind continues an exchange and
  when a security layer is replaced; decision 5's `responseName`s, `StartTLS` on a TLS
  connection and the frame reader's read-ahead; decision 7's security-layer and `StartTLS`
  notes. The measurement of the sealed NTLM bind's reconnect and decision 4's Sicily `[10]` row
  by BL-336 (2026-10-01): the reconnect sent bare NTLM, not SPNEGO, and how an SPNEGO-wrapped
  NTLM bind and its `mechListMIC` are answered is
  [ADR-0077](ADR-0077-ldap-ntlm-binds-serve-spnego-wrapped-ntlm-and-check-its-mechlistmic.md).
  Decision 10 by BL-344 (2026-10-01), recording what BL-311 measured with the pinned Windows
  build: the refusal's wording, the three sealed binds, the `ou=many` child in the one-level
  search and why the `ou=many` entries carry `objectClass`.
- **Amends:** [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) decision 3's table
  (LDAP joins the protocols of `ntlm`, `negotiate`, `digest-md5`, `gssapi`, `plain`, `external`)
  and decision 6 (the SASL contract becomes protocol-neutral and gains a security layer, decision
  6 below). [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)'s LDAP rows,
  [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (`ldaps`, `StartTLS`),
  [ADR-0031](ADR-0031-the-data-directory-and-in-memory-mode.md) (service state under
  `<path>/.surl/`), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (logins), [ADR-0033](ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md) (notes),
  [ADR-0034](ADR-0034-curl-style-help-categories-and-the-manual.md) and
  [ADR-0046](ADR-0046-surl-aihelp-markdown-help-for-ai-agents.md) (help), [ADR-0039](ADR-0039-http-ntlm-challenge-and-ntlmv2-check.md)
  and [ADR-0040](ADR-0040-http-negotiate-carrying-ntlm-bare-or-in-spnego.md) (NTLM) and
  [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md) (limit or shutdown)
  are applied as written.

## Context

`surl ldap://...` and `surl ldaps://...` are to be the server upstream curl's `ldap://` and
`ldaps://` transfers talk to (FR-049). BL-289 built the BER codec in `Surl.Protocol.Ldap` with the
BCL's `System.Formats.Asn1`. What was left, and what this ADR decides from measurement, is
everything around the codec: the directory the server searches and where it comes from, every
bind upstream curl makes and how each is checked, the result code of every refusal, `StartTLS`
and `ldaps`, the limits, the notes and the help, so that BL-306 to BL-310 are built without a
question and BL-311 knows what to prove.

**What upstream curl sends on Windows, from its source** (`lib/ldap.c` at tag `curl-8_21_0`, read
2026-09-30): `ldap_init` (or `ldap_sslinit` for `ldaps`), protocol version 3, referrals off, a
10-second network timeout; with `-u` and `--basic` (the default) and a user *and* password,
`ldap_simple_bind_s`; otherwise `ldap_bind_s` with `LDAP_AUTH_NTLM` (`--ntlm`),
`LDAP_AUTH_DIGEST` (`--digest`) or `LDAP_AUTH_NEGOTIATE` (`--negotiate`, and every other case,
with the current Windows user's credentials when no user is given); a failed bind on `ldap://`
retried once with `LDAP_OPT_PROTOCOL_VERSION` 2; then `ldap_search_s` with the URL's base DN,
scope, filter and attributes (RFC 4516). A result other than `LDAP_SUCCESS` and
`LDAP_SIZELIMIT_EXCEEDED` is `CURLE_LDAP_SEARCH_FAILED` (39); a failed bind is
`CURLE_LDAP_CANNOT_BIND` (38). For `ldaps`, `ldap.c` sets `LDAP_OPT_SSL` on and says "Win32 LDAP
SDK does not support insecure mode without CA!": `-k` does not reach `WinLDAP`.

`lib/openldap.c` (the Linux and macOS builds normally carry it: `STARTTLS`, root-DSE
`supportedSASLMechanisms`, SASL binds through curl's own SASL code) is in no pinned build: none of
the pinned Linux and macOS builds lists `ldap` (`UpstreamCurlBuilds.json`). BL-282 asks Stewart
about one, BL-287 pins it and BL-312 proves it. Decisions 4 and 5 decide what the server offers it
from RFC 4511, RFC 4513 and RFC 4422, so the server is built once; BL-287's measurement confirms
or amends them.

### What upstream curl 8.21.0 does (measured)

Measured 2026-09-30 with the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
(`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ... WinLDAP`), through
`Record-CurlExchange.ps1 -Ldap`, added by this task: a loopback LDAP server that reads each BER
message whole, decodes it into one transcript line, and answers it by operation with curl's
message ID echoed - entries from `-LdapEntry` (an entry with an empty DN is the root DSE),
result codes, `serverSaslCreds` and a matched DN from `-LdapReply` (the script's help). Each case
ran `curl -sS <args>` with `-CurlTimeoutMilliseconds 20000`; `E` is the entry
`dn: cn=alice,dc=example,dc=com` with `objectClass: person`, `cn: alice`, `sn: Smith`,
`mail: alice@example.com`; `U` is `ldap://127.0.0.1:18389/dc=example,dc=com`.

**Every run opens two connections.** The first is curl's own connect: for `ldap` it closes with no
byte sent; for `ldaps` it completes a TLS handshake (with `-k`) and closes. The second is
`WinLDAP`'s, which carries the exchange. `WinLDAP` resolves the URL's host itself:
`--resolve ldap.surl.test:18389:127.0.0.1` with `ldap://ldap.surl.test:18389/` ends in 183 ms
with 38 `LDAP local: bind via ldap_win_bind Unavailable` and no second connection. Every message
uses BER's four-byte long-form lengths (`30 84 00 00 00 1B ...`).

**Simple bind and search** (`-u alice:secret U`, entries `E`):

```
> #1 bindRequest version 3 name "alice" simple "secret"
>   30840000001B0201016084000000120201030405616C6963658006736563726574
< #1 bindResponse 0
> #2 searchRequest base "dc=example,dc=com" scope 0 deref 0 sizeLimit 0 timeLimit 0 typesOnly 00 filter (ObjectClass=*) attributes []
>   30840000003E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100020100010100870B4F626A656374436C617373308400000000
< #2 searchResultEntry
< #2 searchResultDone 0
> #3 unbindRequest
>   3084000000050201034200
```

Exit 0, stdout (bytes, `\t` a tab, `\n` a line feed):
`DN: cn=alice,dc=example,dc=com\n\tobjectClass: person\n\n\tcn: alice\n\n\tsn: Smith\n\n\tmail: alice@example.com\n\n`.
No root-DSE probe precedes a simple bind. The bind name is `-u`'s user exactly: `-u
cn=alice,dc=example,dc=com:secret` sends that DN.

| Case | What curl sent, and how it ended |
| --- | --- |
| `U?cn,mail?sub?(&(\|(cn=al*ce)(sn=*mi*))(!(mail=*))(uid>=5)(cn=*ice)(cn=a*))` | scope 2, attributes `[cn, mail]`, the filter as written: `and` of `or`, `not` of a presence, `>=`, substrings with `initial`/`final` (tag `80`, `82`) and `any` (tag `81`); exit 0 |
| `U?*?one` with two entries | scope 1, attributes `[*]`; exit 0, both entries printed |
| `U??sub?(cn=nobody)`, no entries | `searchResultDone 0` alone; exit 0, empty stdout |
| `-u alice:wrong`, bind answered 49 | the bind, then **the same bind with version 2 on the same connection**, also 49, then unbind; exit 38, `curl: (38) LDAP local: bind via ldap_win_bind Invalid Credentials` |
| bind answered 13 | the version 2 retry, 13; exit 38 `... Confidentiality Required` |
| `dc=nowhere`, search answered 32 | exit 39, `curl: (39) LDAP remote: No Such Object`, nothing printed |
| search answered 4 (`sizeLimitExceeded`) after one entry | exit 0, the entry printed |
| search answered 3 (`timeLimitExceeded`) after one entry | exit 39, `... Time Limit Exceeded`, **nothing printed** |
| a multi-valued attribute (`objectClass: top`, `objectClass: person`, two `mail`) | each value its own `\t<type>: <value>\n` line, one `\n` after the attribute's last value |
| `userCertificate;binary:: AAEC/w==`, `jpegPhoto:: AAEC/w==`, `description: café` | `\tuserCertificate;binary:: AAEC/w==\n\n`, `\tjpegPhoto:: AAEC/w==\n\n`, `\tdescription:: Y2Fmw6k=\n\n`: a `;binary` type, a non-printable byte and a non-ASCII value are each printed as `:: ` and base64 |
| 1000 entries, `??one` | exit 0, 96679 bytes, every entry |
| the server closes the connection instead of answering the search | `WinLDAP` reconnects, binds again, **sends the search again with the same message ID**; a second close ends it: exit 39 `LDAP remote: Unavailable` |
| the server closes instead of answering the bind | the version 2 retry on a new connection, closed too: exit 38 `... Unavailable` |
| `--ssl-reqd U` | curl's own connection only, then exit 4 `curl: (4) LDAP local: explicit TLS not supported` |
| `-k ldaps://127.0.0.1:18636/...` against a throwaway self-signed certificate (`-Tls`) | curl's handshake completes; `WinLDAP`'s own handshake on the second connection fails: exit 38 `... Server Down` |
| `ldaps://...` without `-k` | exit 60, `schannel: SEC_E_UNTRUSTED_ROOT (0x80090325)`, on curl's own handshake |

**Every bind other than a simple one.** Before it, `WinLDAP` reads the root DSE: a base search of
`""` with `(objectclass=*)`, `timeLimit 120`, for `supportedCapabilities`, then
`supportedSASLMechanisms`, then `supportedCapabilities` again (the second and third only when it
is not going straight to the bind below). Then:

| Case | The bind `WinLDAP` sends |
| --- | --- |
| `--ntlm -u alice:secret U` | **Sicily**, whatever the root DSE lists: `bindRequest version 3 name "NTLM"`, authentication choice `[10]` (`8A`, MS-ADTS 5.1.1.1.3's `sicilyNegotiate`) holding an NTLM `NEGOTIATE_MESSAGE`, flags `E20882B7` |
| `--negotiate -u alice:secret U`, the root DSE listing `GSS-SPNEGO` | SASL `GSS-SPNEGO` with a **bare** NTLM `NEGOTIATE_MESSAGE` (no SPNEGO wrapper) as credentials, name `""` |
| no `-u`, `-u :` or `-u alice:` (an empty password), root DSE listing `GSS-SPNEGO` | the same, with the current Windows user's credentials (the `NEGOTIATE_MESSAGE` names the workstation and domain); listing none of it, Sicily. So **the Windows build never binds anonymously** |
| `--digest -u alice:secret U`, root DSE listing `DIGEST-MD5` | SASL `DIGEST-MD5` with no credentials |
| `--negotiate` with the test KDC of ADR-0065 running and `ldap/127.0.0.1` in its keytab | still bare NTLM in `GSS-SPNEGO`: the KDC saw nothing, so for `127.0.0.1` `WinLDAP` never tries Kerberos |

With a `CHALLENGE_MESSAGE` scripted (flags `E2888205`, no signing or sealing granted, target info
`SURL`):

- **Sicily:** answered `bindResponse 0` with the `CHALLENGE_MESSAGE` as the **matched DN**,
  `WinLDAP` sends `bindRequest name ""` with choice `[11]` (`8B`, `sicilyResponse`) holding the
  `AUTHENTICATE_MESSAGE` (NTLMv2, user `alice`, empty domain, flags `E2888235`: **sign and seal
  asked for although the challenge did not grant them**, key exchange, 128-bit).
- **`GSS-SPNEGO`:** answered `bindResponse 14` (`saslBindInProgress`) with the
  `CHALLENGE_MESSAGE` as `serverSaslCreds`, `WinLDAP` sends a second `GSS-SPNEGO` bind holding the
  bare `AUTHENTICATE_MESSAGE`, the same flags.
- **Both:** answered `bindResponse 0`, `WinLDAP`'s next message is **not an LDAPMessage**: a SASL
  security-layer buffer, `00 00 00 54`, then 84 bytes: `01 00 00 00` + 8 bytes + `00 00 00 00`
  (an NTLM signature, version 1, sequence number 0), then 68 bytes - exactly the length of the
  plain base search above. Every message after an NTLM bind is sealed with NTLM's session keys
  (MS-NLMP 3.4). The recorder cannot unseal it and closed; curl exited 39 `LDAP remote: Server
  Down` (after the reconnect, a version 2 Sicily bind holding a bare NTLM token). *Corrected by
  BL-336:* this line first said the reconnect wrapped the NTLM token in SPNEGO; BL-330 could not
  reproduce that in ten configurations (Sicily and `GSS-SPNEGO`, a closed or refused bind and a
  challenge granting sign and seal or not, every root DSE listing, `127.0.0.1` and `localhost`),
  and BL-329's `Fixtures/ldap-negotiate-sealed` shows the same bare version 2 bind: `WinLDAP`
  sends bare NTLM in every measured configuration.
- **`DIGEST-MD5`:** answered `bindResponse 14` with
  `realm="surl.test",nonce="...",qop="auth",charset=utf-8,algorithm=md5-sess` (ADR-0049's mail
  challenge), `WinLDAP` sends nothing more for it and starts again; exit 38 `Protocol Error`. With
  `realm="surl",nonce="...",qop="auth,auth-int,auth-conf",cipher="rc4-40,rc4-56,rc4,des,3des",maxbuf=65536,charset=utf-8,algorithm=md5-sess`
  it answers `username="alice",realm="",nonce="...",digest-uri="ldap/127.0.0.1",cnonce="...",nc=00000001,response=...,qop=auth-conf,cipher=3des,charset=utf-8`:
  **only confidentiality will do, with 3DES**; a final `bindResponse 0` without `rspauth` in
  `serverSaslCreds` is again `Protocol Error` (exit 38).
- Any answer `WinLDAP` cannot use ends 38 `Protocol Error`, after it tried once more on a new
  connection.

## Decision

### 1. The directory

**The entry model.** An entry is a DN and an ordered list of attributes; an attribute is an
attribute description (a type and options, RFC 4512 section 2.5, e.g. `userCertificate;binary`)
and one or more values, each an octet string. There is no schema to load and no schema checking:
an entry holds what its source gave it. `objectClass` is an attribute like any other.

**DNs** are parsed by RFC 4514 (`\`-escapes, `\XX` hex pairs, `#` BER values, multi-valued RDNs
with `+`) and compared in a **normal form**: each attribute type lower-cased (a numeric OID kept),
each value unescaped then matched by its type's equality rule below, the RDNs' AVAs sorted. An
entry's DN as written is what a search returns.

**Matching rules** (RFC 4517), by attribute type, case-insensitively:

| Types | Equality, approx | Ordering (`>=`, `<=`) | Substrings |
| --- | --- | --- | --- |
| `userPassword`, `jpegPhoto`, `userCertificate`, `cACertificate`, `photo`, `audio`, any description with the `binary` option | `octetStringMatch` | `octetStringOrderingMatch` | none (Undefined) |
| `uidNumber`, `gidNumber`, `shadowLastChange`, `shadowMin`, `shadowMax`, `shadowWarning`, `shadowInactive`, `shadowExpire` (RFC 2307) | `integerMatch` (a value that is not an integer is Undefined) | `integerOrderingMatch` | none (Undefined) |
| every other type | `caseIgnoreMatch` after RFC 4518's insignificant-space handling (leading and trailing spaces dropped, inner runs of spaces made one) and Unicode simple case folding | `caseIgnoreOrderingMatch`, ordinal on the folded values | `caseIgnoreSubstringsMatch` |

- **Filters** (RFC 4511 section 4.5.1.7) are three-valued: TRUE, FALSE, Undefined. An item naming
  a type the entry lacks is FALSE (presence) or FALSE (the rest); a rule that does not apply is
  Undefined; `and`, `or` and `not` combine as the RFC says, and only TRUE returns the entry. An
  empty `and` is TRUE and an empty `or` FALSE (RFC 4526). `approxMatch` is equality.
  `extensibleMatch` applies the named rule when it is one of `caseIgnoreMatch` (2.5.13.2),
  `caseExactMatch` (2.5.13.5), `octetStringMatch` (2.5.13.17) or `integerMatch` (2.5.13.14), the
  type's own equality rule when none is named, and is Undefined for any other rule; with
  `dnAttributes` it is TRUE also when an AVA of the entry's DN matches.
- **Attribute selection** (RFC 4511 section 4.5.1.8): none or `*`, every attribute; `1.1` alone,
  none; `+`, no more (the directory has no operational attributes but the root DSE's); named
  ones, case-insensitively, a name without options also selecting its descriptions with options;
  a name the entry lacks is skipped. `typesOnly` sends each selected attribute with no values.

**The root DSE** (RFC 4512 section 5.1) is computed per connection, never stored, and answers a
base search of `""` whatever the bind state (`WinLDAP` reads it before binding): `objectClass:
top`, and, when requested by name or by `+`, `namingContexts` (decision 1's naming contexts, in
DN order), `supportedLDAPVersion: 3`, `supportedSASLMechanisms` (decision 4's offer for this
connection, one value each, in order; left out when empty) and `supportedExtension:
1.3.6.1.4.1.1466.20037` (only when `StartTLS` is offered, decision 5). No vendor name or version
(ADR-0006 section 3). A search of `""` with scope `one` or `sub` finds no entries: the root DSE is
not the parent of the naming contexts.

**A naming context** is an entry whose parent DN is not in the directory. Every other entry's
parent must be.

**Where entries come from.** `<path>/.surl/ldap/directory.ldif` under `--directory`, read once at
start after the data-directory lock and before any listener binds (ADR-0031 decision 7's order);
`Surl.Console` does it (BL-310) and `Surl.Protocol.Ldap` parses it (BL-307). A missing file, and
in-memory mode (no `--directory`), is **an empty directory** (ADR-0031 decision 2): every search
but the root DSE's answers `noSuchObject`. **No option names another seed file** (no new
command-line option, ADR-0007): an operator who wants entries writes the LDIF file. The directory
is **read-only while serving** (decision 3: Add, Delete, Modify and ModifyDN are refused), so it
is never written and needs no write path.

**The file's format** is RFC 2849 LDIF, content records only, UTF-8 without a byte order mark:

- An optional first line `version: 1`. Lines end in LF or CRLF. A line that starts with one space
  continues the line before it (the space dropped). A line starting `#` is a comment, and its
  continuation lines are comment too. Records are separated by one or more empty lines.
- A record is `dn: <value>` or `dn:: <base64>`, then one or more `<description>: <value>`,
  `<description>:: <base64>` lines; `<description>` is `<type>(;<option>)*`, `<type>` a name
  (`[A-Za-z][A-Za-z0-9-]*`) or a numeric OID. A `SAFE-STRING` value is taken as written after the
  separator's spaces; any other value must be base64.
- Values of one description in one record are kept in file order and are one attribute; repeating
  a description later in the record adds values to it. Records are kept in file order, and a
  search returns entries in that order.

**Malformed at start** - surl refuses to start with `CouldNotReadFile` (37) and stderr
`surl: (37) Could not read <file path>: <reason>` (ADR-0050 decision 7's form), `<reason>` being
the exception message when the file cannot be read, and otherwise `line <n>: <what>`, `<n>` the
first physical line of the fault and `<what>` one of: `not UTF-8`; `version is not 1`; `a
continuation line with nothing to continue`; `a record without dn`; `a DN that is not RFC 4514`;
`a line that is not <description>: <value>`; `bad base64`; `a value that is not a SAFE-STRING`;
`a changetype record` (change records are refused, the directory is not edited by its file);
`a URL value` (`:<` is refused: the server reads nothing outside its data directory, ADR-0031
decision 8); `a duplicate DN`; `a parent DN that is not in the file` (checked once the file is
read, against the record's own line); `an entry with no attribute`; `past the directory's
bounds`. Starting empty instead would hide the operator's mistake.

**Bounds** (ADR-0006: every store a peer can fill is bounded; this one is filled by the operator,
and bounded all the same), constructor parameters with these defaults and no option:

| Constant | Default |
| --- | --- |
| `LdapDirectory.DefaultMaxEntries` | 100000 entries |
| `LdapDirectory.DefaultMaxTotalBytes` | 268435456 bytes (256 MiB) of DNs, descriptions and values, as mail's store (ADR-0050 decision 6) |
| `LdapDirectory.MaxValueBytes` | 1048576 bytes, one value (a value past `--max-message` could never be sent) |
| `LdapDirectory.DefaultMaxSearchEntries` | 10000 entries returned by one search |

A search returns at most the smaller of its `sizeLimit` (0 meaning none) and
`DefaultMaxSearchEntries`, then `sizeLimitExceeded` (4) if more matched: curl prints the entries
it got and exits 0 (measured). A search's `timeLimit` (0 meaning none) is honoured, answered
`timeLimitExceeded` (3) with the entries sent so far; curl then exits 39 and prints nothing
(measured). The directory is safe for concurrent searches (it never changes once loaded).

### 2. Binds

**Who may search.** LDAP has a login, so ADR-0032's first principle holds: with accounts
configured or not, **a search other than the root DSE's needs a successful bind on the
connection**, unless `--allow-anonymous` is given. A bound identity, and an anonymous session
under `--allow-anonymous`, reads the whole directory: there is no access control per entry. A
search refused for want of a bind is `insufficientAccessRights` (50) with no matched DN and the
diagnostic `bind first`, whether or not the base exists (ADR-0006 section 3: nothing is learned
about the directory before a bind).

**The simple bind** (RFC 4513 section 5.1), checked through
`IAuthenticationPolicy.CheckPasswordLoginAsync` with the method `simple` in the login note:

| The bind | Answer |
| --- | --- |
| name and password empty (anonymous bind, RFC 4513 5.1.1) | `success` under `--allow-anonymous`; otherwise `inappropriateAuthentication` (48), `anonymous bind refused` |
| a name and an empty password (unauthenticated bind, 5.1.2) | `unwillingToPerform` (53), `unauthenticated bind refused`, as RFC 4513 recommends |
| a password, on a connection without TLS, without `--allow-plaintext-auth` or `--allow-anonymous` | `confidentialityRequired` (13), `simple bind needs TLS or --allow-plaintext-auth`, **without checking it** (ADR-0032 criterion 3); measured: curl 38 `Confidentiality Required` |
| a password, otherwise | the policy's verdict: `success`, or `invalidCredentials` (49) with no diagnostic after ADR-0032 section 8's one-second delay, the same for no such user, a wrong password and no accounts; measured: curl 38 `Invalid Credentials` |

**The bind name maps to an account name** so: a name with no unescaped `=` is the account name as
sent (curl's `-u alice:secret`); a DN is the value of its leftmost RDN when that RDN is one AVA
of type `uid` or `cn` (`cn=alice,dc=example,dc=com` is `alice`); any other DN is an account no
one has (49 after the delay). Directory entries carry no credentials: `userPassword` is data, and
passwords are `--user` and `--user-file`'s (ADR-0032 section 2).

**Version 2.** A bind with `version` 2 is answered exactly as version 3 (the same checks, the same
code): `WinLDAP` sends one only to retry a refused bind, and curl's message names the retry's
code (measured), so a version 2 refusal of its own (`protocolError`) would hide the real reason.
The session that follows is version 3 regardless. Any other version is `protocolError` (2).

**A bind resets the connection's identity** (RFC 4511 section 4.2.1): a failed bind leaves it
anonymous. A bind while a SASL bind is in progress abandons that exchange unless it continues
it; decision 4 states which binds continue an exchange.

### 3. Operations, result codes and the Notice of Disconnection

| Request | Answer |
| --- | --- |
| Bind | decisions 2 and 4 |
| Search | decision 1's entries, then `searchResultDone`: `success`; `noSuchObject` (32) with the matched DN of the nearest existing superior (RFC 4511 section 4.1.9) when the base does not exist and the session may search; `invalidDNSyntax` (34), `the base is not an RFC 4514 DN`, for a base that is not an RFC 4514 DN; decision 1's `sizeLimitExceeded`, `timeLimitExceeded`; `insufficientAccessRights` (50) per decision 2. No referrals, no `searchResultReference` |
| Compare | `compareTrue` (6) when decision 1's equality rule answers TRUE, `compareFalse` (5) when it answers FALSE **or Undefined**, `undefinedAttributeType` (17) for a type the entry lacks, `noSuchObject`, `invalidDNSyntax` (34), `the entry is not an RFC 4514 DN`, for an entry DN that is not RFC 4514, and decision 2's access rule |
| Add, Delete, Modify, ModifyDN | `unwillingToPerform` (53), `the directory is read-only over LDAP` |
| Extended: `StartTLS` | decision 5 |
| Extended: any other OID | `extendedResponse` `protocolError` (2), `unsupported extended operation`, with no `responseName` (RFC 4511 section 4.12) |
| Abandon | no response (RFC 4511 section 4.11); operations are answered in order, so nothing is ever pending to abandon |
| Unbind | the connection is closed, nothing sent |
| A control marked critical | `unavailableCriticalExtension` (12), `critical control not supported`, for the operation (no control is supported); a non-critical one is ignored |
| An unknown operation, or a message the codec refuses (BL-289's `LdapDecodeOutcome`) | the **Notice of Disconnection** (RFC 4511 section 4.4.1: `extendedResponse`, message ID 0, `responseName` `1.3.6.1.4.1.1466.20036`) with `protocolError` (2) and a diagnostic naming the fault, then the connection is closed |

Diagnostics are short ASCII, never echo a peer's value (ADR-0006 section 3), and say nothing
that differs between a missing user, a wrong secret and no accounts.

**The details this decision left open** (amended by BL-335, recording BL-308's code; decided by
Claude under Stewart's delegation):

- **The bind diagnostics.** A bind of version 1, or of 4 and up, is `protocolError` (2), `only
  LDAP versions 2 and 3 are answered`. An authentication choice that is neither `simple`, `sasl`
  nor one of Sicily's `[9]`, `[10]`, `[11]` (the reserved tags 1 and 2, say), and a SASL or
  Sicily mechanism the policy does not accept, is `authMethodNotSupported` (7), `authentication
  method not accepted`. (BL-308 first answered every Sicily and SASL bind so, with `only simple
  binds are answered`; decision 4's exchanges have since replaced that.)
- **A bind DN no account maps from** - a DN whose leftmost RDN is not one `uid` or `cn` AVA - is
  checked by the policy with the whole DN as the user name, an account no one has, so it fails
  `invalidCredentials` (49) after the same delay as a wrong password and tells the peer nothing
  more (`LdapBindNames.AccountNameOf`).
- **A compare whose rule answers Undefined** (an `integerMatch` type compared with a value that is not an integer, say)
  is `compareFalse`: RFC 4511 section 4.10 has no third answer, and `compareFalse` claims no more
  than that the assertion did not hold.
- **Filter depth.** A search filter that nests `and`, `or` and `not` deeper than
  `LdapProtocolServer.MaxFilterDepth` (64) is a message the codec refuses, so it is answered with
  the Notice of Disconnection `protocolError`, `a filter nested too deep`, and the connection is
  closed. The bound keeps the decoder's recursion, and its stack, finite.
- **The Notice of Disconnection's diagnostics** are `LdapDiagnostics`': for a frame the reader
  refuses, `not an LDAPMessage`, `an indefinite length` or `a length of more than four octets`;
  for a message the decoder refuses, `a malformed tag or length`, `an indefinite length`, `an
  unexpected tag`, `a missing element`, `trailing bytes`, `an enumeration out of range`, `an
  invalid value` or `a filter nested too deep`; for an operation tag that is no LDAP operation,
  `unknown operation`.

### 4. SASL binds and the security layer

**What `WinLDAP` needs, from the measurements:** NTLM over Sicily (`--ntlm`), NTLM inside
`GSS-SPNEGO` (`--negotiate`, and every bind without a user and password), and `DIGEST-MD5`
(`--digest`) - and **after every one of them, a SASL security layer**: NTLM sealing for the first
two, `DIGEST-MD5` confidentiality with 3DES for the third. A server that cannot seal and unseal
cannot serve any of them, so the security layer is part of the login, not an extra.

**The mechanisms offered** in `supportedSASLMechanisms`, computed per connection (TLS state,
`--auth`, `--allow-plaintext-auth`), in ADR-0049 decision 2's order with `GSS-SPNEGO` beside
`GSSAPI`: `GSSAPI` (`gssapi`, with `--keytab`), `GSS-SPNEGO` (`negotiate`), `DIGEST-MD5`
(`digest-md5`), `CRAM-MD5`, `NTLM`, `OAUTHBEARER`, `XOAUTH2`, `PLAIN`, `LOGIN` (plain-text ones
only over TLS or with `--allow-plaintext-auth`, ADR-0049 decision 1), `EXTERNAL` (with a client
certificate). The `--auth` words are ADR-0049's; LDAP joins their protocol column. Sicily is not
SASL and is not listed: it is answered when `ntlm` is accepted.

| `WinLDAP` bind | Mechanism (`--auth` word) | Exchange |
| --- | --- | --- |
| Sicily `[9]` `sicilyPackageDiscovery` | `ntlm` | `bindResponse success`, matched DN `NTLM` (MS-ADTS 5.1.1.1.3); `authMethodNotSupported` (7) when `ntlm` is not accepted |
| Sicily `[10]` `sicilyNegotiate` | `ntlm` | the token goes to the NTLM handshake (bare NTLM, which is what `WinLDAP` sends in every measured configuration (BL-330); an SPNEGO-wrapped token is still served, by ADR-0040's rules and [ADR-0077](ADR-0077-ldap-ntlm-binds-serve-spnego-wrapped-ntlm-and-check-its-mechlistmic.md)); its `CHALLENGE_MESSAGE` goes back as `bindResponse success` with the message as the **matched DN** (measured) |
| Sicily `[11]` `sicilyResponse` | `ntlm` | the `AUTHENTICATE_MESSAGE` checked by ADR-0039's NTLMv2 check: `success` with nothing else, or `invalidCredentials` (49) after the delay. A `[11]` with no `[10]` before it on the connection is `protocolError` (2) |
| SASL `GSS-SPNEGO` | `negotiate` | a bare NTLM token is the NTLM handshake as above, its `CHALLENGE_MESSAGE` sent as `saslBindInProgress` (14) `serverSaslCreds` (measured); an SPNEGO token follows ADR-0040 decision 3 (NTLM is the mechanism selected) with ADR-0040's `negTokenResp`s as `serverSaslCreds`, the last one on `success` |
| SASL `DIGEST-MD5` | `digest-md5` | ADR-0049 decision 5's exchange with an LDAP challenge `realm="surl",nonce="<base64 of 16 random bytes>",qop="auth,auth-int,auth-conf",cipher="3des,rc4",maxbuf=65536,charset=utf-8,algorithm=md5-sess`; the response checked as ADR-0049 says (`digest-uri` `ldap/<host>`, hashed as sent); success is `bindResponse success` **with `rspauth=<hex>` as `serverSaslCreds`** (RFC 4422 section 5's additional data; measured: without it `WinLDAP` fails) |
| any other SASL mechanism | its word | ADR-0049 decision 5's exchange: each challenge `saslBindInProgress` with `serverSaslCreds`, the end `success` or `invalidCredentials` |

- **The NTLM `CHALLENGE_MESSAGE` for LDAP** grants what the `NEGOTIATE_MESSAGE` asked for of
  signing (`NEGOTIATE_SIGN`), sealing (`NEGOTIATE_SEAL`), key exchange, 128-bit and 56-bit keys
  and extended session security, as MS-NLMP 3.2.5.1.1 has a server do. ADR-0039's HTTP challenge
  grants none and stays so: HTTP has no security layer.
- **The security layer starts with the first message after the successful bind's response**, in
  both directions (RFC 4422 section 3.7, RFC 4513 section 5.2.1.6), and every message is then one
  buffer: a 4-byte big-endian length then that many protected bytes.
  - **NTLM** (Sicily and `GSS-SPNEGO`), with the flags of the `AUTHENTICATE_MESSAGE`: sealed when
    `NEGOTIATE_SEAL` is set, else signed when `NEGOTIATE_SIGN` is, else no layer. The protected
    bytes are the 16-byte signature then the sealed message (measured order), MS-NLMP 3.4.4.2
    with extended session security: `SealingKey` and `SigningKey` derived per direction from the
    exported session key (the key-exchange key decrypted with RC4 when `NEGOTIATE_KEY_EXCH`),
    RC4 kept running across messages, the checksum encrypted with the same RC4, sequence numbers
    from 0 per direction. RC4 is `Surl.Cryptography.Rc4`.
  - **`DIGEST-MD5`** with `qop=auth-int` or `auth-conf`: RFC 2831 sections 2.3 and 2.4 - `Kic`,
    `Kis`, `Kcc`, `Kcs`, a 10-byte HMAC-MD5 MAC, the 2-byte message type 1 and a 4-byte sequence
    number; `3des` (two-key 3DES-CBC, the BCL's `TripleDES`, its IV from the key as section 2.4
    says) or `rc4` for `auth-conf`. `qop=auth`: no layer.
  - **`GSSAPI`** (RFC 4752): the server's last challenge offers "no security layer" only (bit 1,
    max size 0), so the session continues in clear; ADR-0057's check is unchanged. Confirmed by
    [ADR-0078](ADR-0078-ldap-sasl-gssapi-measured-against-an-openldap-build-with-mit-kerberos.md)
    (BL-342): the OpenLDAP build with MIT Kerberos answers every offer with no security layer and
    max size 0, and asks for no mutual authentication.
  - A buffer past `--max-message`, one that fails its signature or MAC, or one out of sequence
    ends the connection with no answer (the peer's keys are no longer trusted, so a Notice of
    Disconnection could not be read).
- **No unchecked NTLM or `DIGEST-MD5`.** Their security layer's keys come from the account's
  password, so under `--allow-anonymous` they are checked like any other login when the user has
  an account, and refused (`invalidCredentials`, the note saying `the security layer needs the
  account's password`) when not: an accepted bind whose traffic surl cannot unseal would serve
  nothing. Every other mechanism keeps ADR-0049's `--allow-anonymous` rule.
- **Kerberos inside `GSS-SPNEGO`** (an SPNEGO token naming Kerberos first): `WinLDAP` never
  sends it for a loopback address (measured). *Amended by Amendment 1:* for a host name it does,
  and with `--keytab` surl accepts it in one leg with RFC 4121 wrap tokens as the security layer;
  without `--keytab` it is answered by ADR-0040's rule, NTLM selected.

**The contract** (amends ADR-0049 decision 6; BL-328 builds it). The SASL contract is
protocol-neutral, since LDAP is not mail: `MailLoginStep` becomes `SaslLoginStep`,
`MailLoginOutcome` becomes `SaslLoginOutcome`, and `StartSaslExchange` with the per-connection
mechanism list moves to a new `ISaslAuthenticationPolicy` (`IReadOnlyList<string>
GetSaslMechanisms(SaslOfferRequest request)`, `ISaslExchange StartSaslExchange(SaslExchangeStart
start)`), which `IMailAuthenticationPolicy` extends with its mail-only members (`MailLoginOffer`,
`CheckApopLoginAsync`), so no mail server's behaviour changes. `SaslExchangeStart` gains `bool
CanCarrySecurityLayer` (false for the mail servers: their challenges stay as ADR-0049 says; true
for LDAP, which selects the LDAP challenges above), and `SaslLoginStep` gains `ISaslSecurityLayer?
SecurityLayer`, set on an accepted step that negotiated one. `ISaslSecurityLayer` holds `int
MaximumProtectedBytes`, `byte[] Protect(ReadOnlySpan<byte> message)` and `bool
TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message)`; the server owns the 4-byte length
framing. `ntlm` and the Sicily exchange use the same `ISaslExchange` under the mechanism name
`NTLM`, the server mapping Sicily's choices to it. BL-329 builds NTLM sealing and the LDAP NTLM and
`GSS-SPNEGO` exchanges in `Surl.Authentication`; BL-326 builds `DIGEST-MD5`'s integrity and
confidentiality.

**The details this decision left open** (amended by BL-341, recording BL-309's code in
`LdapBindJudge` and `LdapSaslBindJudge`; decided by Claude under Stewart's delegation):

- **The refusal codes.** A step the policy answers `RefusedPlaintext` is
  `confidentialityRequired` (13), `SASL mechanism needs TLS or --allow-plaintext-auth`: the
  simple bind's code for the same rule (decision 2), which curl reports as `Confidentiality
  Required` (measured). A step answered `RefusedMechanism`, and any authentication choice other
  than simple, SASL and Sicily's `[9]`, `[10]` and `[11]`, is `authMethodNotSupported` (7),
  `authentication method not accepted` (decision 3's bind diagnostics). `RefusedCredentials` is
  `invalidCredentials` (49) with no diagnostic, as the table above says.
- **Sicily `[9]`** is answered `success` with `NTLM` as the matched DN when the policy's SASL
  offer for the connection (`ISaslAuthenticationPolicy.GetSaslMechanisms`, compared
  case-insensitively) lists `NTLM`, and `authMethodNotSupported` (7), `authentication method not
  accepted`, otherwise. The reason: the SASL authentication policy has no separate question
  for "is `ntlm` accepted", and its offer already answers it per connection, TLS state and
  `--auth` included.
- **Which bind continues an exchange.** A SASL bind continues the SASL exchange in progress only
  when it names the same mechanism, compared case-insensitively as the policy matches mechanism
  names; Sicily `[11]` continues only a Sicily exchange, and with none in progress is
  `protocolError` (2), `sicilyResponse without sicilyNegotiate`. Every other bind - simple, SASL
  with another mechanism, Sicily `[9]` or `[10]`, a `[11]` refused while a SASL (not Sicily)
  exchange is in progress, a bad version, another authentication choice - abandons the exchange
  in progress before it is answered (RFC 4513 section 5.2.1.2, RFC 4511 section 4.2.1). An exchange that ends - accepted or refused - is no longer in progress.
- **A later bind's security layer replaces the earlier one** once that bind's response has been
  sent (the response itself goes out under the earlier layer); a later bind that installs no
  layer - a simple bind, a refused one, a mechanism with no layer - keeps the installed layer,
  although a refused bind still leaves the connection anonymous (decision 2). The reason:
  nothing in RFC 4422 lets a client drop a layer once installed, and its traffic stays protected
  by the keys both sides already hold.

### 5. `StartTLS` and `ldaps`

- **`StartTLS`** (OID `1.3.6.1.4.1.1466.20037`, RFC 4511 section 4.14) is offered - listed in the
  root DSE and accepted - only when a certificate is configured (`--cert` or `--self-signed`) and
  the connection is not TLS yet, as the mail servers' upgrades are (ADR-0010). Accepted:
  `extendedResponse success` with the `responseName`, then the TLS handshake; bytes pipelined
  after the request are discarded (ADR-0010). Not offered (no certificate): `protocolError` (2),
  as an unknown extended operation. On a connection already TLS: `operationsError` (1), `TLS is
  already established`. During a SASL bind in progress, or after a bind with a security layer:
  `operationsError` (1). After the upgrade the root DSE, the offer and the plain-text rules are
  computed again.
- **`ldaps`** is TLS from the first byte through `ImplicitTlsSchemeServer` (ADR-0010), the
  certificate from `--cert` or `--self-signed`; after the handshake the exchange is `ldap`'s byte
  for byte, with `StartTLS` not offered.
- **The Windows build cannot complete `ldaps` against a certificate Windows does not trust**:
  `WinLDAP` checks the certificate itself and ignores `-k` (measured: 38 `Server Down`). Trusting
  surl's certificate would mean adding it to the machine's certificate store, which the
  conformance tests do not do; they prove the handshake curl itself makes and the 38. BL-312 proved
  `ldaps` through the OpenLDAP build, which honours `-k` and `--cacert`: exit 0 with `-k` and
  with `--cacert` naming the CA that signed surl's `--cert`, 60 without either (ADR-0076
  Amendment 1, 2026-09-30).

**The details this decision left open** (amended by BL-341, recording BL-309's code in
`LdapSession` and `LdapMessageFrameReader`; decided by Claude under Stewart's delegation):

- **`responseName`.** `StartTLS`'s `success` and `operationsError` answers carry its OID,
  `1.3.6.1.4.1.1466.20037`, as `responseName` (RFC 4511 section 4.14.2 requires it). The
  no-certificate refusal carries none: it is decision 3's answer to an unknown extended
  operation, `protocolError` (2), `unsupported extended operation`, and an unknown operation
  names nothing (RFC 4511 section 4.12).
- **`StartTLS` on a TLS connection** is `operationsError` (1), `TLS is already established`,
  with or without a certificate configured, so `ldaps` gets that code whatever the console
  passes the server. The other `operationsError` refusals are `a SASL bind is in progress` and
  `a security layer is installed`, checked in that order after the TLS one.
- **A `requestValue` is ignored**: RFC 4511 section 4.14.1 gives `StartTLS` none, so one sent
  anyway carries nothing the server could act on.
- **The upgrade keeps the connection's bind state**: a bound connection is still bound under
  TLS, since RFC 4513 does not reset it. (A SASL bind in progress or a security layer is never
  carried across: `StartTLS` is refused in both cases, above.)
- **Discarding pipelined bytes** (ADR-0010). `LdapMessageFrameReader` reads a message's value in
  reads of at least `ReadAheadBytes` (4096) and holds whatever arrived past the message for the
  next read; before the handshake `DiscardReadAhead` throws the held bytes away, so bytes the
  client pipelined after `StartTLS` and that arrived with it are discarded, never run. The tag
  and length are still read one byte at a time, so a message past `--max-message` (and a
  security-layer buffer past it or the layer's maximum) is still refused from its length before
  any of its value is read.

### 6. Limits (ADR-0006)

| Limit | LDAP behaviour |
| --- | --- |
| `--max-message` (1 MiB) | one `LDAPMessage`, by its BER length, and one security-layer buffer, by its 4-byte length: before the body is read, the Notice of Disconnection `protocolError` (`a message of <n> bytes is past --max-message <m>`) on a clear connection, and the close alone inside a security layer |
| `--head-timeout` (30 s) | the first message of a connection (ADR-0006's "first packet"): the close, no Notice (nothing has been said yet) |
| `--idle-timeout` (120 s) | the Notice of Disconnection `unavailable` (52), `idle timeout or maximum duration`, on ADR-0059's one-second deadline, then the close. *Amended by BL-335:* ADR-0059 decision 5 gives a server no reason for a cancellation, so this limit and `--max-time` cannot be told apart and share the one diagnostic |
| `--max-time` (3600 s) | the same Notice, with the same diagnostic `idle timeout or maximum duration` (ADR-0059 decision 5) |
| `--max-connections`, `--max-connections-per-address` | the engine's, before any byte |
| Shutdown | no farewell (ADR-0059 decision 4): the connection is closed |
| Directory bounds | decision 1 |

Inside a security layer the Notice of Disconnection is protected like any other message. Curl's
answer to a close is `WinLDAP`'s one reconnect and retry (measured), so a limit costs curl one
more connection, which meets the same limit.

### 7. Verbose and trace notes

Notes are `IExchangeLog.Note` at `verbose` and above (ADR-0033 section 3), peer values escaped as
ADR-0006 section 3 says; message bytes are in the engine's `--trace` dumps as they crossed the
wire (sealed ones sealed), with no extra note per message.

| When | Note |
| --- | --- |
| A bind decided | the login note, `CheckedLogin.Note` (ADR-0038), method `simple`, `NTLM` or the SASL mechanism |
| A bind refused unchecked | `LDAP bind refused: <code name>: <diagnostic>`, e.g. `LDAP bind refused: confidentialityRequired: simple bind needs TLS or --allow-plaintext-auth` |
| A security layer installed | `LDAP security layer: <mechanism>`, the mechanism the bind named (`NTLM` for Sicily). *Amended by BL-341:* the note does not say sealing, signing or the cipher, because `ISaslSecurityLayer` carries no description of itself, and widening the contract in `Surl.Protocol.Abstractions` for one log line was outside BL-309 |
| A security-layer buffer that fails its check (signature, MAC or sequence) | `A security-layer buffer failed its check; closed with no reply.` *Amended by BL-341* |
| A security-layer buffer whose length is past the layer's `MaximumProtectedBytes` or `--max-message` | `A security-layer buffer of <n> bytes is past what the layer or --max-message allows; closed with no reply.`, `<n>` the buffer's announced length. *Amended by BL-341* |
| A search answered | `LDAP search <base> scope <base, one or sub>: <n> entries, <code name>` |
| A write refused | `LDAP <operation> refused: the directory is read-only` |
| `StartTLS` | `LDAP StartTLS accepted` or `LDAP StartTLS refused: operationsError`. *Amended by BL-341:* the no-certificate refusal is decision 3's unknown extended operation and writes no `StartTLS` note |
| Bytes pipelined after `StartTLS` discarded (decision 5) | `Discarded <n> bytes sent after StartTLS`, the mail servers' wording, only when `<n>` is more than 0. *Amended by BL-341* |
| A Notice of Disconnection | `LDAP Notice of Disconnection: <code name>: <diagnostic>` |
| A connection that closes before its first message | nothing beyond the engine's info line: it is curl's own connect (measured) |

### 8. Help and `--aihelp`

The category is curl's own `ldap`, `LDAP protocol` (ADR-0034 decision 1), claiming the schemes
`ldap` and `ldaps`, and holds every option the server reads: `--directory`, `--max-message`,
`--head-timeout`, `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth`,
`--auth`, `--keytab`, `--cert`, `--key`, `--self-signed`, `--cacert`. No new option. The
`--aihelp` topic is `ldap`; its prose says the Windows curl's binds need `--allow-plaintext-auth`
(simple) or `--auth ntlm`, `negotiate` or `digest-md5` with an account, that the directory is
`<path>/.surl/ldap/directory.ldif`, and that Windows curl cannot use `ldaps` against an untrusted
certificate. BL-310 adds them and grows the pinned topic lists, as root `CLAUDE.md` requires.

### 9. The codec stays in `Surl.Protocol.Ldap`

BL-289's codec on the BCL's `System.Formats.Asn1` (BER rules) is kept: it read every message
measured here, the four-byte lengths included. No hand-built BER library is planned. The frame
reader gains the security-layer buffer (decision 4) in BL-309.

### 10. What BL-311 proves with the pinned Windows build

Against `surl` over loopback through the conformance harness; `P` is a temporary `--directory`
whose `.surl/ldap/directory.ldif` holds `dc=example,dc=com` (`objectClass: domain`), `E`, a
second person `cn=bob,dc=example,dc=com` (`sn: Jones`, `mail: bob@other.example`, `description:
café`) and 10001 entries under `ou=many,dc=example,dc=com`, each with an `objectClass`
(`WinLDAP`'s default filter is `(ObjectClass=*)`, so curl sends it for a URL with none, and
surl rightly returns no entry that lacks one); `A` is `--user alice:secret`; `U` is
`ldap://127.0.0.1:<p>/dc=example,dc=com`.

| surl options | curl command line | Exit, and what is checked |
| --- | --- | --- |
| `-d P A --allow-plaintext-auth` | `curl -sS -u alice:secret U` | 0, stdout the base entry in curl's layout |
| the same | `curl -sS -u alice:secret "U?cn,mail?one"` | 0, the two people, `cn` and `mail` only, then `DN: ou=many,dc=example,dc=com` alone (a one-level child with neither attribute prints its DN line only) |
| the same | `curl -sS -u alice:secret "U?cn?sub?(&(objectClass=person)(\|(cn=al*)(sn>=K))(!(mail=*@other.example)))"` | 0, alice only |
| the same | `curl -sS -u alice:secret "U?description?sub?(cn=bob)"` | 0, `\tdescription:: Y2Fmw6k=` |
| the same | `curl -sS -u alice:secret "U??sub?(cn=nobody)"` | 0, empty stdout |
| the same | `curl -sS -u alice:secret ldap://127.0.0.1:<p>/dc=nowhere` | 39, `No Such Object` |
| the same | `curl -sS -u alice:secret "ldap://127.0.0.1:<p>/ou=many,dc=example,dc=com??one"` | 0, 10000 entries (`sizeLimitExceeded` tolerated) |
| the same | `curl -sS -u alice:wrong U` | 38, `Invalid Credentials` |
| `-d P --allow-plaintext-auth` (no accounts) | `curl -sS -u alice:secret U` | 38, `Invalid Credentials` |
| `-d P A` | `curl -sS -u alice:secret U` | 38, `Confidentiality Required` |
| `-d P --allow-anonymous` | `curl -sS -u anyone:anything U` | 0, the base entry |
| `-d P A --auth ntlm` | `curl -sS --ntlm -u alice:secret U` | 0, the base entry (Sicily, sealed) |
| `-d P A --auth negotiate` | `curl -sS --negotiate -u alice:secret U` | 0, the base entry (`GSS-SPNEGO`, sealed) |
| `-d P A --auth digest-md5` | `curl -sS --digest -u alice:secret U` | 0, the base entry (`DIGEST-MD5` `auth-conf`, 3DES) |
| `-d P A --auth ntlm` | `curl -sS --ntlm -u alice:wrong U` | 38, `Invalid Credentials` |
| `-d P A` (`ntlm` not accepted) | `curl -sS --ntlm -u alice:secret U` | 38, `Authentication Method Not Supported` |
| `-d P A --auth ntlm,negotiate` | `curl -sS U` (no `-u`: the Windows user, who has no account) | 38, `Invalid Credentials` |
| `-d P --self-signed A` | `curl -sS -k -u alice:secret ldaps://127.0.0.1:<p>/dc=example,dc=com` | 38, `Server Down` (decision 5) |
| `-d P --self-signed A` | `curl -sS -u alice:secret ldaps://...` | 60 |
| `-d P A` | `curl -sS --ssl-reqd -u alice:secret U` | 4, `explicit TLS not supported` |
| (no `-d`) `A --allow-plaintext-auth` | `curl -sS -u alice:secret U` | 39, `No Such Object` (the empty in-memory directory) |

Plus, fast, in `Surl.Console.UnitTests` (BL-310): a malformed `directory.ldif` ends surl with 37
and decision 1's text before any listener binds.

Four rows were first written by decision rather than measurement; BL-311 measured all four with
the pinned Windows build against a live surl (`UpstreamCurlSearchesSurlOverLdapTests` in
`Surl.Conformance.UnitTests`), and the table above holds the measured answers:

- `--ntlm` when `ntlm` is not accepted: exit 38 with `Authentication Method Not Supported`,
  `WinLDAP`'s wording for result code 7. This ADR first wrote `Auth Method Not Supported`.
- The three sealed binds, `--ntlm` (Sicily), `--negotiate` (`GSS-SPNEGO`) and `--digest`
  (`DIGEST-MD5` `auth-conf`), each exit 0 with the base entry: `WinLDAP` completes all three
  security layers against surl.

Two more things measured that the first table did not say: the `?cn,mail?one` search also
returns `ou=many,dc=example,dc=com`, which has neither attribute, so curl prints its `DN:` line
alone (`DN: ou=many,dc=example,dc=com`); and the `ou=many` entries need an `objectClass`, because
`WinLDAP`'s default filter is `(ObjectClass=*)`. Neither changed the server.

### 11. Who builds what

| Task | Builds |
| --- | --- |
| BL-306 | decision 1's entries, DNs, matching rules, filters, selection, root DSE, bounds |
| BL-307 | decision 1's LDIF loading and its malformed-file texts |
| BL-308 | decisions 2 and 3, simple binds, limits (decision 6), notes (decision 7); until BL-309 lands, every Sicily and SASL bind is answered `authMethodNotSupported` (7) and the root DSE lists no `supportedSASLMechanisms` |
| BL-328 | decision 4's contract: the protocol-neutral rename, `ISaslAuthenticationPolicy`, `ISaslSecurityLayer` |
| BL-329 | NTLM sealing and signing, the LDAP NTLM challenge, the `NTLM` and `GSS-SPNEGO` exchanges for LDAP |
| BL-326 | `DIGEST-MD5`'s LDAP challenge, `rspauth` as final data, `auth-int` and `auth-conf` |
| BL-309 | decisions 4 and 5 in the server: Sicily, SASL binds, the security-layer framing, `StartTLS`, `ldaps` |
| BL-310 | decision 8, the directory's loading at start, registration |
| BL-311 | decision 10 |
| BL-327 | Kerberos inside `GSS-SPNEGO` (Low) |

## Alternatives considered

- **Accept NTLM, Negotiate and Digest binds without a security layer.** Rejected by measurement:
  `WinLDAP` seals every message after an NTLM bind even when the challenge grants no sealing, and
  will not finish a `DIGEST-MD5` bind without `auth-conf`; a server without the layer serves
  none of curl's three non-simple binds.
- **Refuse version 2 binds with `protocolError`.** Rejected: curl reports the retry's code, so
  the real refusal (`Invalid Credentials`, `Confidentiality Required`) would read `Protocol
  Error`.
- **Answer searches before a bind when no accounts are configured**, like HTTP's anonymous reads.
  Rejected: ADR-0032 lists HTTP, Gopher and TFTP as the anonymous protocols; LDAP has a login,
  and `--allow-anonymous` opens it.
- **A writable directory** (Add, Modify, Delete persisted to the LDIF file). Rejected for now:
  upstream curl only searches, so nothing curl does needs it, and a writable directory would need
  a write path, write access rules and a format that round-trips. A later task may add it.
- **A seed-file option for in-memory mode.** Rejected: a new option for what `--directory` already
  does; ADR-0031 has in-memory mode start empty.
- **A schema.** Rejected: curl's searches need matching rules, which decision 1's table gives by
  type; a schema would add checking that no curl request exercises.
- **Keep the SASL contract mail-named and use it from LDAP as is.** Rejected: `MailLoginStep` in
  the LDAP server would say something false (root `CLAUDE.md`, "Say what it does"), and the
  contract needs the security layer anyway.

## Consequences

- `Record-CurlExchange.ps1` gains `-Ldap`, `-LdapEntry`, `-LdapReply`, `-LdapIdleMilliseconds` and
  `-CurlTimeoutMilliseconds` (its help), used again by BL-308, BL-309 and BL-311 to record their
  fixtures.
- BL-309 depends on BL-328, BL-329 and BL-326; BL-328 touches `Surl.Protocol.Abstractions`,
  `Surl.Authentication` and the three mail servers, so it runs apart from them.
- The Windows build's `ldaps` cannot succeed against surl without trusting its certificate on the
  machine; the success case waits for BL-312.
- The measured case list above is the fixture list BL-308 and BL-309 record again as test data.

## Amendment 1 - Kerberos inside GSS-SPNEGO, measured and built (BL-327, 2026-09-30)

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-327. Amends decision 4's Kerberos line only.

### What was measured

With the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, curl 8.21.0, `WinLDAP`),
ADR-0065's test KDC and BL-265's `SURL.TEST` realm mapping, on the lane machine, through
`Record-CurlExchange.ps1 -Ldap -KerberosTestKdc` with `--negotiate -u
tester@SURL.TEST:<password>`:

- **No machine change is needed.** `ldap://localhost:<port>/...` reaches Kerberos: `WinLDAP`
  resolves `localhost` itself and asks the KDC for the service ticket
  `ldap/<the machine's host name>:<port>@SURL.TEST` (here `ldap/Stewart-Rogers-AI-PC:18389`) - the
  canonical host name, **with the port** - in the realm of the user's principal, so no hosts-file
  entry and no host-to-realm mapping is involved. `ldap://127.0.0.1` still never asks (decision
  4's measurement stands).
- **The bind:** after the root DSE search, one SASL `GSS-SPNEGO` bind whose credentials are an
  RFC 4178 `NegTokenInit` listing `1.2.840.48018.1.2.2` (MS-KRB5), `1.2.840.113554.1.2.2`
  (Kerberos), `1.3.6.1.4.1.311.2.2.30` (NEGOEX) and NTLMSSP, in that order, with the AP-REQ as its
  optimistic token, `ap-options` `mutual-required`, and the GSS-API checksum flags asking for
  confidentiality and integrity; no `mechListMIC`.
- **The answer it accepts:** `bindResponse success` with `serverSaslCreds` an `accept-completed`
  `negTokenResp` naming MS-KRB5 as `supportedMech` and the AP-REP token as `responseToken`, and no
  `mechListMIC`: `WinLDAP` sends nothing more for the bind. **There is no RFC 4752 section 3.3
  layer negotiation** (no wrapped 4-byte offer either way), unlike SASL `GSSAPI`: the GSS-API flags
  alone choose the layer.
- **The layer:** every message after the bind is a SASL buffer (4-byte length) holding one RFC
  4121 wrap token, **sealed** (`Flags` `02`), `EC` 0, `RRC` 28, the client's sequence numbers
  from the authenticator's. Surl's replies sealed the same way (`SentByAcceptor`, `Sealed`, `EC` 0,
  `RRC` 28, sequence numbers from the AP-REP's) were read: curl printed the entry, sent its unbind
  sealed, and **exited 0**. The recording is `Surl.Authentication.UnitTests/Fixtures/ldap-kerberos-sealed`.

To answer the bind while recording, `Record-CurlExchange.ps1` gained `-LdapKerberosAcceptor`,
which hands the bind and each buffer to `Run-KerberosAcceptor.cs` (a C# file-based app over
`Surl.Kerberos` and the keytab the test KDC wrote), and the KDC now starts before the recorder's
listener, since a child process that inherited the listening socket kept the recording from
ever finishing.

### Decision

- **A `GSS-SPNEGO` token selecting Kerberos is accepted with `--keytab`** (`GssSpnegoSaslExchange`
  in `Surl.Authentication`): the selection, the "Kerberos first with its AP-REQ as the optimistic
  token, else refused" rule, the `mechListMIC` both ways and the account match are ADR-0057
  decision 8 and ADR-0064's for HTTP Negotiate, with the service **`ldap`**, any host and port
  the keytab holds a key for. One leg: success is `bindResponse success` with the
  `accept-completed` `negTokenResp` (the AP-REP when mutual authentication was asked) as
  `serverSaslCreds`. A refused ticket notes `Kerberos: <reason>` and names no user; every later
  refusal names the principal.
- **`--allow-anonymous` accepts an unchecked Kerberos bind** (the ticket must still decrypt):
  unlike NTLM and `DIGEST-MD5`, the layer's keys come from the ticket, not the account's password.
- **The security layer is RFC 4121's wrap token** (`KerberosSaslSecurityLayer`): surl seals
  (`KerberosSecurityContext.Seal`: `EC` 0, `RRC` 28, as Windows sends) when the client asked for
  confidentiality, only signs when it asked for integrity alone, and runs no layer when it asked
  for neither; it reads the client's tokens sealed or signed with any `RRC`, in sequence.
- **Without `--keytab`** a token selecting Kerberos is answered as before, by ADR-0040's rule
  with NTLM selected.

### Alternatives considered

- **A hosts-file entry or a `ksetup /addhosttorealmmap` for a `.surl.test` name** (the task's
  suggestion). Not needed: `localhost` reaches Kerberos with the user's realm, and a machine
  change is Stewart's to make.
- **RFC 4752's layer negotiation after the bind**, as SASL `GSSAPI` has it. Rejected: measured,
  `WinLDAP` neither sends nor waits for it, and Active Directory's `GSS-SPNEGO` has none.
- **`RRC` 0 on surl's sealed tokens**, RFC 4121's simplest. Not chosen: `RRC` 28 is what
  Windows' own Kerberos sends and was measured to work; RFC 4121 section 4.2.5 obliges every
  receiver to accept either.
