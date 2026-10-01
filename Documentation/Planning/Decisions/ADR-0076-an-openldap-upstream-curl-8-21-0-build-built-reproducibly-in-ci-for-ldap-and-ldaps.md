# ADR-0076 — An OpenLDAP upstream curl 8.21.0 build, built reproducibly in CI, for `ldap` and `ldaps`

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-287 (FR-049). Stewart approved obtaining the build on 2026-09-30 (BL-282: "yes OpenLDAP" -
  an unpatched tag `curl-8_21_0` build whose `ldap` and `ldaps` run over OpenLDAP, for Linux and
  macOS if one can be had, by a third-party static build or by building the tag from source in CI).
  Which one, where it lives and how CI obtains it he left to this ADR.
- **Amends:** [ADR-0072](ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md)
  decision 4's `GSSAPI` line (decision 6 below). [ADR-0016](ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md)
  and [ADR-0030](ADR-0030-static-curl-8-21-0-windows-build-as-a-supplementary-build-for-smb.md) are
  applied as written.
- **Amended:** decision 1 by [ADR-0078](ADR-0078-ldap-sasl-gssapi-measured-against-an-openldap-build-with-mit-kerberos.md)
  (BL-342, 2026-10-01): the build also links MIT Kerberos 1.22.2's GSS-API and is re-pinned
  (`62061C58...7E55`) at the same path; every case below that was re-run is unchanged, and the
  `AUTH=GSSAPI` row's "no GSS-API" is now ADR-0078's measured exchange.

## Context

Upstream curl has two LDAP implementations. Every pinned build that lists `ldap` runs `lib/ldap.c`
over Windows' `WinLDAP`; `lib/openldap.c` - what curl on Linux and macOS normally uses, with
`STARTTLS`, the root-DSE `supportedSASLMechanisms` search and SASL binds through curl's own SASL
code - was in no pinned build (BL-282's Context). ADR-0072 decided decisions 4 and 5 for it from
RFC 4511, RFC 4513 and RFC 4422 and left their confirmation to this measurement.

**No published build will do.** Checked 2026-09-30: stunnel/static-curl's 8.21.0 builds (the Linux
and macOS reference builds, ADR-0016) list no `ldap`; distribution packages (Debian's, Ubuntu's -
WSL's `curl 8.18.0 ... OpenLDAP/2.6.10`, "security patched: 8.18.0-1ubuntu2.4") carry patches and are
not 8.21.0. So the build is made from source, which leaves one problem: a build is pinned by its
SHA-256 (ADR-0003), so it must come out the same bytes every time it is built.

**What was built and checked, 2026-09-30:**

- Sources, each pinned by SHA-256: `curl-8.21.0.tar.xz` from `https://curl.se/download/`
  (`AA1B66A70EACE83DC624508745646C08AE561DE512AB403ADFFB93AC87FC72E6`; its `.asc` verified with
  `gpg --verify` against Daniel Stenberg's key from `https://daniel.haxx.se/mykey.asc`: "Good
  signature from Daniel Stenberg"), `openldap-2.6.15.tgz` (the newest 2.6 release on
  openldap.org; `BC91225DBFC50354033B1303BC91D1A7F6DDD1DC32FAC950D79C28FE66D6BCA8`), and
  `openssl-3.5.9.tar.gz` from OpenSSL's GitHub release
  (`603F5602E2EEF00D77FBD429D34DCD5822BB301757A1BC9CDB24C670F1EB859A`, matching the `.sha256`
  published beside it).
- `Build-OpenLdapUpstreamCurl.ps1` (added by this task) builds them in a Docker container of
  `alpine@sha256:5291449c3df73caf6ed85e649dec1b9e818b39a5d8c871e97afc13e9cd5e8fa8` (Alpine 3.22)
  with `binutils=2.44-r3 gcc=14.2.0-r6 linux-headers=6.14.2-r0 make=4.4.1-r3 musl-dev=1.2.5-r12
  perl=5.40.4-r0`: OpenSSL and OpenLDAP's `libldap` and `liblber` as static libraries, then curl
  statically linked against musl and both, stripped, with `SOURCE_DATE_EPOCH` set to curl 8.21.0's
  release date (OpenLDAP's `mkversion` then writes no user, host or path, and GCC's `__DATE__` and
  `__TIME__` are that date) and `-ffile-prefix-map`. Curl is configured with `--enable-ldap
  --enable-ldaps --enable-ntlm` and none of the optional compression, IDN, PSL, HTTP/2 or SSH
  libraries: they change no LDAP byte. `--enable-ntlm` because curl 8.21.0 builds NTLM only when
  asked, and ADR-0072 offers SASL `NTLM`.
- Built twice from clean on Docker Desktop 29.6.1 (Windows 11, WSL 2): the same SHA-256 both times,
  `8D4572E89081E84BDDDB147527523A80D9FAD140FF615F7B379BBCC239892398`, in about two minutes.
  Its `curl -V`, run on WSL's Ubuntu 26.04 (glibc: the binary is static, so it runs anywhere on
  x86-64 Linux):

  ```
  curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.9 OpenLDAP/2.6.15
  Release-Date: 2026-06-24
  Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp smtp smtps telnet tftp ws wss
  Features: alt-svc AsynchDNS HSTS HTTPS-proxy IPv6 Largefile NTLM SSL threadsafe TLS-SRP UnixSockets
  ```

## Decision

### 1. The build

The supplementary `linux-x64` build `Build-OpenLdapUpstreamCurl.ps1` makes, as above, pinned in
`UpstreamCurlBuilds.json` by its SHA-256 with its `version`, `protocols` and `features` lines as
measured, at `/opt/upstream-curl/8.21.0-openldap/curl`, beside the reference build's directory.
Everything that decides its bytes - the image digest, the package versions, the three source
hashes, the configure lines and `SOURCE_DATE_EPOCH` - is written in that one script, so the pin and
its recipe change together or not at all.

### 2. Its role: supplementary, for `ldap` and `ldaps` only

As ADR-0030 decision 5 has it: `UpstreamCurlLocator.Locate` still returns the `linux-x64` reference
build by default (so no existing conformance test changes build), and `LocateForProtocol` returns
this one for `ldap` and `ldaps`, which the reference build does not list. It is never used to
second-guess the reference build for a protocol both have (ADR-0017). Two tests pin this against
the real pin file: `UpstreamCurlLocatorTests.LocateForProtocol_RealPinFileOnLinux_SelectsTheBuildItsAdrNames`
and `Locate_RealPinFileOnLinux_StillReturnsTheReferenceBuild`.

### 3. How CI obtains it

The Linux leg of `.github/workflows/ci.yml` reads the one supplementary `linux-x64` pin that lists
`ldap`, restores its directory from the Actions cache under a key holding its SHA-256, and only on a
miss runs `Build-OpenLdapUpstreamCurl.ps1` (GitHub's Ubuntu runners have Docker). Then, as for every
downloaded build (ADR-0016 decision 4), nothing runs the file until its SHA-256 matches the pin.
A mismatch fails the job and is never re-pinned in CI (ADR-0016 decision 6): it means the build did
not reproduce, and the fix is a new ADR that re-pins it. The same holds when Alpine's 3.22
repository drops a pinned package version: `apk` refuses, the build fails, nothing unpinned runs.

### 4. No macOS or Windows build

GitHub's macOS arm64 runners have no Docker, and a native macOS build depends on whichever Xcode
the runner has, so it could not be pinned by SHA-256 and rebuilt to match. Approval covered macOS
"if one can be had"; it cannot, reproducibly. Windows needs none: its builds already run
`lib/ldap.c`, which ADR-0072 measured. `lib/openldap.c` is the same C on every platform, so one
platform proves it.

### 5. How it was measured

`Record-CurlExchange.ps1 -Ldap` (ADR-0072) gains StartTLS: an `extendedRequest` for
`1.3.6.1.4.1.1466.20037` answered success through `-LdapReply 'EXTENDED=0'` carries the OID as
`responseName` and switches the connection to TLS 1.2 with the throwaway certificate; and its
throwaway certificate's key is the platform's RSA outside Windows (`RSACng` is Windows only). The
script ran under PowerShell 7.6.6 in WSL's Ubuntu 26.04 with `-Curl` naming the pinned file (the
script refuses any other), `-CurlTimeoutMilliseconds 20000`, every case `curl -sS <args>`. `E` and
`U` are ADR-0072's (`dn: cn=alice,dc=example,dc=com`, `objectClass: person`, `cn: alice`, `sn:
Smith`, `mail: alice@example.com`; `ldap://127.0.0.1:18389/dc=example,dc=com`); `R` is a root DSE
listing `GSSAPI GSS-SPNEGO DIGEST-MD5 CRAM-MD5 NTLM OAUTHBEARER XOAUTH2 PLAIN LOGIN EXTERNAL`.

**Every run opens one connection**, and every message uses BER's short length forms. A simple bind
and search (`-u alice:secret U`, entries `E`):

```
= connection 1 accepted
> #1 bindRequest version 3 name "alice" simple "secret"
>   301702010160120201030405616C6963658006736563726574
< #1 bindResponse 0
> #2 searchRequest base "dc=example,dc=com" scope 0 deref 0 sizeLimit 0 timeLimit 0 typesOnly 00 filter (objectclass=*) attributes []
>   30360201026331041164633D6578616D706C652C64633D636F6D0A01000A0100020100020100010100870B6F626A656374636C6173733000
< #2 searchResultEntry
< #2 searchResultDone 0
> #3 unbindRequest
>   30050201034200
```

Exit 0, stdout byte for byte the `WinLDAP` build's plus one more `\n` at the end:
`DN: cn=alice,dc=example,dc=com\n\tobjectClass: person\n\n\tcn: alice\n\n\tsn: Smith\n\n\tmail: alice@example.com\n\n\n`.
The filter is `(objectclass=*)`, lower case (`WinLDAP`: `(ObjectClass=*)`).

| Case | What curl sent, and how it ended |
| --- | --- |
| no `-u`, entries `E` | `bindRequest version 3 name "" simple ""`, then the search: **an anonymous bind** (the `WinLDAP` build never binds anonymously); exit 0 |
| `-u alice:wrong`, bind answered 49 | the bind once, **no version 2 retry**, then unbind; exit 67 `curl: (67) Login denied` |
| bind answered 13 | unbind; exit 38 `curl: (38) LDAP: cannot bind` |
| ADR-0072's filter case | scope 2, attributes `[cn, mail]`, the filter as written (`A048...`); exit 0 |
| `dc=nowhere`, search answered 32 | exit 39 `curl: (39) LDAP remote: search failed No such object` |
| search answered 4 after one entry | exit 0, the entry printed |
| search answered 3 after one entry | exit 39 `... search failed Time limit exceeded`, **the entry printed** (`WinLDAP`: nothing printed) |
| ADR-0072's multi-valued and binary entry | the same lines as `WinLDAP`'s: `;binary`, non-printable and non-ASCII values as `:: ` and base64 |
| `--ssl-reqd U`, StartTLS answered 2 (`protocolError`) | `extendedRequest 1.3.6.1.4.1.1466.20037` (`301D02010177188016312E332E362E312E342E312E313436362E3230303337`) first, then unbind; exit 1 `curl: (1) Unsupported protocol` |
| `--ssl-reqd U`, StartTLS answered 53 (`unwillingToPerform`) | unbind; exit 64 `curl: (64) Requested SSL level failed` |
| `--ssl U`, StartTLS answered 2 | carries on in clear: **`bindRequest version 2`**, then the search; exit 0. Answering that version 2 bind 2 too: exit 1 `Unsupported protocol` |
| `--ssl U`, StartTLS answered 53 | carries on in clear with a version 3 bind; exit 0 |
| `-k --ssl-reqd U`, StartTLS answered 0 with `responseName` | `= TLS handshake completed`, then the bind (version 3) and search inside TLS; exit 0 |
| `-k ldaps://...` (`-Tls`) | one connection, TLS from the first byte, the same exchange; exit 0 |
| `ldaps://...` without `-k` | exit 60 `curl: (60) SSL certificate OpenSSL verify result: self-signed certificate (18)` |

**SASL.** With `--login-options AUTH=<mech>`, `--ntlm`, `--digest` or `--oauth2-bearer` (and never
for a plain `-u user:password`), curl first reads the root DSE, then binds with name `""`:

```
> #1 searchRequest base "" scope 0 deref 0 sizeLimit 0 timeLimit 0 typesOnly 00 filter (objectclass=*) attributes [supportedSASLMechanisms]
>   303E020101633904000A01000A0100020100020100010100870B6F626A656374636C61737330190417737570706F727465645341534C4D656368616E69736D73
```

| Case (root DSE `R` unless stated) | What curl sent, and how it ended |
| --- | --- |
| `AUTH=PLAIN -u alice:secret`, with or without `--sasl-ir` | `sasl PLAIN credentials 00616C69636500736563726574` (always an initial response); bind 0: exit 0. Bind 49: unbind, exit 67 `Login denied` |
| `AUTH=LOGIN` | `sasl LOGIN credentials 616C696365` (the user as initial response), on 14 `sasl LOGIN credentials 736563726574` (the password, whatever the challenge says), on 0 the search: exit 0. A 14 after the password: unbind, exit 67 |
| `AUTH=CRAM-MD5` | `sasl CRAM-MD5 no credentials`, on 14 with `<1896.697170952@surl>` the RFC 2195 answer `alice f637fa40168e63f97fff8498c75a5ea2`, on 0: exit 0 |
| `AUTH=DIGEST-MD5`, or `--digest` | `sasl DIGEST-MD5 no credentials`; answered 0 at once: exit 67. On 14 with ADR-0072's LDAP challenge (`qop="auth,auth-int,auth-conf",cipher="3des,rc4"`): `username="alice",realm="surl",nonce="...",cnonce="...",nc="00000001",digest-uri="ldap/127.0.0.1",response=...,qop=auth` - **`qop=auth` whatever is offered: no security layer**; then `bindResponse 0` with `rspauth=<hex>` as `serverSaslCreds`, or with none: exit 0. `rspauth` sent as a further 14 instead: unbind, exit 67 |
| `AUTH=NTLM`, or `--ntlm` | `sasl NTLM credentials` holding a `NEGOTIATE_MESSAGE`, flags `00088206` (no sign, no seal); on 14 with a `CHALLENGE_MESSAGE` (flags `E2088205`) an NTLMv2 `AUTHENTICATE_MESSAGE` (user `alice`, empty domain, workstation `WORKSTATION`) **carrying the challenge's flags unchanged**; on 0 the search **in clear**: exit 0. Answered 0 at once: exit 67 |
| `--oauth2-bearer tok123 -u alice:` | `sasl OAUTHBEARER credentials` `n,a=alice,\x01host=127.0.0.1\x01port=18389\x01auth=Bearer tok123\x01\x01`; on 0: exit 0 |
| the same with `AUTH=XOAUTH2` | `sasl XOAUTH2 credentials` `user=alice\x01auth=Bearer tok123\x01\x01`; on 0: exit 0 |
| `AUTH=EXTERNAL -u alice:` | `sasl EXTERNAL credentials 616C696365` (the user as authorization identity); on 0: exit 0 |
| `AUTH=*` | curl's own preference among those listed: `DIGEST-MD5` from `R`; `PLAIN` from a root DSE listing only `PLAIN` |
| `AUTH=GSSAPI` | no bind: this build has no GSS-API; unbind, exit 67 |
| `AUTH=PLAIN`, the root DSE listing no mechanism | no bind; unbind, exit 67 |
| `-k --ssl-reqd AUTH=PLAIN`, StartTLS answered 0 | StartTLS, the handshake, then the root-DSE search and the bind inside TLS; exit 0 |

### 6. What this means for ADR-0072

**Confirmed, unchanged:** decision 3's root DSE (it must list `supportedSASLMechanisms` when asked
by name, or curl gives up 67 without binding) and its version 2 rule (`--ssl` against a refused
StartTLS binds as version 2, and the session must go on); decision 4's mechanism list, its LDAP
`DIGEST-MD5` challenge (curl answers it with `qop=auth`, so no security layer, and accepts
`rspauth` on the final `success`), and its NTLM rule (the `CHALLENGE_MESSAGE` grants only what the
`NEGOTIATE_MESSAGE` asked for, so curl's `00088206` gets no signing or sealing, and the echoed
`AUTHENTICATE_MESSAGE` flags select no security layer - which is right, since curl's SASL has
none); decision 5's StartTLS (offered with a certificate; `protocolError` when not offered, RFC 4511
section 4.14.2's answer for a server that does not support TLS - curl then goes on in clear under
`--ssl` and stops with 1 under `--ssl-reqd`).

**Amended:** decision 4's `GSSAPI` line said "BL-287 measures what the OpenLDAP build accepts and
amends this if it asks for more". This build has no GSS-API, so that stays unmeasured; the line
stands as decided (no security layer offered) and BL-342 measures it against a build that has
GSS-API.

**For BL-312**, the cases and exit codes above are what the OpenLDAP build must reach against
`surl`: in particular 67 (not 38) for a refused password, the anonymous bind without `-u`, and the
exit 1 and 64 StartTLS refusals.

## Consequences

- `ldap` and `ldaps` gain the half of upstream curl's LDAP no build exercised: StartTLS, SASL, and
  the anonymous bind. BL-312 proves them on CI's Linux leg; elsewhere its tests report Inconclusive.
- CI's Linux leg spends about two minutes building the pin when the cache misses, once per SHA-256.
- Surl now builds an upstream curl itself. The rule that keeps that honest is the same as for a
  download: a SHA-256 fixed in the repository, checked before every run, never re-pinned by CI.
- Re-pinning (a new OpenLDAP or OpenSSL release, an Alpine package that left the repository) is a
  new ADR, as for any pin.

## Alternatives considered

- **A distribution's curl package** (Debian, Ubuntu, Alpine). Rejected: patched, and no 8.21.0.
- **Building on the runner without a container.** Rejected: the runner image's compiler and
  libraries change under it, so the bytes would not reproduce.
- **Building once and keeping the binary** as a release asset or in the repository. Rejected: a
  release needs Stewart's word, and a binary in git is a large blob nobody can review; the recipe
  plus the hash is reviewable and rebuilds the same file.
- **OpenSSL 4.0.1, as the reference builds use.** Rejected: OpenLDAP 2.6 is not yet known to build
  against OpenSSL 4, and the TLS library sends no LDAP byte; 3.5 is OpenSSL's long-term release.
