# ADR-0149 — How an LDAP directory serves as an optional account source

- **Status:** Accepted
- **Date:** 2026-11-11
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11, in
  BL-671. Stewart approved LDAP as an account source on 2026-11-04 as in scope, lowest priority, last;
  the design was left to Claude.
- **Builds on:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (accounts, methods, section 8's password checks and refusal delay, section 9's warnings),
  [ADR-0072](ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md)
  (the LDAP server and its directory, left unchanged here),
  [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md) decision 2
  (`IOriginConnector`, BL-551's outbound connections),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 4 (one account set; 4.3 names this task),
  [ADR-0101](ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md)
  (the item grammar, bounds and help),
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 4 (groups and scheme views),
  [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md),
  [ADR-0128](ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md),
  [ADR-0132](ADR-0132-how-the-account-book-changes-while-the-server-runs.md),
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (bounds) and
  [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3 (reference rules).
- **Amends:** ADR-0102 decision 4.1 (a group or access-row member naming no configured account is
  accepted as a directory name when `--user-ldap` is given, decision 8).
- **Acted on by:** BL-672 (the check in `Surl.Authentication`), BL-1068 (the TLS client connection in
  `Surl.Networking`), BL-673 (the option and its composition).

## Context

`surl` takes accounts from `--user`, `--user-file` and `--mailbox` (ADR-0032, ADR-0102). A test or a
team that already keeps its accounts in an LDAP directory (OpenLDAP, Active Directory, 389 Directory
Server) would rather point `surl` at it than copy the accounts into a file. BL-671 weighs two
readings of "an LDAP directory serves as an account source":

1. **An external directory.** `surl` connects to it as an LDAP client and checks a login by binding
   with the user's name and password (RFC 4511 section 4.2, RFC 4513 section 5.1.3).
2. **Surl's own directory.** The LDIF file the LDAP server serves (`<path>/.surl/ldap/directory.ldif`,
   ADR-0072 decision 1) becomes an account source through its `userPassword` attributes.

Constraints the decision must keep: ADR-0032 section 8 (a peer sees no difference between no such user,
a wrong password and no accounts; the one-second refusal delay); base class library only; native AOT;
only `Surl.Networking` constructs a `Socket` or an `SslStream` (root `CLAUDE.md`); `Surl.Authentication`
may not reference `Surl.Protocol.Ldap` (ADR-0002 decision 3); time on the injected `TimeProvider`;
everything bounded (ADR-0006); the directory's own credentials never echoed.

No upstream curl measurement applies: upstream curl is not a party to this exchange. `surl` is the LDAP
*client* here, talking to a directory the operator runs; curl only sends its login to `surl` by the
methods it already uses, whose bytes the existing ADRs pinned. Every wire choice below therefore cites
RFC 4511 (the protocol), RFC 4513 (authentication and TLS), RFC 4515 (filters) or RFC 4516 (URLs).

## Decision

### 1. Reading 1: an external directory, checked by binding. Reading 2 is not adopted

`--user-ldap` names an external directory; `surl` checks a password login it cannot settle from its own
accounts by searching the directory for the user's entry and binding as that entry with the password the
peer sent (decision 4). ADR-0072 is unchanged: the LDIF directory's `userPassword` stays data, not a
credential.

**Why reading 1:**

- It is the use the request names: accounts kept elsewhere, checked where they are kept, never copied.
  A changed or disabled account takes effect at the next login with no restart.
- The directory, not `surl`, judges the password, so whatever form it stores (`{SSHA}`, `{CRYPT}`,
  Kerberos pass-through, Active Directory's own) works without `surl` implementing any of them.

**Why not reading 2:**

- It would turn a data file into a credential source behind the operator's back: an LDIF seeded only
  to answer searches would start admitting logins on every scheme. That widens access, which secure by
  default forbids unless asked for.
- `userPassword` values in real LDIF are usually hashed (`{SSHA}`, `{CRYPT}`, `{PBKDF2}`), which serve
  only plain-password methods - exactly what reading 1 already serves - while `--user-file` already
  gives clear passwords that serve every method (ADR-0032 section 2).
- A test that wants one file for both directory entries and accounts can run `surl ldap://…` and
  `--user-file` side by side today. If a need for reading 2 appears, it is a new ADR with its own
  explicit option; nothing here closes it.

### 2. The logins it serves, and the ones it never serves

A bind proves a password and nothing else, so the directory serves only a login in which `surl`
receives the clear password:

| Served (the password reaches `surl`) | Never served (`surl` would need the secret or a hash of it) |
| --- | --- |
| HTTP Basic (RFC 7617), on `http`, `https`, `ws`, `wss`, `rtsp` and the proxies | HTTP Digest, NTLM, Negotiate (NTLM or Kerberos) |
| FTP `USER`/`PASS` | Bearer tokens (an empty-name account, ADR-0032 section 1) |
| IMAP `LOGIN`, POP3 `USER`/`PASS` | APOP, SASL `CRAM-MD5`, `DIGEST-MD5`, `SCRAM-SHA-1`, `SCRAM-SHA-256`, `NTLM`, `GSSAPI`, `GSS-SPNEGO`, `EXTERNAL`, `OAUTHBEARER`, `XOAUTH2` |
| SASL `PLAIN` (RFC 4616) and `LOGIN` on every SASL server | AWS Signature Version 4, Azure Shared Key and SAS, Google V4 HMAC |
| SSH `password` (RFC 4252 section 8) | SSH `publickey` and `keyboard-interactive` beyond a password prompt |
| MQTT `CONNECT` user name and password | SMB (NTLM, Kerberos) |
| LDAP simple bind on `surl`'s own LDAP server (ADR-0072 decision 2) | TLS-SRP (`--tlsuser`), CoAP DTLS PSK |
| Each extended server's clear-password login (Redis `AUTH`, NATS, STOMP, AMQP `PLAIN`, …) | |

**How it is drawn:** every served login reaches `IAuthenticationPolicy.CheckPasswordLoginAsync` or
`Surl.Authentication`'s `PLAIN` and `LOGIN` SASL exchanges, which call the same password check; that
check is the only place the directory is consulted (decision 6). Every never-served method looks its
account up in the `AccountBook`, where a directory user does not exist, so it is answered exactly as an
unknown user is: the same verdict, note and delay (ADR-0032 section 8). No method is turned off, no
server changes, and a peer cannot tell a directory account from a missing one by trying Digest.

### 3. The option

One new layer-2 option, in ADR-0101 decision 3's item grammar (no separator in the head):

```
--user-ldap <ldap-url>[;bind-dn=<dn>][;bind-password-file=<file>][;ca-file=<file>][;timeout=<seconds>][;protocols=<scheme>,...][;plaintext]
```

| Key or flag | Meaning | Default |
| --- | --- | --- |
| head | An RFC 4516 LDAP URL, `ldap://` or `ldaps://` (decision 3.1) | required |
| `bind-dn` | The DN `surl` binds as before searching (RFC 4513 section 5.1.3) | none: the search runs anonymously (RFC 4513 section 5.1.1) |
| `bind-password-file` | A file whose first line is `bind-dn`'s password | none; given exactly when `bind-dn` is |
| `ca-file` | PEM certificates trusted, beside the system's roots, to verify the directory | the system's roots alone |
| `timeout` | Seconds one login's whole directory check may take, 1 to 60 | 5 |
| `protocols` | The schemes a directory account may log in on, as `--mailbox`'s key (ADR-0102 decision 4.3) | every scheme |
| `plaintext` | `ldap://` without StartTLS: the passwords cross the network in clear | off; refused unless `--allow-plaintext-auth` is given |

Help category `auth` (ADR-0101 decision 7), description "Check password logins against an LDAP
directory". `OptionArgumentType` `ldap-url` with the allowed-values text listing the keys and the
shared sentence. **The option is given at most once**: one directory is the account source, and a
second would make "which directory refused" ambiguous; a second occurrence is refused (decision 7).
`SurlCommandLine` gains `UserLdap`, a `ConfiguredLdapAccountSource(string Url, string? BindDn, string?
BindPasswordFile, string? CaFile, int TimeoutSeconds, IReadOnlyList<string>? Protocols, bool
IsPlaintext)` beside `CommandLineAccount` (ADR-0101 decision 4's model), with `Url` as written, decoded
once by the item grammar.

**Why the bind password comes from a file only:** it is a real service credential for a directory
that outlives the test, and a command line is visible to every local user in the process list. `--user`
takes test accounts inline because they exist only for `surl`; this one does not. `Surl.Api`'s
spelling (BL-668) may take it as a value, since an in-process call has no process list.

**Why item grammar and not three options:** ADR-0101 decision 3 makes every layer-2 option an item
so `-K`, `Surl.Api`, `Surl.Mcp` and the control endpoint share one model. The cost is that a `%` in the
URL is percent-decoded twice, once by the item grammar and once by the URL: `%` in the URL is written
`%25` in the item (`%2528` for an encoded `(`). `--aihelp` says so. LDAP URLs rarely need a `%`, since
DNs, `(`, `)`, `=`, `*`, `&`, `|` and `,` are written as they are in the head.

#### 3.1 The URL (RFC 4516)

`ldap[s]://<host>[:<port>]/<base-dn>[?<attribute>[?<scope>[?<filter>]]]`

- **Scheme.** `ldaps` is LDAP over TLS from the first byte, port 636 by default; `ldap` is port 389 and
  is upgraded with StartTLS before any credential is sent (decision 5). Any other scheme is refused.
- **Host** is required: RFC 4516 section 2 lets an empty host mean "the client's choice", which a
  server has none of. A user name or password in the URL (`ldap://u:p@host`) is refused without echoing
  the URL: RFC 4516 has no user information, and the bind's credentials have their own keys.
- **Base DN** is required (RFC 4516 section 2, `dn`), parsed as an RFC 4514 DN; an empty one is
  refused, since searching from the root of an unknown server is never what is meant.
- **Attribute** is the one attribute the user name is matched on, `uid` when absent (`sAMAccountName`
  or `userPrincipalName` for Active Directory). More than one attribute is refused.
- **Scope** is `base`, `one` or `sub` (RFC 4516 section 2), `sub` when absent.
- **Filter** is an RFC 4515 filter, `(objectClass=*)` when absent; it narrows who may log in
  (`(memberOf=cn=testers,ou=groups,dc=example,dc=com)`). It is parsed at start, and a malformed one
  refused.
- **Extensions** (RFC 4516's fifth field) are refused, critical or not: none is needed, and ignoring an
  unknown one would let a misspelt setting pass silently, as ADR-0101 refuses an unknown key.

### 4. One login, on the wire

For each login decision 6 sends to the directory, on one new connection, in this order. Every message
is an `LDAPMessage` (RFC 4511 section 4.1.1) with message IDs counting from 1, LDAP version 3, BER with
definite lengths (RFC 4511 section 5.1):

1. **Connect** to the URL's host and port (decision 5 for TLS).
2. **Service bind**, when `bind-dn` is given: a `BindRequest` (RFC 4511 section 4.2) with `simple`
   authentication, `name` the `bind-dn`, `password` the file's (RFC 4513 section 5.1.3). Anything but
   `success` (0) ends the check as the directory unavailable (decision 6): the service account is the
   operator's error, never the peer's. Without `bind-dn` no bind is sent and the search runs as the
   anonymous identity (RFC 4513 section 5.1.1).
3. **Search** for the user's entry: a `SearchRequest` (RFC 4511 section 4.5.1) with `baseObject` the
   URL's base DN, `scope` the URL's, `derefAliases` `neverDerefAliases`, `sizeLimit` 2, `timeLimit` the
   `timeout` in seconds, `typesOnly` TRUE, `filter` `(&<url-filter>(<attribute>=<name>))` with the user
   name escaped as an RFC 4515 section 3 value (`*`, `(`, `)`, `\` and U+0000 as `\2a`, `\28`, `\29`,
   `\5c`, `\00`), and `attributes` the single `1.1`, which asks for no attributes (RFC 4511 section
   4.5.1.8). An escaped name is an equality match, never a wildcard: `al*` cannot match `alice`.
   - exactly one `SearchResultEntry` and a `SearchResultDone` of `success`: its `objectName` is the DN
     bound in step 4;
   - none: **no such user**, refused;
   - two, or `sizeLimitExceeded` (4): **ambiguous**, refused, never bound: picking one would let the
     filter's order decide who logs in;
   - a `SearchResultReference` or a `referral` (10) result is not followed (RFC 4511 sections 4.1.10
     and 4.5.3 leave chasing to the client): a referral reaching another server could carry the
     password there. Counted as no entry, and noted;
   - any other result code ends the check as the directory unavailable.
4. **User bind** on the same connection: a `BindRequest` with `simple` authentication, `name` the
   entry's DN and `password` the password the peer sent (RFC 4513 section 5.1.3). `success` (0) is
   **accepted**; `invalidCredentials` (49) is **refused**; any other code (`inappropriateAuthentication`
   (48) for an account that may not bind, `unwillingToPerform` (53) for one disabled or locked, …) is
   **refused** and its code noted (decision 9). The account name accepted is the name the peer sent,
   not the DN, so groups, access rows, events and notes keep naming it as the peer wrote it.
5. **Unbind** (`UnbindRequest`, RFC 4511 section 4.3) and close. No response is awaited.

**An empty password is never sent.** RFC 4513 section 5.1.2's unauthenticated bind (a name and an empty
password) answers `success` on many directories without checking anything, and section 6.3.1 warns
clients against it. A login with an empty password is refused by `surl` before the directory is
contacted, as `--user` already refuses an empty password (ADR-0032 section 1), and costs the same
delay as any refusal.

**One connection per login, no pool and no cache.** A fresh connection keeps every login independent
(a pooled connection would have to rebind as the service, RFC 4513 section 5.1, between users), and no
verdict is cached, so a test that changes or disables a directory account sees it at the next login.
A cache or a pool is later work if a measured load needs one, in a new ADR.

### 5. TLS (RFC 4513 section 3)

- **`ldaps://`:** TLS from the first byte to the URL's port.
- **`ldap://`:** the first message is the StartTLS `ExtendedRequest`, `requestName`
  `1.3.6.1.4.1.1466.20037` and no value (RFC 4511 section 4.14.1); anything but an `ExtendedResponse`
  of `success` (RFC 4511 section 4.14.2) ends the check as the directory unavailable, and nothing more
  is sent. The TLS handshake follows on the same connection (RFC 4513 section 3.1.1). There is no
  fallback to clear text: a directory or an attacker answering `protocolError` cannot downgrade the
  connection.
- **Server identity** (RFC 4513 section 3.1.3; RFC 6125): the certificate chain must build to a system
  root or a `ca-file` certificate, and the URL's host must match a DNS-ID (or an IP-address SAN for an
  IP literal) in the certificate. There is no option to skip either check, unlike curl's `-k`: the
  password a peer gave `surl` is sent over this connection, so a test that wants a self-signed
  directory names its certificate in `ca-file`.
- **Versions:** TLS 1.2 and 1.3 only, as ADR-0006 section 4 holds `surl`'s own listeners. No client
  certificate is sent (RFC 4513 section 5.2.1's SASL `EXTERNAL` is not used).
- **`;plaintext`** sends both binds in clear over `ldap://` and is accepted only with
  `--allow-plaintext-auth` (ADR-0032's loosening for clear passwords), warned at start (decision 9).
  `;plaintext` on an `ldaps://` URL is refused: it would say something `surl` does not do.

### 6. Where the directory sits among the accounts

- **The account book first.** A login whose name is an account in the `AccountBook` (`--user`,
  `--user-file`, `--mailbox`, or one a test added, ADR-0132) is judged by the book alone and never
  sent to the directory, so one name never has two passwords and a wrong local password is never tried
  against the directory.
- **The directory second.** A name the book does not hold, on a scheme `protocols=` admits, with a
  non-empty password and a name of at most 256 UTF-8 bytes holding no U+0000, goes to the directory.
  Any other is refused without contacting it, as an unknown user.
- **Verdicts.** Accepted, or refused as a wrong credential (`PasswordLoginVerdict.RefusedCredentials`).
  No such user, ambiguous, wrong password, a refused bind, a timeout, a refused connection, a TLS or
  identity failure, a malformed answer and a service bind that failed are all the **same** refusal to
  the peer: the same verdict, the same login note as an unknown `--user-file` user, and the same delay
  (ADR-0032 section 8). Only the verbose log's reason (decision 9) tells them apart, for the operator.
- **A directory that is down** is refused like a wrong password and noted; `surl` keeps serving, and
  its local accounts keep working. `surl` does not contact the directory at start, so a test may start
  `surl` before its directory.
- **The refusal delay** for a login the directory judged runs from when the check began: the refusal
  is answered once one second has passed since then, or at once if the directory took longer. So a
  slow directory does not stack its latency on top of the delay, and a refusal the directory answers
  quickly costs what an unknown local user costs. The delay is still cancelled with the exchange.
- **Faults and events** (ADR-0128): an accepted directory login is offered to the fault plan as any
  account is (`refuse-login` applies), and every judged directory login publishes the same
  `LoginJudgedEvent`, method `password`, never a password, a DN or the bind password.
- **Snapshots, reset and restore** (ADR-0132) cover the account book only. Directory accounts are not
  in it and are untouched; a test controls them in its directory.

### 7. Refusals and exit codes

**No refusal text ever holds the URL, the bind DN or a password**, so a refusal can be pasted into a
bug report. `<name>` is `--user-ldap` as written.

While parsing, in `Surl.Cli` (`FailedInit` (2), `surl: ` then the text, then ADR-0007's `try` line):

| Case | Text after `surl: ` |
| --- | --- |
| Not `ldap://` or `ldaps://` | `option <name>: expected an ldap:// or ldaps:// URL` |
| `bind-dn` without `bind-password-file`, or the other way round | `option <name>: bind-dn and bind-password-file are given together` |
| `timeout` not a whole number from 1 to 60 | `option <name>: timeout is not 1 to 60 seconds` |
| `plaintext` on `ldaps://` | `option <name>: plaintext cannot be used with ldaps://` |
| Given twice | `option <name>: is given twice` |
| Unknown key, malformed item, `protocols=` naming no server | ADR-0101 decision 5's rows, as for every item option |

After the whole command line is read, in `Surl.Composition`, before any listener binds (no `try`
line). The URL's structure is checked here because its parsers live in `Surl.Authentication`
(decision 11), which `Surl.Cli` does not reference:

| Case | Exit | Text after `surl: ` |
| --- | --- | --- |
| A user name or password in the URL | `FailedInit` (2) | `(2) --user-ldap: the URL holds a user name or password; use bind-dn and bind-password-file` |
| No host | 2 | `(2) --user-ldap: the URL names no host` |
| A port that is not 1 to 65535 | 2 | `(2) --user-ldap: the URL's port is not 1 to 65535` |
| No base DN, or one that is not an RFC 4514 DN | 2 | `(2) --user-ldap: the URL's base DN is missing or malformed` |
| More than one attribute | 2 | `(2) --user-ldap: the URL names more than one attribute` |
| A scope other than `base`, `one`, `sub` | 2 | `(2) --user-ldap: the URL's scope is not base, one or sub` |
| A malformed RFC 4515 filter | 2 | `(2) --user-ldap: the URL's filter is malformed` |
| An extension | 2 | `(2) --user-ldap: the URL's extensions are not supported` |
| `plaintext` without `--allow-plaintext-auth` | 2 | `(2) --user-ldap plaintext needs --allow-plaintext-auth` |
| `bind-password-file` missing, a directory or unreadable | `CouldNotReadFile` (37) | `(37) Could not read bind password file <path>` |
| Its first line empty, or not UTF-8 | `FailedInit` (2) | `(2) Bind password file <path>: the password is empty or not UTF-8` |
| `ca-file` missing or unreadable | `CaCertificateBadFile` (77) | `(77) Could not read CA file <path>` |
| `ca-file` holding no PEM certificate | `CaCertificateBadFile` (77) | `(77) CA file <path> holds no certificate` |

37 and 77 are the codes `surl` already gives a file it cannot read and a bad CA file. The bind password
file is read once at start; its first line (to LF, one CR before it dropped) is the password, and the
rest of the file is ignored. Nothing about the directory is checked at start (decision 6).

### 8. Groups, access rows and scheme views

- **A directory account is an account name** to `--group`, `--share-access`, `--topic-access`,
  `--shared-folder` and the fault plan's account match, like any other (ADR-0102's note for BL-671).
- **Amends ADR-0102 decision 4.1:** with `--user-ldap` given, a member that names no configured account
  or group is accepted at start as a directory name, since the directory is not read at start; `surl -v`
  notes each one once (`Member <name> is not a configured account; taken as a directory account`).
  Without `--user-ldap`, ADR-0101 decision 5 row 18's refusal is unchanged.
- **`protocols=`** filters the directory as it filters an account: on a scheme it does not name, a
  directory name is refused as an unknown user without contacting the directory, through the same
  per-scheme views (ADR-0102 decision 4.3).

### 9. Warnings and notes

- **No warning for `--user-ldap` alone:** it admits only people whose passwords the operator's directory
  knows, and whom the URL's filter selects; it widens nothing a peer can do without a password.
- **`;plaintext`** writes, after `--allow-plaintext-auth`'s line in ADR-0032 section 9's order:
  `surl: warning: --user-ldap: passwords are sent to <host>:<port> without TLS`.
- **At start** `surl -v` notes `Account source: LDAP directory <scheme>://<host>:<port>` (never the base
  DN, the filter or the bind DN, which name the directory's layout).
- **Per login** the existing note (ADR-0038) `Login refused: <method> <user>` gains, for a directory
  refusal, ` (directory: <reason>)`, with `<reason>` one of `no such entry`, `ambiguous entry`,
  `wrong password`, `bind refused, result <code>`, `service bind refused, result <code>`,
  `StartTLS refused, result <code>`, `referral not followed`, `timed out after <n> s`,
  `could not connect`, `TLS failed`, `certificate not trusted for <host>` or `malformed answer`. A peer
  never sees it. No note or exception message ever holds a password, the bind password or the bind DN.

### 10. Bounds (ADR-0006)

| Bound | Value | When reached |
| --- | --- | --- |
| One login's directory check | `timeout`, 5 s by default, 1 to 60 s, on the injected `TimeProvider`, covering connect, TLS, binds and search | refused, `timed out after <n> s` |
| Directory checks at once | 16 per `surl` instance; a further login waits for a slot inside its own `timeout` | refused, `timed out after <n> s` |
| One message from the directory | 64 KiB by its BER length, checked before the body is read | refused, `malformed answer`; the connection closed |
| Entries read from one search | 2 (`sizeLimit` 2, enforced by `surl` too) | the second makes it `ambiguous entry` |
| User name sent | 256 UTF-8 bytes, no U+0000 | refused without contacting the directory |
| Bind password file read | 64 KiB | the 37 row above |

Each bound is a constant in `Surl.Authentication`, not an option, except `timeout`.

### 11. Where the code lives

- **`Foundation/Surl.Authentication.UnitLibrary`** (BL-672): the check (`LdapAccountSource`), the
  per-login exchange of decision 4, the few `LDAPMessage`s it needs (`BindRequest`/`BindResponse`,
  StartTLS's `ExtendedRequest`/`ExtendedResponse`, `SearchRequest`, `SearchResultEntry`,
  `SearchResultReference`, `SearchResultDone`, `UnbindRequest`) written and read with the BCL's
  `System.Formats.Asn1` under BER rules, the RFC 4515 filter parser and value escaping, and the RFC 4516
  and RFC 4514 URL and DN parsers, whose refusals `Surl.Composition` turns into decision 7's start-up
  texts. It reaches the directory
  through a seam it owns:

  ```csharp
  public interface ILdapDirectoryConnector
  {
      // Opens TCP to host:port; TLS at once when useTls, else clear (StartTLS is the source's to ask).
      ValueTask<ILdapDirectoryConnection> ConnectAsync(LdapDirectoryEndpoint endpoint, CancellationToken cancellationToken);
  }
  public interface ILdapDirectoryConnection : IAsyncDisposable
  {
      Stream Stream { get; }
      // Runs the TLS client handshake on the open connection, after StartTLS's success.
      ValueTask StartTlsAsync(CancellationToken cancellationToken);
  }
  ```

  Tests replay a directory in memory behind it, so `Surl.Authentication`'s tests open no socket.
- **`Foundation/Surl.Networking.UnitLibrary`** (BL-1068, filed by this task): a public TLS client
  connector over sockets and `SslStream` - resolve and connect as `SocketOriginConnector` does, TLS 1.2
  and 1.3, chain built to the system roots plus given PEM certificates, host-name or IP check, client
  handshake on an open stream for StartTLS - with no reference to `Surl.Authentication`. **No allowed-
  origin rule applies**: the operator names the directory, unlike a proxy's peer-chosen origin
  (ADR-0088 decision 2).
- **`Application/Surl.Composition.UnitLibrary`** (BL-673): adapts BL-1068's connector to
  `ILdapDirectoryConnector` and composes `LdapAccountSource` into `AuthenticationPolicy` beside the
  account book; and **`Application/Surl.Cli.UnitLibrary`** (BL-673): the option row and the item reader.
- **Why not reuse `Surl.Protocol.Ldap`'s codec:** ADR-0002 forbids `Surl.Authentication` referencing a
  protocol server, and lifting the codec into a horizontal library would touch the LDAP server, the
  ADR-0002 table and every LDAP task for nine small client messages. The two codecs share
  `System.Formats.Asn1`, not code; ADR-0072 decision 9 stays true (no hand-built BER library).
- **Why `ILdapDirectoryConnector` is not in `Surl.Protocol.Abstractions`:** no protocol server uses it,
  and a contract there would make BL-672 run apart from every protocol task (`touches`).

## Alternatives considered

- **Reading 2, the LDIF `userPassword` as credentials.** Rejected in decision 1: it widens access
  silently and serves only what reading 1 serves.
- **Both readings.** Rejected for now: two new sources at once for a lowest-priority feature, and
  reading 2's warning-worthy widening without a user who asked for it.
- **Direct bind with a DN template** (`uid=%s,ou=people,dc=…`, or Active Directory's `name@domain`).
  Rejected as the only mode: it cannot apply a filter (group membership) and cannot reject an ambiguous
  name; search-then-bind covers both directory shapes with one rule (`sAMAccountName` as the
  attribute). A template mode can be added as a key later without repurposing anything.
- **Reading password hashes from the directory** (search `userPassword` and compare). Rejected: it
  needs read access to every hash, which well-run directories deny, and `surl` would implement every
  hash scheme. It would still not serve Digest or NTLM.
- **Three plain options** (`--user-ldap`, `--user-ldap-bind-dn`, `--user-ldap-password-file`).
  Rejected for ADR-0101's one-item model; the cost (double percent-decoding) is stated in decision 3.
- **An inline `bind-password=`.** Rejected: process lists (decision 3).
- **A `-k`-style key to skip certificate checks.** Rejected: the peer's password would go to whoever
  answers. `ca-file` serves self-signed test directories.
- **Falling back from StartTLS to clear text.** Rejected: a downgrade an attacker controls.
- **Caching verdicts or pooling connections.** Rejected for now (decision 4).
- **Contacting the directory at start to fail fast.** Rejected: tests start `surl` and the directory in
  either order, and a directory down at start may be up by the first login.

## Consequences

- **BL-672** (`Surl.Authentication`): Goal and `touches` unchanged. Its Context gains: the seam is
  `ILdapDirectoryConnector` (decision 11), the codec is on `System.Formats.Asn1`, the URL and filter
  parsers live here, and the refusal delay for a directory login runs from the check's start
  (decision 6). Its tests cover decision 4's cases, decision 2's never-served methods, decision 10's
  bounds and decision 9's notes.
- **BL-673** (`Surl.Cli`, `Surl.Composition`): Goal unchanged, the option is `--user-ldap` with
  decision 3's keys. `depends-on` gains BL-1068 (the TLS client connector it adapts). `touches`
  unchanged: the adapter lives in `Surl.Composition`. Its first criterion reads per decision 7:
  `Surl.Cli.UnitTests` refuse a non-LDAP scheme and the bad keys, and `Surl.Composition.UnitTests`
  refuse the malformed URL structures, neither echoing the URL. It also carries decision 8's amendment to
  ADR-0102 decision 4.1 (composition's group and access-row member check) and decision 9's warning,
  in `Surl.Composition`, and the `--aihelp` facts, prose and example (an unreachable directory refused
  at login is not runnable as an example; the example shows decision 7's `plaintext` refusal).
- **BL-1068** (new, `Foundation/Surl.Networking.UnitLibrary` and `.UnitTests`): the TLS client
  connector of decision 11, tested against an in-process `SslStream` server on loopback.
- ADR-0072 is unchanged; ADR-0100 decision 4.3's promise (no server changes) holds.
