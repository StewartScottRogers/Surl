# ADR-0057 — Surl's Kerberos: a keytab, a hand-built AP-REQ check, and how Negotiate and SASL `GSSAPI` use it

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-217 (FR-046).
- **Amended by:** Amendment 1 below (BL-268): decisions 8 and 9 measured against the test KDC
  of [ADR-0065](ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md);
  both hold. Amendment 2 below (BL-351): decision 2's table gains `ldap`.
- **Amends:** [ADR-0040](ADR-0040-http-negotiate-carrying-ntlm-bare-or-in-spnego.md) decision 3
  (a Kerberos token inside Negotiate is no longer refused once a keytab is configured, decision 8
  below); [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  section 11 (Kerberos lands as decided here); [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md)
  decision 3's table gains `Surl.Kerberos.UnitLibrary` (decision 6).
  [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) decision 4 said this task
  decides Kerberos; wherever ADR-0049 says "BL-210" it means BL-217, this ADR's task.

## Context

Negotiate (RFC 4559) carries NTLM today (ADR-0040, ADR-0042), and ADR-0032 section 11 made
Kerberos inside it later work, built by hand. ADR-0049 decision 4 made SASL `GSSAPI`
(RFC 4752) Kerberos V5 too, refused as not available until this ADR and BL-218 land. Both need
the same piece: an acceptor that holds a service key and checks a client's Kerberos AP-REQ
(RFC 4120 section 3.2), wrapped as a GSS-API token (RFC 4121, RFC 1964 section 1.1). The BCL
has AES, HMAC-SHA1/SHA256/SHA384 and `System.Formats.Asn1`, but no Kerberos, no RFC 3961
key derivation and no AES-CTS; the root `CLAUDE.md` has those built by hand, in their own
library, never taken from a package.

### What upstream curl 8.21.0 does (measured)

Measured 2026-09-30 on the lane machine (Windows 11 10.0.26200, not in a domain, no KDC
reachable) with `Record-CurlExchange.ps1`, which needed no extension:

- **The Windows reference build**, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (features include `Kerberos`, `SPNEGO`, `SSPI`):
  - `-Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH GSSAPI PLAIN'`,
    `-u user@EXAMPLE.COM:secret`: curl sends `AUTH GSSAPI` with no initial response; answered
    `334 ` (`-SaslChallenge 'GSSAPI=334 '`) it sends nothing more and exits
    `curl: (94) An authentication function returned an error`. Answered `235` at once, it exits
    `curl: (67) Login denied`.
  - The same with `--sasl-ir`: curl sends no `AUTH` at all and exits 94: SSPI's Kerberos
    package cannot make the initial token without a KDC, and curl fails before sending.
  - HTTP, every request answered `401` with `WWW-Authenticate: Negotiate`,
    `--negotiate -u user@EXAMPLE.COM:secret`: `InitializeSecurityContext failed:
    SEC_E_NO_CREDENTIALS (0x8009030e)` before each request, no `Authorization` sent, exit 0
    (ADR-0040 measured the same for other user names).
- **The unpatched 8.21.0 build** (ADR-0030, ADR-0042),
  `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256
  `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, used here only for HTTP
  Negotiate as ADR-0042 allows:
  - HTTP, `--negotiate -u user@EXAMPLE.COM:secret`: Negotiate finds no KDC for `EXAMPLE.COM`
    and falls back to bare NTLM:
    `Authorization: Negotiate TlRMTVNTUAABAAAAt4II4gAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`, no
    SPNEGO and no Kerberos mechanism offered.
  - For comparison only (SMTP is outside ADR-0042's use of this build), SASL `GSSAPI` fails
    exactly as the reference build's does: `AUTH GSSAPI`, `334 `, exit 94.
- The Linux and macOS reference builds list neither `Kerberos` nor `SPNEGO` nor `GSS-API`
  (`UpstreamCurlBuilds.json`), so they never send `AUTH GSSAPI` or a Negotiate token.

So **no pinned build can make a Kerberos token without a KDC**, and none sent one here. Every
Kerberos byte below is therefore decided from the RFCs and from upstream curl 8.21.0's own
source at the tag `curl-8_21_0`, read on 2026-09-30:

- `lib/curl_sasl.c`: `GSSAPI` is chosen with `sasl->mutual_auth = FALSE`; the service is
  `--service-name` when given, else the protocol's (`smtp`, `imap`, `pop`, the same names the
  measured `DIGEST-MD5` `digest-uri` and NTLM SPNs carry, ADR-0049); the host is the URL's.
  After the AP-REQ, with mutual authentication off, curl reads the server's next challenge as
  the wrapped security-layer offer at once; with it on, it reads an AP-REP first, then the offer
  after an empty response. The authorization identity it sends is `--sasl-authzid`, empty when
  not given.
- `lib/vauth/krb5_sspi.c`: the SSPI package is `Kerberos` (not `Negotiate`), so the token is a
  bare RFC 1964 `InitialContextToken`, not SPNEGO; the SPN is `<service>/<host>`
  (`Curl_auth_build_spn`); `ISC_REQ_MUTUAL_AUTH` only when mutual authentication is asked,
  which SASL never does. The server's security message must unwrap to exactly 4 bytes whose
  first byte has the no-security-layer bit (`KERB_WRAP_NO_ENCRYPT`) set; curl answers with
  that one layer, a maximum size of 0 and the authorization identity, wrapped by
  `EncryptMessage(..., KERB_WRAP_NO_ENCRYPT, ...)`: an RFC 4121 wrap token without
  confidentiality.
- HTTP Negotiate on SSPI (ADR-0040): the SPN is `HTTP/<host>`, the package `Negotiate`, so a
  Windows client's token is SPNEGO whose `mechTypes` lead with Microsoft's Kerberos OID
  `1.2.840.48018.1.2.2`, then `1.2.840.113554.1.2.2`, with the Kerberos AP-REQ as the
  optimistic token.

## Decision

### 1. The service key comes from a keytab, and only from a keytab

- **`--keytab <file>`** names an MIT-format keytab file (the format `ktutil`, `kadmin ktadd`,
  Heimdal's `ktutil` and Windows' `ktpass /out` write): the two-byte version `0x05 0x02`, then
  length-prefixed big-endian entries of principal (realm and components), timestamp, 8-bit key
  version number (overridden by the optional trailing 32-bit one when it is present and not 0),
  enctype and key. A negative length marks a deleted entry, which is skipped. Version `0x0501`
  (native byte order) is refused as malformed.
- **No key derived from a password on the command line.** RFC 3962's string-to-key needs the
  principal's salt, which on Active Directory is not the default one, and it would put a
  service secret in the process list and the shell history. Every KDC exports a keytab, and a
  keytab carries the key version number and every enctype at once. `Surl.Kerberos` has no
  string-to-key at all (decision 6); tests use fixed keys (decision 11).
- `--keytab` is a `<file>` (ADR-0007 section 2): an empty argument is refused while parsing,
  last value wins, not negatable. The file is read when surl starts serving, before any listener
  binds, as `--user-file` is (ADR-0032 section 2):

  | Case | Exit code | Text after `surl: ` |
  | --- | --- | --- |
  | Missing, a directory, or unreadable | `CouldNotReadFile` (37) | `(37) Could not read keytab <path>` |
  | Not a version `0x0502` keytab, or an entry running past the end | `FailedInit` (2) | `(2) Keytab <path> is malformed at byte <offset>` |
  | No entry with an enctype of decision 3 | `FailedInit` (2) | `(2) Keytab <path> holds no key surl can use` |

  Entries with other enctypes (DES, `rc4-hmac`, Camellia) are skipped, each with the warning
  line `surl: warning: --keytab: skipped the <enctype name> key of <principal>` (ADR-0032
  section 9's form). No key byte is ever logged, at any log level.
- **Consistency**, checked after the whole command line is read: `--auth` naming `gssapi`
  without `--keytab` is refused, `FailedInit`, `surl: (2) --auth gssapi needs --keytab`, since
  a server that offered `GSSAPI` without a key would break every curl login that picks it
  (ADR-0049 decision 4, exit 94). `--auth negotiate` without `--keytab` stays ADR-0040's
  NTLM-only Negotiate. `--keytab` when `--auth` names neither word is accepted, with the warning
  `surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi`.
- **Help and AI help** (ADR-0034, ADR-0046): `--keytab` sits in the `auth` category, described
  "Read Kerberos service keys from a keytab file"; its `OptionArgumentType` is the file type
  `--user-file` has; the `auth` `--aihelp` topic gains a paragraph saying that `negotiate`
  accepts Kerberos and `gssapi` works only with `--keytab`, and an example
  `surl --auth negotiate --keytab http.keytab --user-file users.txt http://0.0.0.0:8080/`.
  The manual describes the file format, the principals of decision 2 and the account mapping of
  decision 9.

### 2. The service principals, the host and the realm

- A ticket is accepted for a principal `<service>/<host>@<REALM>` when the keytab holds a key
  for that principal (decision 4) and `<service>` is the one the listen URL's scheme answers:

  | Schemes | Service |
  | --- | --- |
  | `http`, `https` | `HTTP` |
  | `smtp`, `smtps` | `smtp` |
  | `imap`, `imaps` | `imap` |
  | `pop3`, `pop3s` | `pop` |

  These are the names upstream curl asks for by default (its source, and the SPNs of
  ADR-0049's measured NTLM and `DIGEST-MD5` exchanges). The service and the realm are compared
  case-insensitively (ordinal), as Active Directory resolves SPNs; the rest ordinally. A client
  that sets `--service-name` to anything else gets a ticket for a principal surl does not
  answer, refused like a wrong key.
- **The host is whatever the keytab names.** A server listening on any address cannot know
  the name a client used (ADR-0049 decision 5 decided `digest-uri` the same way), and the
  keytab already says which hosts the operator holds keys for: a keytab with
  `HTTP/www.example.com@EXAMPLE.COM` and `HTTP/www@EXAMPLE.COM` answers both names. The ticket's
  server name travels in clear, so the key is looked up by it; the ticket is then checked under
  that key.
- **The realm is whatever the keytab names**, the same way. There is no realm option.

### 3. The enctypes

| Enctype | Number | Specification | Accepted |
| --- | --- | --- | --- |
| `aes256-cts-hmac-sha1-96` | 18 | RFC 3962 | yes |
| `aes128-cts-hmac-sha1-96` | 17 | RFC 3962 | yes |
| `aes128-cts-hmac-sha256-128` | 19 | RFC 8009 | yes |
| `aes256-cts-hmac-sha384-192` | 20 | RFC 8009 | yes |
| `rc4-hmac` | 23 | RFC 4757 | **no** |
| DES, 3DES, Camellia | 1, 3, 16, 25, 26 ... | RFC 3961, RFC 6803 | no |

- The four AES enctypes are the ones every current KDC issues: Active Directory since Windows
  Server 2008 domain functional level, MIT and Heimdal by default.
- **`rc4-hmac` is not accepted.** RFC 8429 deprecates it (it SHOULD NOT be used), Windows is
  removing it from Kerberos by default, and its key is the account's unsalted NT hash, so a
  keytab holding it holds a password equivalent. It is refused for its weakness, not for its
  difficulty; accepting it would need an ADR that decides a loosening option for tests, as
  ADR-0051 did for SSH's weak algorithms. DES and 3DES are deprecated by RFC 6649 and RFC 8429;
  Camellia is rare and no measured client of this project would send it.
- The ticket's encryption and its session key may use different enctypes; both must be in
  the table. A subkey in the authenticator must be too.

### 4. Checking the AP-REQ

Everything is read with `System.Formats.Asn1` under DER rules (ADR-0040 decision 3); malformed
DER, trailing bytes and a missing required field are refusals, never exceptions (ADR-0040
decision 6). In order:

1. **The GSS-API framing** (RFC 1964 section 1.1, RFC 4121 section 4.1): `[APPLICATION 0]`,
   the mechanism OID `1.2.840.113554.1.2.2` (or, inside SPNEGO only, `1.2.840.48018.1.2.2`,
   decision 8), the two-byte `TOK_ID` `01 00`, then a `KRB_AP_REQ` (RFC 4120 section 5.5.1)
   with `pvno` 5 and `msg-type` 14.
2. **The options.** `use-session-key` (user-to-user) is refused: surl has no ticket-granting
   ticket. `mutual-required` decides the AP-REP (decision 5).
3. **The key.** The ticket's `sname` and `realm` choose the keytab entries (decision 2); among
   them, the one whose enctype is the ticket's `enc-part.etype` and whose key version number is
   its `kvno`, or the highest for that enctype when `kvno` is absent. None is a refusal.
4. **The ticket** is decrypted with that key, key usage 2 (RFC 4120 section 7.5.1), and the
   integrity check must pass. In the `EncTicketPart`: the `invalid` flag is refused; the time
   now must lie in `[starttime - skew, endtime + skew]` (`authtime` when `starttime` is absent);
   `caddr`, `transited` and `authorization-data` (a Windows PAC included) are not checked:
   tickets are addressless behind NAT, surl trusts only the one realm whose key it holds, and it
   grants nothing a PAC would restrict.
5. **The authenticator** is decrypted with the ticket's session key, key usage 11. Its `crealm`
   and `cname` must equal the ticket's (ordinal); `ctime`/`cusec` must be within the skew of now;
   its `cksum` must be the GSS-API checksum, type `0x8003` (RFC 4121 section 4.1.1): at least 24
   bytes, `Lgth` 16. The channel bindings (`Bnd`) are not checked (surl's HTTPS offers no
   channel-binding contract yet, and ADR-0039 does not check NTLM's either); the flags are read
   for `GSS_C_MUTUAL_FLAG` (2); a delegated credential (`GSS_C_DELEG_FLAG`, 1) is skipped and
   never used or stored.
6. **The replay cache** (decision 7) must not hold this authenticator.
7. The context key is the authenticator's `subkey` when present, else the ticket's session key
   (RFC 4121 section 2; surl sends no acceptor subkey). The context keeps the client's
   `seq-number` for the tokens of decision 5.

- **Clock skew is 300 seconds**, RFC 4120's usual value and Active Directory's and MIT's
  default, measured on the injected `TimeProvider` (root `CLAUDE.md`). It is not an option.
- **Every refusal is `RefusedCredentials`**, after ADR-0032 section 8's 1-second delay; surl
  sends no `KRB-ERROR` token (a `401` or the mail refusal of ADR-0049 decision 7 says enough,
  and curl ends with it). The reason goes to the verbose log only, in the words
  `Kerberos: <reason>` (`no key for <principal> <enctype> kvno <n>`, `ticket expired`,
  `clock skew`, `replayed authenticator`, `integrity check failed`, `malformed token`); key
  bytes and the decrypted parts are never written.
- Integrity checks and the checksum comparison use `CryptographicOperations.FixedTimeEquals`.

### 5. The AP-REP, and the tokens after it

- **An AP-REP is sent exactly when the AP-REQ's `ap-options` has `mutual-required`**
  (RFC 4120 section 3.2.4). Its `EncAPRepPart` holds the authenticator's `ctime` and `cusec`, no
  subkey, and a `seq-number` of four random bytes from the injected random source (ADR-0049
  decision 5), encrypted with the ticket's session key, key usage 12, and framed as an RFC 4121
  token: `[APPLICATION 0]`, the Kerberos OID, `TOK_ID` `02 00`, the `KRB_AP_REP`.
- **Wrap and MIC tokens** are RFC 4121 section 4.2's (`TOK_ID` `05 04` and `04 04`), with
  `SentByAcceptor` set on surl's and the key usages 22 (acceptor seal), 23 (acceptor sign), 24
  (initiator seal) and 25 (initiator sign). Surl's wrap tokens carry no confidentiality and a
  right rotation count of 0; tokens read from a client may have either confidentiality flag and
  any rotation count, which is undone before the check. Sequence numbers are checked to be the
  client's next one. RFC 1964's older token formats belong to DES and `rc4-hmac` and are not
  read.

### 6. The library: `Surl.Kerberos.UnitLibrary`

A new horizontal library, with its `Surl.Kerberos.UnitTests` beside it, held to the quality
gates, AOT-compatible, referencing nothing but the shared framework (`System.Formats.Asn1`,
`System.Security.Cryptography`). It joins ADR-0002 decision 3's table as a row referencing
nothing, and `Surl.Authentication.UnitLibrary` references it, as it references
`Surl.Cryptography` (ADR-0032 section 7); no protocol server needs it, since every login goes
through the policy contracts. Its public surface, in namespace `Surl.Kerberos`, one public type
per file:

```csharp
public sealed class KerberosKeytab                 // decision 1
{
    public static KerberosKeytabReadResult Read(ReadOnlySpan<byte> bytes);   // entries, skipped entries, or the malformed offset
    public IReadOnlyList<KerberosKeytabEntry> Entries { get; }
}
public sealed record KerberosKeytabEntry(KerberosPrincipalName Principal, uint KeyVersionNumber, KerberosEncryptionType EncryptionType, ReadOnlyMemory<byte> Key);
public sealed record KerberosPrincipalName(string Realm, IReadOnlyList<string> Components); // ToString(): RFC 1964 display form
public enum KerberosEncryptionType { Aes128CtsHmacSha196 = 17, Aes256CtsHmacSha196 = 18, Aes128CtsHmacSha256128 = 19, Aes256CtsHmacSha384192 = 20 }

public sealed class KerberosAcceptor(KerberosKeytab keytab, KerberosReplayCache replayCache, TimeProvider timeProvider, IKerberosRandomSource randomSource)
{
    // decisions 2 and 4: checks one RFC 1964 InitialContextToken for the given service.
    public KerberosAcceptResult Accept(ReadOnlySpan<byte> initialContextToken, string service);
}
public sealed record KerberosAcceptResult(KerberosSecurityContext? Context, string? RefusalReason);

public sealed class KerberosSecurityContext        // decision 5
{
    public KerberosPrincipalName ClientPrincipal { get; }
    public bool IsMutualAuthenticationRequested { get; }
    public byte[] CreateApRepToken();
    public byte[] Wrap(ReadOnlySpan<byte> message);               // no confidentiality
    public bool TryUnwrap(ReadOnlySpan<byte> token, out byte[] message);
    public byte[] GetMic(ReadOnlySpan<byte> message);
    public bool VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token);
}

public sealed class KerberosReplayCache(TimeProvider timeProvider) { ... }   // decision 7
public interface IKerberosRandomSource { void Fill(Span<byte> destination); }
```

Inside (internal): the RFC 3961 simplified profile (`n-fold`, `DK`, `DR`), RFC 3962's
AES-CTS with HMAC-SHA1-96, RFC 8009's `KDF-HMAC-SHA2` with HMAC-SHA256-128 and
HMAC-SHA384-192, CTS over the BCL's `Aes.EncryptCbc`/`DecryptCbc`, the four checksum types (15,
16, 19, 20), and the DER types of RFC 4120 section 5. The implementing task may rename members
where the code shows a truer name; the split and the responsibilities stand.

### 7. The replay cache

One per surl process, shared by every listener, holding the SHA-256 of each accepted
authenticator's cipher text (MIT's hash-of-ciphertext approach, which also catches a replay
re-encrypted under nothing new) with the authenticator's `ctime`. An entry is dropped once
`ctime + skew` has passed on the `TimeProvider`, since an older authenticator fails the skew
check anyway. It holds at most 65536 entries; when full, a new AP-REQ is refused
(`replay cache full` in the verbose log) rather than evicting an entry, which would let a
replay through. 65536 authenticators in a 300-second window is over 200 logins a second,
well past ADR-0006's connection limits.

### 8. Negotiate (HTTP) with Kerberos

With `--keytab` given and `negotiate` accepted, `NegotiateAuthenticationMethod` (ADR-0040)
changes as follows; without `--keytab` it stays exactly as ADR-0040 decides.

- **SPNEGO**: the first `NegTokenInit` mechanism that surl supports is selected: Kerberos
  (either OID) or NTLM. When Kerberos is selected, the optimistic token must be its AP-REQ; a
  `NegTokenInit` naming Kerberos first with no optimistic token is refused (every measured
  SSPI and GSS-API client sends one). There is no fallback to NTLM after a refused ticket: the
  client chose Kerberos.
- The reply echoes the OID as the client listed it (Windows lists
  `1.2.840.48018.1.2.2`, and expects that one back, MS-SPNG section 3.3.5.1):
  `negTokenResp { negState accept-completed, supportedMech <oid>, responseToken <AP-REP
  token when mutual-required> }`, sent on the accepted response as
  `WWW-Authenticate: Negotiate <base64>` (RFC 4559 section 5; ADR-0032 section 6's final token).
- **`mechListMIC`**: when the client's `NegTokenInit` carries one, it is checked as a Kerberos
  MIC over the DER of `mechTypes` (key usage 25) and a refusal fails the login; surl then sends
  its own MIC over the same bytes (key usage 23). When the client sends none, surl sends none:
  Kerberos was the client's first choice, which RFC 4178 section 5 lets the exchange complete
  without MICs.
- **A bare Kerberos token** (`[APPLICATION 0]` with the Kerberos OID, not SPNEGO) after
  `Negotiate` is accepted too, answered with the bare AP-REP token when mutual authentication was
  asked, else no final token: ADR-0040 accepts bare NTLM for the same reason, and MIT and
  Heimdal clients configured without SPNEGO send one.
- One leg: Kerberos completes on the first request; the connection remembers the login as
  ADR-0044 decides for Negotiate. A token's AP-REQ refused is `401` with every challenge again,
  after the delay, as every refusal is.

### 9. SASL `GSSAPI` (RFC 4752)

`GSSAPI` is offered first (ADR-0049 decision 2) when `gssapi` is accepted, on every connection
(it sends no clear secret, so TLS does not matter); decision 1 makes `--keytab` a precondition.

1. **The initial token**: the `AUTH GSSAPI` initial response, or the response to one empty
   challenge when there was none (curl without `--sasl-ir`). It is an RFC 1964
   `InitialContextToken`, checked by decision 4 for the service of decision 2.
2. **When `mutual-required` was set**, the next challenge is the AP-REP token, and the client's
   answer must be empty (RFC 4752 section 3.1); anything else is refused.
3. **The security-layer offer**: a challenge holding the wrap token of the 4 bytes
   `01 00 00 00`: the no-security-layer bit alone and a maximum message size of 0, since surl
   offers no integrity or confidentiality layer. curl's SSPI code requires exactly 4 bytes with
   that bit, so the offer it accepts is this one.
4. **The client's choice**: its response must unwrap (decision 5) to at least 4 bytes whose first
   is `01` (any other layer, or several bits, is refused); the next 3 bytes are ignored; the rest
   is the authorization identity, UTF-8.
5. **The account** (decision 10): the identity must be empty (curl without `--sasl-authzid`) or
   equal, ordinally, to the account name the ticket maps to; else `RefusedCredentials`.
6. The final step is `Accepted` (`AcceptedUnchecked` under `--allow-anonymous`).

Every step runs under `--allow-anonymous` too, since the AP-REP and the wrap tokens need the
decrypted session key; `--allow-anonymous` only skips the account match of decision 10. A
ticket that cannot be decrypted is refused even then: nothing can be answered without its key.

### 10. The account, and the `CheckedLogin` user

- **The login is the ticket's client principal** in RFC 1964 display form: components joined
  with `/`, then `@` and the realm, with `/`, `@` and `\` inside a name escaped by `\`
  (`user@EXAMPLE.COM`, `host/web01@EXAMPLE.COM`). It is accepted when an account of exactly that
  name exists (ordinal), and its password is not used, as `EXTERNAL`'s certificate name is
  (ADR-0049 decision 4). The KDC vouches for the principal; the account list is what says the
  principal may log in to this server, so configuring a keytab never opens the server to a whole
  realm by itself. The same name curl's `-u user@EXAMPLE.COM:secret` picks is the one the
  account is written with.
- **The `CheckedLogin`** (ADR-0038): the method is `Negotiate` for HTTP (ADR-0038 section 4)
  and `GSSAPI` for SASL (ADR-0049 decision 7); the user is the display form above. It is set on
  the deciding step: `Accepted` or `RefusedCredentials` after the ticket decrypted. A token that
  never decrypted names no user (ADR-0038 section 6).

### 11. How CI tests it without a KDC

Every Kerberos test is a fast test, platform-neutral, with no KDC, no network and no
`TestCategory=Integration`:

- **The crypto** is pinned by the RFCs' published vectors: RFC 3961 appendix A.1 (`n-fold`),
  RFC 3962 appendix B (AES-CTS encryptions with the key `chicken teriyaki`, and the derived keys
  of its string-to-key cases taken as fixed keys), and RFC 8009 appendix A (key derivations,
  encryptions with their confounders, and checksums for enctypes 19 and 20).
- **Tickets and authenticators are made by hand**: the tests hold a fixed service key and a
  fixed session key, encode the `EncTicketPart`, the `Authenticator` and the `AP-REQ` as DER
  from RFC 4120 section 5 with the vector-checked encryption and a fixed confounder, and replay
  them through `KerberosAcceptor`. The same tokens, wrapped in SPNEGO as MS-SPNG lays them out,
  drive `NegotiateAuthenticationMethod`, and with RFC 4752's steps drive the `GSSAPI` exchange.
  Time is a fake `TimeProvider` (expiry and skew), randomness an injected source.
- **Keytabs** are byte arrays written out by hand from the MIT format, including a deleted
  entry, a 32-bit key version number, a skipped `rc4-hmac` entry and truncations.
- **What CI cannot show** is a real client's token. Proving `--negotiate` and SASL `GSSAPI` from
  pinned upstream curl end to end needs a KDC the client's SSPI trusts, and on Windows that
  means configuring the machine's realm (`ksetup`), which a CI runner and an unattended lane
  should not do. That proof is its own decision, filed as BL-242 below.

### 12. Who builds what

| Work | Task |
| --- | --- |
| `Surl.Kerberos` created, with the RFC 3961/3962/8009 enctype profiles and checksums (decisions 3, 6, 11), added to ADR-0002's table and the isolation test | BL-243 |
| The keytab, principal names, the AP-REQ check, AP-REP, wrap and MIC tokens and the replay cache in `Surl.Kerberos` (decisions 1 to 5, 7) | BL-239 |
| `--keytab`, its refusals, warnings, help, manual and AI help, the `gssapi` precondition, and the keytab composed into `Surl.Authentication`'s settings (decision 1) | BL-240 |
| Kerberos inside Negotiate (decision 8), and the `auth` help and manual sentence saying so | BL-241 |
| SASL `GSSAPI` (decision 9) | BL-218 |
| How Kerberos logins are proved against pinned upstream curl with a KDC (decision 11) | BL-242 |

## Alternatives considered

- **A password option with RFC 3962 string-to-key.** Rejected (decision 1): the salt is not
  knowable in general, and a service secret on the command line leaks. A keytab is what every
  KDC hands out.
- **Accepting `rc4-hmac`.** Rejected for now (decision 3); it would come back only as a named
  loosening in its own ADR.
- **A fixed service host or a `--kerberos-principal` option.** Rejected: surl listens on any
  address and the keytab already names the principals it holds; one more option would say the
  same thing twice.
- **Accepting any principal the KDC vouches for, without an account.** Rejected: a keytab would
  then open the server to a whole realm, against secure-by-default (ADR-0032); the account list
  decides who logs in for every other method.
- **Falling back to NTLM when a Kerberos optimistic token fails.** Rejected: the client chose
  Kerberos and gets a refusal like any bad credential; silently downgrading would hide the
  failure.
- **Evicting the oldest replay-cache entry when full.** Rejected (decision 7): it lets a replay
  through.
- **Sending a `KRB-ERROR` on refusal.** Rejected: curl exits on the protocol's refusal either
  way, and the error names which check failed.
- **Using the platform's GSS-API or SSPI.** Rejected: not BCL, not on every platform, and
  Kerberos is built by hand (root `CLAUDE.md`).

## Consequences

- `surl --auth negotiate --keytab http.keytab --user-file users.txt http://...` accepts a
  Kerberos ticket for `HTTP/<host>@<REALM>` from a domain-joined Windows client, curl's
  `--negotiate` included, and still accepts NTLM from clients that choose it.
- `surl --auth gssapi,cram-md5 --keytab mail.keytab ...` offers `GSSAPI` first on SMTP, IMAP and
  POP3, and curl's `-u user@EXAMPLE.COM:x` logs in with it once its machine can reach the KDC.
- On this machine, not in a domain, curl still sends no Kerberos token (measured above), so the
  end-to-end proof waits for BL-242.
- `Surl.Kerberos.UnitLibrary` and its tests join the solution; `Surl.Authentication` references
  it, and the product overview's "Layers" row says so (BL-243).

## Amendment 1 - decisions 8 and 9 measured against the test KDC (BL-268, 2026-09-30)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30, as
[ADR-0065](ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md)
decision 6 asks. **Both decisions hold; nothing in surl changes, and no task is filed.**

### How it was measured

On the lane machine (Windows 11 Pro 10.0.26200, a workgroup member) with BL-265's realm mapping
(`ksetup` shows `SURL.TEST: kdc = 127.0.0.1`), the hand-built test KDC of ADR-0065 decision 1
(`Run-KerberosTestKdc.cs`) serving `tester@SURL.TEST` and the service principals
`HTTP/web.surl.test`, `smtp/mail.surl.test`, `imap/mail.surl.test` and `pop/mail.surl.test`,
with these builds:

| Build | Path | SHA-256 | Version |
| --- | --- | --- | --- |
| Windows reference | `C:\Program Files\Git\mingw64\bin\curl.exe` | `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 8.21.0 |
| Unpatched static-curl, HTTP Negotiate only (ADR-0042) | `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe` | `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` | 8.21.0 |

- **The token alone**: `Record-CurlExchange.ps1 -KerberosTestKdc -KerberosPassword
  surl-test-password -KerberosServicePrincipal HTTP/web.surl.test` with the canned
  `401 WWW-Authenticate: Negotiate` and `-CurlArgs -sS --negotiate -u
  tester@SURL.TEST:surl-test-password --resolve web.surl.test:18080:127.0.0.1
  http://web.surl.test:18080/`: curl exits 0, `kdc.log` shows the AS exchange answered
  `KDC_ERR_PREAUTH_REQUIRED` (25), the AS exchange with pre-authentication, and the TGS exchange
  for `HTTP/web.surl.test`, each ticket `aes256-cts-hmac-sha1-96` (18).
- **The whole exchange**: the test KDC started once with all four principals, its
  `service.keytab` handed to four `surl --keytab service.keytab --user-file users.txt` (one
  account, `tester@SURL.TEST`), `--auth negotiate` on `http://127.0.0.1:18180/` and
  `--auth gssapi` on `smtp://127.0.0.1:18125/`, `imap://127.0.0.1:18143/` and
  `pop3://127.0.0.1:18110/`; then `Record-CurlExchange.ps1 -NoServer` ran 14 cases: HTTP
  `--negotiate` with each build, and for each of SMTP, IMAP and POP3 the reference build with
  `--login-options AUTH=GSSAPI -u tester@SURL.TEST:surl-test-password --resolve
  mail.surl.test:<port>:127.0.0.1`, with and without `--sasl-ir`, with and without
  `--sasl-authzid tester@SURL.TEST`. **Every case exits 0** and its login is accepted.
- Each token was taken from curl's `-v` output and decoded with the run's `service.keytab`: the
  ticket decrypted with the service key (key usage 2), the authenticator with the ticket's
  session key (key usage 11), and the client's wrap token read with the authenticator's subkey.

### HTTP Negotiate (decision 8)

| | Reference build | Static-curl build |
| --- | --- | --- |
| Sent | On the first request, unasked: with `--negotiate` the only method, curl picks it at once | The same |
| Outer token | SPNEGO `NegTokenInit` (`1.3.6.1.5.5.2`), 701 bytes | The same, 713 bytes |
| `mechTypes`, in order | `1.2.840.48018.1.2.2` (Microsoft Kerberos), `1.2.840.113554.1.2.2` (Kerberos), `1.3.6.1.4.1.311.2.2.30` (NEGOEX) | The same three, then `1.3.6.1.4.1.311.2.2.10` (NTLM) |
| `reqFlags` | none | none |
| Optimistic token | A Kerberos AP-REQ, framed `[APPLICATION 0]` with the **standard** OID `1.2.840.113554.1.2.2` (not the Microsoft one the list leads with), `TOK_ID` `01 00` | The same |
| `ap-options` | `20000000`: `mutual-required` set, `use-session-key` clear | The same |
| Authenticator checksum | type `0x8003`, 24 bytes: `Lgth` 16, zero channel bindings, flags `0x3E` (`MUTUAL`, `REPLAY`, `SEQUENCE`, `CONF`, `INTEG`; no `DELEG`) | The same |
| Authenticator | an aes256 subkey, a sequence number, and `AD-IF-RELEVANT` holding types 143 (`00 40 00 00`) and 144 (the target `HTTP/web.surl.test@SURL.TEST`, UTF-16LE) | The same |
| `mechListMIC` | **not sent** | **not sent** |
| surl's answer | `negTokenResp { negState accept-completed, supportedMech 1.2.840.48018.1.2.2, responseToken <AP-REP token, standard OID> }`, no `mechListMIC`; curl exits 0 with no complaint in `-v` | The same |

Decision 8 holds: Kerberos (under the Microsoft OID) is the first mechanism listed, so ADR-0064
decision 1's refusal of Kerberos-not-first never fires for either build; the optimistic token is
its AP-REQ; with no `mechListMIC` from the client, surl sends none; and the echoed Microsoft OID is
accepted. One detail decision 8 did not spell out is now measured: the AP-REQ inside the SPNEGO
token is framed with the standard Kerberos OID even though the list leads with Microsoft's, which
`KerberosAcceptor.AcceptInsideSpnego` already accepts (it takes either OID). Without a KDC the
static-curl build falls back to bare NTLM (Context above); with one it sends SPNEGO and Kerberos.

### SASL `GSSAPI` (decision 9)

Identical in every case except where the table says otherwise:

| | SMTP | IMAP | POP3 |
| --- | --- | --- | --- |
| How the initial token travels, without `--sasl-ir` | `AUTH GSSAPI`, `334 `, the token | `AUTHENTICATE GSSAPI <token>` (surl advertises `SASL-IR`, and curl's IMAP sends an initial response whenever the server does) | `AUTH GSSAPI`, `+ `, the token |
| With `--sasl-ir` | the same: the 852-character token would make the `AUTH` line longer than curl's 512-byte SMTP limit, so curl sends it after the `334` | the same | the same: over curl's 255-byte POP3 limit |

- **The initial token** is a bare RFC 1964 `InitialContextToken` (`[APPLICATION 0]`, OID
  `1.2.840.113554.1.2.2`, `TOK_ID` `01 00`, the AP-REQ), not SPNEGO: 638 bytes for `smtp` and
  `imap`, 635 for `pop`.
- **`mutual-required` is not set**: `ap-options` `00000000`, and the authenticator checksum
  (type `0x8003`, `Lgth` 16, zero channel bindings) has flags `0x00000000`. The authenticator
  carries an aes256 subkey and a sequence number. So no AP-REP is asked for, and curl reads surl's
  next challenge, the wrapped `01 00 00 00` offer, at once.
- **The client's answer** is an RFC 4121 wrap token, `TOK_ID` `05 04`, flags `00` (no
  confidentiality, no acceptor subkey), `EC` 12, **`RRC` 12** (Windows rotates the 12-byte
  checksum to the front, which surl undoes, decision 5), its sequence number the authenticator's.
  Unwrapped it is:
  - without `--sasl-authzid`: `01 00 00 00` - the no-security-layer bit alone, a maximum size of
    0 and an empty authorization identity;
  - with `--sasl-authzid tester@SURL.TEST`: `01 00 00 00` followed by `tester@SURL.TEST` in UTF-8.

Decision 9 holds at every step: the bare `InitialContextToken`, no mutual authentication, the
4-byte offer curl accepts, and a choice of `01` with the identity after the 3 size bytes, empty or
equal to the ticket's client principal. SMTP and POP3 never carry curl's `GSSAPI` token as an
initial response, because of the line limits above; surl's initial-response path for them is
still right for a client with a shorter token, and IMAP exercises it.

## Amendment 2 - decision 2's table gains `ldap` (BL-351, 2026-10-01)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-10-01, in
BL-351 (FR-052). Decision 2's table predates the LDAP server's SASL binds (ADR-0072), and
`GssapiSaslExchange` followed it: every scheme that was not SMTP or IMAP answered the service
`pop`, so SASL `GSSAPI` over `ldap` and `ldaps` refused every ticket (`Kerberos: no key for
ldap/<host>@<REALM>`). BL-351's conformance test found it: the OpenLDAP build of
[ADR-0078](ADR-0078-ldap-sasl-gssapi-measured-against-an-openldap-build-with-mit-kerberos.md),
holding a ticket for `ldap/ldap.surl.test@SURL.TEST` from the test KDC, ended 67 against
`surl --auth gssapi --keytab`. The table gains a row:

| Schemes | Service |
| --- | --- |
| `ldap`, `ldaps` | `ldap` |

`ldap` is the service upstream curl asks for (ADR-0078 decision 1 measured its TGS request for
`ldap/ldap.surl.test`), and the one `GSS-SPNEGO` already answered (ADR-0072 Amendment 1). With it,
the same test exits 0 with the entry on stdout.
