# ADR-0049 — The mail servers' SASL and APOP logins: the mechanisms offered, their `--auth` words and the `IMailAuthenticationPolicy` contract

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-185 (FR-046).
- **Amends:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  section 3: its table of `--auth` words, the default set and the order methods are listed in
  grow by decision 3 below. Its section 6 contract (`IAuthenticationPolicy`) is unchanged; the
  mail logins get a new interface beside it (decision 6). Everything else in ADR-0032 and
  [ADR-0038](ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md) stands.

## Context

SMTP (`AUTH`, RFC 4954), IMAP (`AUTHENTICATE`, RFC 3501 section 6.2.2, and `LOGIN`) and POP3
(`AUTH`, RFC 5034; `USER`/`PASS` and `APOP`, RFC 1939) log in through SASL (RFC 4422) and, for
IMAP and POP3, through a clear-password command of their own. ADR-0032 section 6 left "any other
kind" of login than a clear password to "a new contract member ... added by that server's task";
this ADR decides that member once for all three mail servers, before BL-193 (the contract),
BL-194 to BL-196 (the mechanisms in `Surl.Authentication`), BL-197 (the `--auth` words) and the
servers' own login tasks.

### What upstream curl 8.21.0 does (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` (`UpstreamCurlBuilds.json`),
  on 2026-09-29.
- **Tool:** `Record-CurlExchange.ps1 -Smtp`, `-Imap` and `-Pop3` on ports 18025, 18143 and 18110,
  with `-SmtpReply 'EHLO=...'`, `-ImapReply 'CAPABILITY=...'` and `-Pop3Reply 'CAPA=...'` setting
  what the server advertises, and the new `-SaslChallenge 'MECHANISM=line'` (added by this task,
  described in the script's help) scripting the continuations a mechanism needs. `ALL` below is
  `GSSAPI EXTERNAL OAUTHBEARER XOAUTH2 NTLM DIGEST-MD5 CRAM-MD5 LOGIN PLAIN` (IMAP: each as
  `AUTH=<name>`). `-u user:secret` unless said. `>` is curl, `<` the recorder.

**Which mechanism curl picks** when several are advertised (SMTP; IMAP and POP3 agree where
measured):

| Advertised | Credentials | curl sends |
| --- | --- | --- |
| `ALL` | `-u user:secret` | `AUTH DIGEST-MD5` |
| `DIGEST-MD5 CRAM-MD5 LOGIN PLAIN` | same | `AUTH DIGEST-MD5` |
| `NTLM CRAM-MD5 LOGIN PLAIN` | same | `AUTH CRAM-MD5` (also over `smtps://` with `-k`) |
| `NTLM LOGIN PLAIN` | same | `AUTH NTLM` |
| `LOGIN PLAIN` | same | `AUTH PLAIN` |
| `GSSAPI PLAIN` | `-u user@EXAMPLE.COM:secret` | `AUTH GSSAPI` (then exit 94 once challenged: `(94) An authentication function returned an error`) |
| `ALL` | `-u user: --oauth2-bearer tok` | `AUTH OAUTHBEARER` |
| `ALL` | `-u user:secret --oauth2-bearer tok` | `AUTH OAUTHBEARER` |
| `XOAUTH2 LOGIN PLAIN` | `-u user: --oauth2-bearer tok` | `AUTH XOAUTH2` |
| `DIGEST-MD5 CRAM-MD5 NTLM LOGIN PLAIN` | `-u user: --oauth2-bearer tok` | nothing; exit 67 `(67) Login denied` |
| `ALL` | `-u user:` | `AUTH DIGEST-MD5` (`EXTERNAL` is never picked unasked) |
| `SCRAM-SHA-256` | `-u user:secret` | nothing; exit 67 `(67) Login denied` |
| no `AUTH` line | `-u user:secret` | no `AUTH` at all; carries on unauthenticated, exit 0 |

So curl's preference is: `OAUTHBEARER`, `XOAUTH2` (only with `--oauth2-bearer`, and then
nothing else), then `GSSAPI` (only for a user name holding a realm), `DIGEST-MD5`, `CRAM-MD5`,
`NTLM`, `PLAIN`, `LOGIN`; `EXTERNAL` only when forced. The order the server advertises in
changes nothing. The mechanism is the same over TLS.

**What curl sends for each mechanism** (`--login-options AUTH=<mech>`):

| Mechanism | Without `--sasl-ir` | With `--sasl-ir` |
| --- | --- | --- |
| `PLAIN` | `AUTH PLAIN`, `< 334 `, `> AHVzZXIAc2VjcmV0` (`\0user\0secret`) | `AUTH PLAIN AHVzZXIAc2VjcmV0` |
| `PLAIN`, `--sasl-authzid boss` | | `AUTH PLAIN Ym9zcwB1c2VyAHNlY3JldA==` (`boss\0user\0secret`) |
| `LOGIN` | `AUTH LOGIN`, `< 334 VXNlcm5hbWU6`, `> dXNlcg==`, `< 334 UGFzc3dvcmQ6`, `> c2VjcmV0` | `AUTH LOGIN dXNlcg==`, `< 334 UGFzc3dvcmQ6`, `> c2VjcmV0` |
| `CRAM-MD5` | `AUTH CRAM-MD5`, `< 334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=`, `> dXNlciA0ZWY4ZWU0NDBiYjU0MTg5NWEwMWY2OWZiMjY2MjlkZA==` (`user 4ef8ee440bb541895a01f69fb26629dd`) | the same: no initial response |
| `DIGEST-MD5` | `AUTH DIGEST-MD5`, `< 334 <challenge>`, `> <response>`, `< 334 <rspauth>`, `> ` (an empty line) | the same: no initial response |
| `NTLM` | `AUTH NTLM`, `< 334 `, `> <type 1>`, `< 334 <type 2>`, `> <type 3>` | `AUTH NTLM <type 1>`, `< 334 <type 2>`, `> <type 3>` |
| `XOAUTH2`, `-u user: --oauth2-bearer tok` | `AUTH XOAUTH2`, `< 334 `, `> dXNlcj11c2VyAWF1dGg9QmVhcmVyIHRvawEB` (`user=user\x01auth=Bearer tok\x01\x01`) | `AUTH XOAUTH2 dXNlcj11c2VyAWF1dGg9QmVhcmVyIHRvawEB` |
| `OAUTHBEARER`, same | `AUTH OAUTHBEARER`, `< 334 `, `> bixhPXVzZXIsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgwMjUBYXV0aD1CZWFyZXIgdG9rAQE=` (`n,a=user,\x01host=127.0.0.1\x01port=18025\x01auth=Bearer tok\x01\x01`) | `AUTH OAUTHBEARER` and the same bytes |
| `EXTERNAL`, `-u user:` | `AUTH EXTERNAL`, `< 334 `, `> dXNlcg==` (`user`) | `AUTH EXTERNAL dXNlcg==` |
| `GSSAPI`, `-u user:secret` | nothing; exit 67 `(67) Login denied` | |

IMAP sends the same bytes after `A002 AUTHENTICATE <mech>`, its continuations being `+ ...`;
with `--sasl-ir` it sends the initial response on the command line whether or not `SASL-IR` is
advertised. POP3 sends the same after `AUTH <mech>`, continuations `+ ...`.

- **`DIGEST-MD5`**, offered `realm="surl",nonce="MDEyMzQ1Njc4OWFiY2RlZg==",qop="auth",charset=utf-8,algorithm=md5-sess`
  (base64 `cmVhbG09InN1cmwiLG5vbmNlPSJNREV5TXpRMU5qYzRPV0ZpWTJSbFpnPT0iLHFvcD0iYXV0aCIsY2hhcnNldD11dGYtOCxhbGdvcml0aG09bWQ1LXNlc3M=`),
  curl answers
  `username="user",realm="",nonce="MDEyMzQ1Njc4OWFiY2RlZg==",digest-uri="smtp/127.0.0.1",cnonce="e495b889b7b854405de324ede5849ea3",nc=00000001,response=9ff8685c4f01832cd4bed741bdc439b7,qop=auth,charset=utf-8`.
  The realm it sends is empty, whichever realm is offered (also measured with RFC 2831's
  `elwood.innosoft.com`), and the response is computed with that empty realm: RFC 2831 section
  2.1.2.1 over `user::secret` gives `9ff8685c...`, over `user:surl:secret` it would give
  `35bb9e57...`. The `digest-uri` is `smtp/`, `imap/` or `pop/` then the host as given in the
  URL. curl does not check `rspauth` (a wrong one was accepted) and answers it with an empty line.
- **`CRAM-MD5`**, challenged `<0123456789abcdef.1790640000@surl>`, curl answers
  `user 79a4ce2457c420f9dd830de72f78a7d2`, HMAC-MD5 keyed by `secret` (RFC 2195).
- **`APOP`**: POP3 greeting `+OK POP3 ready <0123456789abcdef.1790640000@surl>`, CAPA `USER`
  only: curl sends `APOP user 32d4437494fda0ae78d0559952474e34`, MD5 of timestamp then
  `secret` (RFC 1939 section 7). With `SASL PLAIN` or `SASL LOGIN` also in CAPA, curl uses SASL
  and never `APOP`. With no timestamp in the greeting it sends `USER user` / `PASS secret`;
  forced with `--login-options AUTH=+APOP` and no timestamp it sends nothing, exit 67.
  `AUTH=+USER` is refused by curl, exit 3.
- **`NTLM`**, answered with the type 2 message Surl's HTTP NTLM sends (ADR-0039: target `SURL`,
  server challenge `0123456789abcdef`), `--sasl-ir`, SMTP: type 1
  `TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`, type 2
  `TlRMTVNTUAACAAAACAAIADAAAAAFgoqgASNFZ4mrze8AAAAAAAAAABwAHAA4AAAAUwBVAFIATAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAAAAAA`,
  type 3 (`user`/`secret`, NTLMv2, SPN `smtp/127.0.0.1`)
  `TlRMTVNTUAADAAAAGAAYAH4AAADUANQAlgAAAAAAAABYAAAACAAIAFgAAAAeAB4AYAAAAAAAAABqAQAABYKIogoA9GUAAAAPpgITG5q/UykWS+PZmsHf4HUAcwBlAHIAUwBUAEUAVwBBAFIAVAAtAFIATwBHAEUAUgBTAC0AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIWk1twMV/l4rJ3YcUmGEWwEBAAAAAAAAswFk93xQ3QG9LFx5Aw4sWQAAAAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAIAFAAUAAAAAAAAAABAAAAACAAAA82fx1uMgwq+62Zq5sV4iBe8HQknDfjMjud6b7ltW9Twv580Os8fiRrKHCVeMX/KLHTmbLzKHclJA6owo/zF5gKABAAAAAAAAAAAAAAAAAAAAAAAAkAHABzAG0AdABwAC8AMQAyADcALgAwAC4AMAAuADEAAAAAAAAAAAA=`.
  IMAP and POP3 send the same type 1 and a type 3 of the same shape with SPN `imap/...` and
  `pop/...`.
- **IMAP `LOGIN`**: with no `AUTH=` advertised curl sends `A002 LOGIN user secret`, quoting as
  needed (`-u 'us er:se"cr\et'` gives `A002 LOGIN "us er" "se\"cr\\et"`); with
  `--login-options AUTH=+LOGIN` it sends `LOGIN` even when `AUTH=` mechanisms are advertised.
  With `LOGINDISABLED` and no `AUTH=` it sends nothing, exit 67 `(67) Login denied`; with
  `LOGINDISABLED AUTH=PLAIN` it uses `AUTHENTICATE PLAIN`.
- **POP3 `USER`/`PASS`**: with neither `USER` nor `SASL` in CAPA and no timestamp curl sends
  nothing, exit 67; when `CAPA` is answered `-ERR`, it falls back to `USER`/`PASS`.

**What curl does when a login fails.** It never falls back to another mechanism; every failure
is exit 67:

| Server answer | stderr |
| --- | --- |
| SMTP `535 5.7.8 ...` after a response, `504 5.5.4 ...` to `AUTH CRAM-MD5`, `534 5.7.9 ...` or `538 5.7.11 ...` to `AUTH PLAIN`, `501 5.7.0 ...` after a response | `curl: (67) Login denied` |
| IMAP `A002 NO [AUTHENTICATIONFAILED] ...` to `AUTHENTICATE`, `NO` to `AUTHENTICATE CRAM-MD5` with `AUTH=PLAIN` also advertised | `curl: (67) Login denied` |
| IMAP `A002 NO ...` to `LOGIN` | `curl: (67) Access denied. ` |
| POP3 `-ERR` to `AUTH PLAIN`, and to `AUTH CRAM-MD5` with `PLAIN` also advertised | `curl: (67) Login denied` |
| POP3 `-ERR` to `PASS` | `curl: (67) Access denied. -` |
| POP3 `-ERR` to `APOP` | `curl: (67) Authentication failed: 45` |
| `XOAUTH2` answered with a continuation carrying an error (`334 eyJzdGF0dXMiOi...`) | curl sends nothing more and hangs up: `curl: (67) Login denied` |
| `OAUTHBEARER` answered the same way | curl sends `AQ==` (`\x01`, RFC 7628 section 3.2.3), then the final refusal: `curl: (67) Login denied` |
| `DIGEST-MD5` challenge that is not a challenge (`bm9uc2Vuc2U=`) | curl sends nothing: `curl: (94) An authentication function returned an error` |
| A final success (`235`, `OK`, `+OK`) where curl expects a continuation (`AUTH DIGEST-MD5`, `AUTH XOAUTH2` without an initial response) | `curl: (67) Login denied` |

## Decision

### 1. The mechanisms, and what counts as a plain-text secret

| Mechanism | Specification | Plain-text secret | Built by |
| --- | --- | --- | --- |
| `PLAIN` | RFC 4616 | **yes** | BL-194 |
| `LOGIN` | draft-murchison-sasl-login | **yes** | BL-194 |
| `XOAUTH2` | Google's XOAUTH2 | **yes** (a bearer token) | BL-194 |
| `OAUTHBEARER` | RFC 7628 | **yes** (a bearer token) | BL-194 |
| `CRAM-MD5` | RFC 2195 | no | BL-195 |
| `DIGEST-MD5` | RFC 2831 (Historic, RFC 6331) | no | BL-195 |
| POP3 `APOP` | RFC 1939 section 7 | no | BL-195 |
| `NTLM` | MS-NLMP, carried as in HTTP (ADR-0039) | no | BL-196 |
| `EXTERNAL` | RFC 4422 appendix A, the TLS client certificate | no (no secret is sent) | BL-216 |
| `GSSAPI` | RFC 4752, Kerberos V5 | no | BL-218, after BL-217 decides Kerberos (BL-218 is done; see Amendment 2) |
| IMAP `LOGIN`, POP3 `USER`/`PASS` | RFC 3501, RFC 1939 | **yes** | the IMAP and POP3 servers, through ADR-0032's `CheckPasswordLoginAsync` |

- A bearer token read off the wire is as good as a password, so `XOAUTH2` and `OAUTHBEARER`
  count as plain-text, like HTTP Bearer (ADR-0032 section 3). `CRAM-MD5`, `DIGEST-MD5`, `APOP`
  and `NTLM` send a value computed from the secret, and are not.
- **A plain-text mechanism is offered and accepted only over TLS, or with
  `--allow-plaintext-auth`**, as HTTP Basic is (ADR-0032 section 4). Over a connection without
  TLS it is not advertised, and a client that starts it anyway is refused before any credential
  is read (`RefusedPlaintext`, decision 7): `AUTH LOGIN` is refused before the password is asked
  for, and an initial response sent with `AUTH PLAIN` is not checked.
- IMAP `LOGIN` and POP3 `USER`/`PASS` stay outside `--auth`, as FTP's `USER`/`PASS` and MQTT's
  password are (ADR-0032 sections 5 and 6): they are each protocol's base login, governed by
  `--allow-plaintext-auth` alone.

### 2. What each server offers, and in what order

The offer is computed per connection from the accepted set (`--auth`), the connection's
`TlsSession` and `--allow-plaintext-auth`, again after `STARTTLS` or `STLS` (the client asks for
the capabilities again, and RFC 3207, 3501 and 2595 require it to). It does not depend on whether
any account is configured (ADR-0006 section 3).

- **SASL mechanisms**, in this order, each only when its word is accepted and it may be offered
  on the connection: `GSSAPI`, `DIGEST-MD5`, `CRAM-MD5`, `NTLM`, `OAUTHBEARER`, `XOAUTH2`,
  `PLAIN`, `LOGIN`, `EXTERNAL`. This is curl's measured preference, so the list reads as what a
  curl client will pick; the order changes nothing for curl. `EXTERNAL` is offered only when
  `TlsSession.ClientCertificate` is not `null` (ADR-0010 section 5, `--cacert`); `GSSAPI` was not
  offered until BL-218 landed; since it did (done), `GSSAPI` is offered first on every connection,
  TLS or not, whenever `gssapi` is accepted (Amendment 2).
- **SMTP**: the mechanisms on one `250-AUTH <m1> <m2> ...` line of the `EHLO` reply, left out when
  none may be offered.
- **IMAP**: `AUTH=<m>` capabilities in the same order; `LOGINDISABLED` (RFC 3501 section 6.2.3)
  when a clear password may not be used on the connection; `SASL-IR` (RFC 4959) always.
- **POP3**: `SASL <m1> <m2> ...` in `CAPA` when any may be offered; `USER` only when a clear
  password may be used; the greeting carries an `APOP` timestamp only when `apop` is accepted.

With the default set over a connection without TLS (which leaves out `DIGEST-MD5`, decision 3),
the offer is `CRAM-MD5` alone: curl's `-u` then logs in with `CRAM-MD5` over
`smtp://`, `imap://` and `pop3://` without sending the password, and IMAP says `LOGINDISABLED`.
Over TLS it is `CRAM-MD5 OAUTHBEARER XOAUTH2 PLAIN LOGIN`, and curl picks `CRAM-MD5` (or
`OAUTHBEARER` with `--oauth2-bearer`).

### 3. The `--auth` words

Each SASL mechanism's word is its registered name in lower case, the word curl's own
`--login-options AUTH=<mech>` uses, so an operator writes on the server the name they write on
the client. A mechanism that is the same method as an HTTP one shares its word: `ntlm` accepts
HTTP NTLM and SASL `NTLM`, which carry the same messages and are checked by the same code. HTTP
Digest (RFC 7616) and `DIGEST-MD5` (RFC 2831) are different mechanisms and get different words;
so do HTTP Bearer and the two SASL bearer-token mechanisms, which curl selects with `AUTH=` and
not with `--oauth2-bearer` alone.

ADR-0032 section 3's table becomes, in this order, which is also the order of the warning line
`surl: warning: --auth: accepted methods are <methods>` (ADR-0032 section 9) and of `--help`'s
list:

| `--auth` word | Method | Protocols | In the default set |
| --- | --- | --- | --- |
| `negotiate` | Negotiate, RFC 4559 | HTTP | no (ADR-0032) |
| `gssapi` | SASL `GSSAPI` | SMTP, IMAP, POP3 | no (refused as not available until BL-218 built it; BL-218 is done, and `gssapi` now needs `--keytab`; see Amendment 2) |
| `ntlm` | NTLM | HTTP, SMTP, IMAP, POP3 | no (ADR-0032) |
| `digest` | Digest, RFC 7616 | HTTP | yes |
| `digest-md5` | SASL `DIGEST-MD5` | SMTP, IMAP, POP3 | **no** |
| `cram-md5` | SASL `CRAM-MD5` | SMTP, IMAP, POP3 | yes |
| `apop` | POP3 `APOP` | POP3 | **no** |
| `basic` | Basic, RFC 7617 | HTTP | yes |
| `plain` | SASL `PLAIN` | SMTP, IMAP, POP3 | yes |
| `login` | SASL `LOGIN` | SMTP, IMAP, POP3 | yes |
| `bearer` | Bearer, RFC 6750 | HTTP | yes |
| `oauthbearer` | SASL `OAUTHBEARER` | SMTP, IMAP, POP3 | yes |
| `xoauth2` | SASL `XOAUTH2` | SMTP, IMAP, POP3 | yes |
| `external` | SASL `EXTERNAL` | SMTP, IMAP, POP3 | yes (refused as not available until BL-216 built it and added it to the default set; BL-216 is done) |
| `aws-sigv4` | AWS Signature Version 4 | HTTP | yes |

- The order: methods computed from a secret, strongest first, then clear secrets, then
  `external`, with `aws-sigv4` last as before; each mail word sits beside its HTTP kin.
- **The default set** becomes
  `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,aws-sigv4`, and `external` joined
  it when BL-216 landed (done), so it is now
  `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,external,aws-sigv4`. **`digest-md5`** stays a named choice because RFC 6331 moved it to Historic
  for its weaknesses, and curl prefers it above every other mechanism, so offering it by default
  would make it the one every curl client uses. **`apop`** stays a named choice because its
  MD5-prefix construction lets a party that chooses the timestamp recover password characters
  (Leurent, "Message Freedom in MD4 and MD5 Collisions: Application to APOP", 2007), and a POP3
  client that sees a timestamp may use it. Both are named, like `ntlm`, with `--auth`.
- The words are matched and stored as ADR-0032 section 1 says; the refusal
  `surl: (2) --auth <word> is not available in this build` covered `gssapi` and `external` until
  their tasks landed, and nothing else once BL-194 to BL-196 were Done; since BL-216 (done) it
  covered `gssapi` alone, and since BL-218 (done) it covers no `--auth` word (Amendment 2).
- **Help** (ADR-0034, one line within curl's 79 columns): `--auth`'s description stays
  "Authentication methods accepted", its `Default` becomes the default set above, and its
  explanation lists the words with the protocols each applies to, as the table does. BL-197
  writes it, with the manual and the `--aihelp` facts (ADR-0046).

### 4. `GSSAPI`, Kerberos and `EXTERNAL`

- **`GSSAPI`** is Kerberos V5, which ADR-0032 section 11 made later work for Negotiate. It is
  built by hand like every other missing primitive, not refused as a decision: BL-217 decides how
  Surl holds its Kerberos key and checks a ticket, and BL-218 builds the SASL `GSSAPI` exchange
  on it. Until BL-218 landed, `--auth gssapi` was refused as not available, `GSSAPI` was never
  offered, and a client that sent `AUTH GSSAPI` anyway got `RefusedMechanism`; BL-218 is done,
  and Amendment 2 records what holds now. Measured, curl picks
  `GSSAPI` unasked only for a user name holding a realm (`user@EXAMPLE.COM`); a server that
  offered it without being able to finish it would break those logins with exit 94.
- **`EXTERNAL`** logs in as the verified TLS client certificate (ADR-0010 section 5: `--cacert`
  makes every handshake require one that chains to its anchors). The identity is the
  certificate's subject simple name (`X509Certificate2.GetNameInfo(X509NameType.SimpleName,
  false)`); the login is accepted when an account of exactly that name exists (its password is
  not used) and the authorization identity curl sends (the `-u` user name, measured) is empty or
  equal to it (ordinal). Otherwise it is `RefusedCredentials`. It is offered only on a
  connection whose `TlsSession.ClientCertificate` is not `null`, and is not plain-text. BL-216
  built it (done); amendment 1 records how it answers a connection without a client
  certificate.

### 5. Each mechanism's exchange

Everything below is the bytes before base64; the server encodes and decodes (decision 6).
Randomness comes from an injected source and time from the injected `TimeProvider`, as
`INtlmServerChallengeSource` does for NTLM, so tests are deterministic.

- **`PLAIN`**: without an initial response, one empty challenge. The response is
  `[authzid] NUL authcid NUL passwd`, UTF-8 (RFC 4616). The `authzid` must be empty or equal to
  `authcid` (ordinal): Surl has no proxy rights to grant, and RFC 4616 section 2 has the server
  fail an identity it will not let `authcid` act as. Any other shape is refused as a bad
  credential with no user in the note (ADR-0038 section 6).
- **`LOGIN`**: challenges `Username:` then `Password:` (base64 `VXNlcm5hbWU6`,
  `UGFzc3dvcmQ6`), only `Password:` when the initial response gave the user name. Both UTF-8.
- **`XOAUTH2`**: without an initial response, one empty challenge. The response is
  `user=<name>\x01auth=Bearer <token>\x01\x01`. **`OAUTHBEARER`**: the same, then
  `n,[a=<authzid>],\x01` followed by `key=value\x01` pairs, one of them `auth=Bearer <token>`,
  and a final `\x01` (RFC 7628 section 3.1). For both, the token is checked against the
  empty-name accounts exactly as HTTP Bearer is (ADR-0032 sections 1 and 3); the user name and
  `a=` are not matched, since a token account has no name. A refused token is answered, after
  the 1-second delay, with one error challenge `{"status":"invalid_token"}` (RFC 7628 section
  3.2.2; `XOAUTH2` servers answer the same way); whatever the client sends next ends it with
  `RefusedCredentials`, not delayed again. Measured, curl's `OAUTHBEARER` sends `\x01` and
  `XOAUTH2` hangs up; both exit 67.
- **`CRAM-MD5`**: the challenge is `<` 16 lower-case hex digits of random `.` the Unix time in
  seconds `@surl>` (RFC 2195's `msg-id` form; `surl` rather than a host name, ADR-0006 section 3).
  The response is `<user> SP <32 lower-case hex digits>`, HMAC-MD5 of the challenge keyed by the
  password's UTF-8 bytes, compared case-insensitively as hex. An initial response is refused as
  a bad credential (the server speaks first).
- **`DIGEST-MD5`**: the challenge is
  `realm="surl",nonce="<base64 of 16 random bytes>",qop="auth",charset=utf-8,algorithm=md5-sess`,
  measured above to be answered by curl. The response's `username`, `nonce`, `cnonce`, `nc`,
  `qop`, `digest-uri` and `response` are required; `nonce` must be the one just issued, `nc`
  `00000001`, `qop` `auth`; `realm` must be empty or `surl` and is hashed **as sent** (curl sends
  it empty); `digest-uri` is hashed as sent and not checked, since a server listening on any
  address cannot know the host name the client used; `authzid`, if present, must equal
  `username`. The response is RFC 2831 section 2.1.2.1's, with `md5-sess`. When it matches, the
  server sends the continuation `rspauth=<hex>` (section 2.1.3, `A2` = `:` + `digest-uri`) and
  accepts on the client's empty answer; a non-empty answer is refused. One nonce per exchange,
  never reused. An initial response is refused as a bad credential.
- **`NTLM`**: an empty challenge when no initial response was sent; the type 1 message answered
  with ADR-0039's type 2 message, and the type 3 message checked by ADR-0039's NTLMv2 check,
  reusing `NtlmHandshake` and its messages as they are (BL-196). The exchange object holds the
  handshake, so it dies with the `AUTH` command, as the HTTP one dies with its connection.
- **`EXTERNAL`**: decision 4. Without an initial response, one empty challenge; the response is
  the authorization identity, possibly empty.
- **`APOP`**: the greeting's timestamp has `CRAM-MD5`'s form. The digest is 32 hex digits of
  MD5 over the timestamp's bytes then the password's UTF-8 bytes, compared case-insensitively as
  hex. The server passes the timestamp its greeting carried.
- **Every comparison** of a secret or a value computed from one is
  `CryptographicOperations.FixedTimeEquals`, an unknown user costs the same as a wrong secret,
  and "no such user", "wrong secret" and "no accounts" answer the same (ADR-0032 section 8).
- **`--allow-anonymous`**: every mechanism still runs its steps, so curl completes the exchange
  it started (measured: a final success where curl expects a continuation is exit 67), and ends
  `AcceptedUnchecked` without checking anything: `PLAIN`, `XOAUTH2`, `OAUTHBEARER` and
  `EXTERNAL` accept their first response, `LOGIN` its second, `CRAM-MD5` its one response,
  `DIGEST-MD5` its empty answer after an `rspauth` computed over nothing (any value; curl does
  not check it), `NTLM` its type 3 message. A plain-text mechanism is not refused under
  `--allow-anonymous`, as HTTP Basic is not (ADR-0032 section 4, step 1); it is still offered
  only as decision 2 says.

### 6. The contract

All in `Surl.Protocol.Abstractions.UnitLibrary`, namespace `Surl.Protocol.Abstractions`, one
public type per file, shared-framework types only; BL-193 adds them. It is **a new interface
beside `IAuthenticationPolicy`**, so no existing implementer changes: `IAuthenticationPolicy` is
untouched, and so are `Surl.Authentication`'s `AuthenticationPolicy` and the test doubles in
`Surl.Protocol.Http.UnitTests` and `Surl.Protocol.Mqtt.UnitTests` that implement it.
`AnonymousAuthenticationPolicy` additionally implements the new interface (BL-193), and
`AuthenticationPolicy` does once BL-194 lands. `Surl.Console` passes the same object to the mail
servers as both interfaces.

```csharp
public interface IMailAuthenticationPolicy
{
    // What a mail server advertises on a connection in this TLS state (decision 2).
    MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession);

    // Starts one AUTH (IMAP: AUTHENTICATE) exchange; it lives until the command ends.
    ISaslExchange StartSaslExchange(SaslExchangeStart start);

    // POP3 APOP: one step, never a Challenge.
    ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken);
}

public sealed record MailLoginOffer(
    IReadOnlyList<string> SaslMechanisms,   // registered names, upper case, in decision 2's order; empty when none
    bool IsClearPasswordLoginOffered,       // IMAP LOGIN (else LOGINDISABLED), POP3 USER/PASS
    bool IsApopOffered);                    // POP3: the greeting carries a timestamp

public sealed record SaslExchangeStart(
    string Scheme,                          // the listen URL's scheme, for the log
    string Mechanism,                       // as the client named it; matched case-insensitively
    ReadOnlyMemory<byte>? InitialResponse,  // decoded; null when none was sent, empty for "="
    TlsSession? TlsSession);                // IConnection.TlsSession now: null means unencrypted

public interface ISaslExchange
{
    // The first step: judges the initial response, or issues the first challenge.
    ValueTask<MailLoginStep> BeginAsync(CancellationToken cancellationToken);

    // Each later step, with the client's decoded response. Throws InvalidOperationException
    // after a step that was not a Challenge.
    ValueTask<MailLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken);
}

public sealed record ApopLogin(
    string Scheme,
    string UserName,                        // as sent
    string Timestamp,                       // the one this connection's greeting carried, brackets included
    string Digest,                          // as sent
    TlsSession? TlsSession);

public enum MailLoginOutcome
{
    Challenge,              // send Challenge as a continuation and read the next response
    Accepted,               // checked, and matches an account
    AcceptedUnchecked,      // --allow-anonymous (ADR-0038)
    RefusedCredentials,     // checked, and refused, after the 1-second delay
    RefusedPlaintext,       // a plain-text mechanism without TLS or --allow-plaintext-auth; nothing read
    RefusedMechanism,       // unknown, not accepted, or not offered on this connection
}

public sealed record MailLoginStep(
    MailLoginOutcome Outcome,
    ReadOnlyMemory<byte> Challenge,         // the continuation's bytes before base64; empty unless Challenge
    string? AccountName,                    // the account logged in when Accepted; otherwise null
    CheckedLogin? CheckedLogin);            // the login note to write; null when nothing was checked
```

- **The server owns the framing**: base64 in and out (`Convert.TryFromBase64String`, one BCL
  call, so no shared helper is needed), `=` as an empty initial response (RFC 4954, 4959, 5034),
  `*` as the client cancelling (the exchange is dropped without calling it again), the line
  limits, and when `AUTH` is allowed at all (after `EHLO`, not twice, not during a transaction).
  The policy owns every mechanism: which are offered, what a challenge holds, what a response
  means and the delay.
- **The note** travels on the step, as on `HttpAuthenticationVerdict` (ADR-0038 section 2): the
  server writes `CheckedLogin.Note` to its exchange log whenever it is not `null`, before it
  answers. It is set on the step that decided the credentials: `Accepted` or
  `RefusedCredentials`, `DIGEST-MD5`'s final `Accepted` (not the `rspauth` challenge), and the
  bearer mechanisms' error challenge (not the refusal after it).
- **`AnonymousAuthenticationPolicy`** (the test double and the pre-composition default) offers
  `PLAIN` and the clear-password login and not `APOP`; its exchange for any mechanism accepts the
  initial response when one was sent and otherwise sends one empty challenge and accepts
  whatever answers it, `AcceptedUnchecked` with no note, and `CheckApopLoginAsync` is
  `AcceptedUnchecked`. That is the fewest steps with which curl completes a login it chose
  (measured: `PLAIN` with and without `--sasl-ir`); BL-193's "in one step" is this.

### 7. Failures in each protocol's words

| Step | SMTP (RFC 4954 section 6, RFC 3463) | IMAP (RFC 3501, RFC 5530) | POP3 (RFC 5034, RFC 3206) |
| --- | --- | --- | --- |
| `Challenge` | `334 <base64>` (`334 ` when empty) | `+ <base64>` (`+ ` when empty) | `+ <base64>` (`+ ` when empty) |
| `Accepted`, `AcceptedUnchecked` | `235 2.7.0 Authentication successful` | `<tag> OK AUTHENTICATE completed` | `+OK Authentication successful` |
| `RefusedCredentials` | `535 5.7.8 Authentication credentials invalid` | `<tag> NO [AUTHENTICATIONFAILED] Authentication failed` | `-ERR [AUTH] Authentication failed` |
| `RefusedPlaintext` | `538 5.7.11 Encryption required for requested authentication mechanism` | `<tag> NO [PRIVACYREQUIRED] Encryption required` | `-ERR [AUTH] Encryption required` |
| `RefusedMechanism` | `504 5.5.4 Unrecognized authentication type` | `<tag> NO Unsupported authentication mechanism` | `-ERR Unsupported authentication mechanism` |
| The client sends `*` | `501 5.7.0 Authentication cancelled` | `<tag> BAD Authentication cancelled` | `-ERR Authentication cancelled` |
| A response that is not base64 | `501 5.5.2 Cannot decode response` | `<tag> BAD Cannot decode response` | `-ERR Cannot decode response` |

- IMAP `LOGIN` and POP3 `USER`/`PASS` answer `CheckPasswordLoginAsync`'s verdicts in the same
  words: `RefusedCredentials` and `RefusedAnonymous` as `RefusedCredentials` above,
  `RefusedPlaintext` as above (POP3 answers `USER` itself that way when the offer has no clear
  password, so the password is never sent), and success `<tag> OK LOGIN completed` or
  `+OK Logged in`. `APOP` answers `CheckApopLoginAsync` as `AUTH` does, and `RefusedMechanism`
  when `APOP` was not offered.
- **`534` is not used**: a mechanism the server does not accept is not offered, and one started
  anyway is `504`, the same answer as an unknown one, so nothing tells a peer how `--auth` is
  set.
- Every row is exit 67 for curl, measured above; none makes curl try another mechanism, so the
  order of decision 2 and the default set decide which mechanism a curl client uses.
- **The delay**: `RefusedCredentials` is decided after ADR-0032 section 8's fixed 1-second delay,
  waited on the injected `TimeProvider` inside `Surl.Authentication` and cancelled with the
  exchange's token. A challenge, `RefusedPlaintext`, `RefusedMechanism`, a cancel and an
  undecodable response are not delayed: nothing was checked.
- **The `CheckedLogin` words** (ADR-0038): the method is the mechanism's registered name in
  capitals as on the wire (`PLAIN`, `LOGIN`, `CRAM-MD5`, `DIGEST-MD5`, `NTLM`, `XOAUTH2`,
  `OAUTHBEARER`, `EXTERNAL`, `GSSAPI`) or `APOP`, the word a reader finds on the bytes-received
  line above the note, as HTTP's is. The user is `PLAIN`'s `authcid`, `LOGIN`'s user name,
  `CRAM-MD5`'s user, `DIGEST-MD5`'s `username`, NTLM's as ADR-0038 names it for HTTP, `APOP`'s
  name, `EXTERNAL`'s certificate name, and `CheckedLogin.BearerTokenUser` for the two bearer
  mechanisms; a user that cannot be read is left out (ADR-0038 section 6). IMAP `LOGIN` and POP3
  `USER`/`PASS` keep ADR-0038 section 3's scheme as the method.

### 8. Who builds what

| Work | Task |
| --- | --- |
| The decision 6 contract, `AnonymousAuthenticationPolicy`'s implementation | BL-193 |
| `PLAIN`, `LOGIN`, `XOAUTH2`, `OAUTHBEARER`, the offer (decision 2) | BL-194 |
| `CRAM-MD5`, `DIGEST-MD5`, `APOP` | BL-195 |
| `NTLM` | BL-196 |
| The `--auth` words, default set, order, help, manual and AI help | BL-197 |
| `EXTERNAL`, then `external` joins the default set | BL-216 (filed by this task; done) |
| How Surl holds a Kerberos key and checks a ticket, for Negotiate and `GSSAPI` | BL-217 (filed by this task; done) |
| SASL `GSSAPI` | BL-218 (filed by this task; done, with `--keytab` from BL-240; see Amendment 2) |
| Offering, framing and answering the logins | the SMTP, IMAP and POP3 servers' login tasks (BL-200, BL-204, BL-206) |

## Alternatives considered

- **Mapping the SASL mechanisms onto the HTTP words** (`basic` for `PLAIN` and `LOGIN`, `bearer`
  for the two bearer mechanisms, `digest` for `CRAM-MD5` and `DIGEST-MD5`). Fewer words, but a
  word would then name different mechanisms with different weaknesses (`digest` would switch on a
  Historic mechanism with an RFC one), and an operator could not accept `PLAIN` without HTTP
  Basic. The SASL names are the ones curl's `AUTH=` uses; one concept, one name.
- **One word per protocol's login** (`smtp-auth`, `imap-auth`): says nothing about what is
  proved, and the plain-text rule is per mechanism, not per protocol.
- **Adding the members to `IAuthenticationPolicy`**: every existing implementer and test double
  would change, and BL-193 would have to touch `Surl.Authentication` and two server test projects.
- **Offering `DIGEST-MD5` and `APOP` by default**: curl picks `DIGEST-MD5` first whenever it is
  offered, so the default would make every curl login use a Historic mechanism.
- **Refusing a bad bearer token with the final failure at once**, without the error challenge:
  curl exits 67 either way, but RFC 7628 section 3.2.2 requires the challenge for `OAUTHBEARER`,
  and a faithful mate sends it.
- **Checking `DIGEST-MD5`'s realm against `surl`**: curl sends an empty realm whichever is
  offered, so that check would refuse every curl login.
- **Leaving `GSSAPI` and `EXTERNAL` out**: the root `CLAUDE.md` rules it out; each has its task.

## Consequences

- `curl -u user:secret smtp://...`, `imap://...` and `pop3://...` log in to a default Surl with
  `CRAM-MD5`, over TLS or not, and never send the password in clear; `--login-options AUTH=PLAIN`
  and the rest work over TLS, or in clear with `--allow-plaintext-auth`.
- `Record-CurlExchange.ps1` gains `-SaslChallenge`, described in its help, for BL-194 to BL-196's
  fixtures and the servers' conformance.
- `--auth`'s default set, table and warning order change (BL-197), and `Requirements.md`'s FR-046
  and FR-008 rows read this ADR when those tasks land.

## Amendment 1 - `EXTERNAL` without a client certificate, and help's long `--auth` default (BL-216, recorded by BL-235, 2026-09-30)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29, in
BL-216, and recorded here by BL-235 because BL-155 held this folder while BL-216 ran.

1. **A client that sends `AUTH EXTERNAL` on a connection with no TLS client certificate** gets
   `RefusedMechanism` from `AuthenticationPolicy.StartSaslExchange`: at once, with no
   `RefusalDelay` and no login note, and even under `--allow-anonymous`
   (`AuthenticationPolicy.CanIdentifyClient`). Why: decision 2 offers `EXTERNAL` only on a
   connection whose `TlsSession.ClientCertificate` is not `null`, so there the mechanism is one the
   server did not offer, and a mechanism not offered is refused as a mechanism (decision 7), not as
   credentials. There is also no client identity for `EXTERNAL` to accept - its identity *is* the
   certificate - so `--allow-anonymous`, which accepts whatever identity a login names, has nothing
   to accept; answering `RefusedCredentials` after the delay would claim a check that never ran.
   `ExternalSaslMechanismTests.NoClientCertificate_IsRefusedAsAMechanismUndelayedAndNotOffered`
   (`Surl.Authentication.UnitTests`) pins it.
2. **Help wraps `--auth`'s default within 79 columns** ([ADR-0034](ADR-0034-curl-style-help-categories-and-the-manual.md)
   decision 3's width). With `external` in it the default
   `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,external,aws-sigv4` is one word
   too long for any eight-space-indented paragraph line, and ADR-0034 put such a word alone on a
   line past 79 columns. `HelpLayout.WrapParagraph` now breaks a word too long for any line after
   each of its commas and wraps the pieces greedily, so the `Default:` paragraph stays within 79
   columns and every piece still reads as part of the list. A word that fits a line is never
   broken, so no other page changes. Why: ADR-0034 fixes the width at 79 so a page is one text a
   test can pin and every line fits curl's columns; breaking at the list's own separators keeps
   that without inventing a hyphenation rule.
   `HelpTextTests.Answer_Auth_IsItsPageWithItsDefaultWrappedAndItsExplanation`
   (`Surl.Cli.UnitTests`) pins it. ADR-0034 decision 3 states the same rule.

## Amendment 2 - `--auth gssapi` is available and needs only `--keytab` (BL-218 and BL-240, recorded by BL-273, 2026-09-30)

Recorded by BL-273, 2026-09-30. BL-218 built SASL `GSSAPI` and BL-240 added `--keytab`, as
[ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
decisions 1 and 9 decide; this amendment brings decisions 1 to 4 above up to date with them.

1. **`--auth gssapi` is available.** It is no longer refused as not available: a SASL `GSSAPI`
   login checks the client's Kerberos ticket against the `--keytab` keys (ADR-0057 decision 9).
2. **It needs `--keytab`.** A start whose `--auth` names `gssapi` without `--keytab` is refused
   before anything else is checked, writing `surl: (2) --auth gssapi needs --keytab` and exiting
   2 (`FailedInit`): `CommandLineRunner.FindOptionRefusal`, through
   `KeytabComposition.IsGssapiWithoutKeytab` (ADR-0057 decision 1).
3. **It is not in the default set**, which stays
   `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,external,aws-sigv4`
   (`CommandLineOptions`, `--auth`'s `Default`), because it needs `--keytab`.
4. **`GSSAPI` is offered first** when `gssapi` is accepted, on any connection, TLS or not: it
   sends no clear secret (`SaslMechanism.InOfferOrder` puts it first, and
   `AuthenticationPolicy.GetMailLoginOffer` offers it whenever the `--keytab` acceptor is
   composed, which point 2 of this amendment guarantees).
5. **`surl: (2) --auth <word> is not available in this build` covers no `--auth` word** any more.
   The refusal remains only for an option this build does not serve yet
   (`CommandLineRunner.FindUnavailableOption`).
