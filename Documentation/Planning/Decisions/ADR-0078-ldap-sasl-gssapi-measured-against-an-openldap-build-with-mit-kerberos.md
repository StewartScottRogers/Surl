# ADR-0078 — LDAP SASL `GSSAPI` measured against an OpenLDAP build with MIT Kerberos

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-10-01,
  in BL-342 (FR-049).
- **Amends:** [ADR-0076](ADR-0076-an-openldap-upstream-curl-8-21-0-build-built-reproducibly-in-ci-for-ldap-and-ldaps.md)
  decision 1 (the build gains MIT Kerberos's GSS-API and is re-pinned; decision 3 below).
  **Confirms, unchanged:** [ADR-0072](ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md)
  decision 4's `GSSAPI` line.

## Context

ADR-0072 decision 4 has surl's last `GSSAPI` challenge offer "no security layer" only (bit 1, max
size 0) and left it to a measurement of `lib/openldap.c` to show whether upstream curl asks for
more. The build ADR-0076 pinned has no GSS-API (`AUTH=GSSAPI` ends 67 without a bind), so BL-342
needs a build that has it, and a way to give that build a ticket.

**The approval.** Stewart's approval in BL-282 covers "an unpatched tag `curl-8_21_0` build whose
`ldap` and `ldaps` run over OpenLDAP ... by building the tag from source in CI" (ADR-0076). A build
of the same tag, from the same verified tarball and recipe, that also links MIT Kerberos is still
that: no binary is downloaded, curl is not patched, and Kerberos is the GSS-API library curl's own
`configure --with-gssapi` takes. So it is within that approval, and no new question goes to him.

**What was built and checked, 2026-10-01:**

- One more source, pinned by SHA-256 in `Build-OpenLdapUpstreamCurl.ps1`: `krb5-1.22.2.tar.gz`, the
  newest MIT Kerberos release, from `https://kerberos.org/dist/krb5/1.22/`
  (`3243FFBC8EA4D4AC22DDC7DD2A1DC54C57874C40648B60FF97009763554EAF13`; its `.asc` verified with
  `gpg --verify`: "Good signature from Greg Hudson <ghudson@mit.edu>", primary key fingerprint
  `C449 3CB7 39F4 A89F 9852 CBC2 0CBA 0857 5F83 72DF`).
- In the same digest-pinned Alpine 3.22 container and package versions, the script first builds
  krb5's libraries only (`util`, `include`, `lib`, `build-tools`: the rest needs `yacc`, which is not
  pinned and curl links none of it) as static libraries with its built-in crypto
  (`--with-crypto-impl=builtin --with-tls-impl=no --disable-pkinit`, no keyutils, LDAP, readline or
  system libverto), then configures curl with `--with-gssapi=/build/prefix` and `-lkrb5support`
  added to `LIBS` (`krb5-config` leaves it out, and a static link needs it).
- Built three times from clean on Docker Desktop 29.6.1: the same SHA-256 every time,
  `62061C585462734FC83361FA873CD6893A7A7D66E8739FD9B230FF9C51237E55`. Its `curl -V`:

  ```
  curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.9 mit-krb5/1.22.2 OpenLDAP/2.6.15
  Release-Date: 2026-06-24
  Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp smtp smtps telnet tftp ws wss
  Features: alt-svc AsynchDNS GSS-API HSTS HTTPS-proxy IPv6 Kerberos Largefile NTLM SPNEGO SSL threadsafe TLS-SRP UnixSockets
  ```

## Decision

### 1. How it was measured

`Record-CurlExchange.ps1 -Ldap -KerberosTestKdc -LdapKerberosAcceptor` (ADR-0065, BL-327) ran in a
container of `mcr.microsoft.com/dotnet/sdk:10.0` (Ubuntu 24.04, .NET SDK 10.0.401, PowerShell
7.6.6, root, so the hand-built test KDC binds port 88), with `-Curl` naming the pinned file and
`-CurlTimeoutMilliseconds 20000`. Three extensions made this possible, all in measuring fixtures:

- **`-KerberosTestKdc` runs outside Windows.** MIT's GSS-API takes no password from `-u`; it reads
  a credential cache. So there the script writes `krb5.conf` (realm `SURL.TEST`, its KDC
  `127.0.0.1:88` over TCP through `udp_preference_limit = 1`, `.surl.test` mapped to `SURL.TEST`, no
  DNS and no reverse lookup), runs `kinit tester@SURL.TEST` from `PATH` (Ubuntu's `krb5-user`, only
  to fill the cache) and runs curl with `KRB5_CONFIG` and `KRB5CCNAME` naming them.
- **`Run-KerberosAcceptor.cs` gains `gssapi`** (a bare Kerberos `InitialContextToken`, through
  `KerberosAcceptor.Accept`) and **`sign`** (an RFC 4121 wrap token that is never sealed).
- **`-Ldap` answers a SASL `GSSAPI` bind** (RFC 4752): the first with `saslBindInProgress` (14)
  carrying the AP-REP when curl asked for mutual authentication, otherwise carrying the
  security-layer offer, `-LdapGssapiSecurityLayers` wrapped with integrity only, as RFC 4752 section
  3.1 says; the bind that answers it is unwrapped, decoded and answered `success`.

Every case is `curl -sS --resolve ldap.surl.test:18389:127.0.0.1 --login-options AUTH=GSSAPI -u
tester@SURL.TEST: ldap://ldap.surl.test:18389/dc=example,dc=com` unless stated, the service
principal `ldap/ldap.surl.test`, the root DSE ADR-0076's `R`, entries ADR-0076's `E`. The offer
`07100000` (every layer, 1 MiB):

```
> #1 searchRequest base "" scope 0 ... filter (objectclass=*) attributes [supportedSASLMechanisms]
< #1 searchResultEntry
< #1 searchResultDone 0
> #2 bindRequest version 3 name "" sasl GSSAPI credentials 6082020F06092A864886F71201020201006E8201FE308201FAA003020105A10302010EA20703050000000000...
= Kerberos accepted tester@SURL.TEST, conf
< #2 bindResponse 14
> #3 bindRequest version 3 name "" sasl GSSAPI credentials 050400FF000C0000000000002A1139410100000051F2642356F261F6574C8304
=   unwrapped (signed) to 01000000: layer 0x01, max size 0, authzid ""
< #3 bindResponse 0
> #4 searchRequest base "dc=example,dc=com" scope 0 ... filter (objectclass=*) attributes []
>   30360201046331041164633D6578616D706C652C64633D636F6D0A01000A0100020100020100010100870B6F626A656374636C6173733000
< #4 searchResultEntry
< #4 searchResultDone 0
> #5 unbindRequest
```

Exit 0, the entry on stdout as ADR-0076 printed it; `kdc.log`: the AS exchange (first refused 25,
`PREAUTH_REQUIRED`, then issued, enctype 18) and one TGS for `ldap/ldap.surl.test@SURL.TEST`.

| Case | What curl sent, and how it ended |
| --- | --- |
| offer `07100000` | as above: the AP-REQ (`ap-options` all clear, so **no mutual authentication**; its checksum asks for confidentiality, as MIT's always does), then RFC 4121 wrap token `0504 00 FF ...` (no seal), unwrapped `01 000000`: **no security layer, max size 0, empty authzid**; the search in clear; exit 0 |
| offer `01000000` (no layer only, surl's) | the same answer `01000000`; exit 0 |
| offer `01FFFFFF` | the same answer `01000000` (curl answers max size 0 whatever is offered); exit 0 |
| offer `02100000` (integrity only) or `06100000` (integrity and confidentiality) | `* GSSAPI handshake failure (invalid security layer)`, then a **bind with an empty mechanism and no credentials** (`sasl  no credentials`, RFC 4513 section 5.2.1.2's abort). Answered `success` or `authMethodNotSupported` (7) alike, curl **goes on to the search** as an unbound client; exit 0 |
| `--sasl-authzid admin` | answer `0100000061646D696E`: no layer, max size 0, authzid `admin`; exit 0 |
| `-u 'SURL\tester:'` | the same exchange as with `tester@SURL.TEST` (the ticket is the cache's); exit 0 |
| `-u tester:secret`, or `-u tester:` | **no bind** (curl's SASL takes `GSSAPI` only for a user naming a domain with `@`, `\` or `/`); unbind, exit 67 `Login denied` |
| no `-u` | no bind; unbind, exit 67 |
| `ldap://127.0.0.1:18389/...` (no host name) | the TGS for `ldap/127.0.0.1@SURL.TEST` refused (7, no such principal), no bind; exit 94 `An authentication function returned an error` |
| `AUTH=*` with `-u tester@SURL.TEST:` | **`GSSAPI`** chosen from `R`, the same exchange; exit 0 |
| `AUTH=*` with `-u alice:secret` | `DIGEST-MD5`, as ADR-0076 measured (no domain in the user); answered 0 at once: exit 67 |
| `-u alice:secret` (simple bind) | as ADR-0076: `bindRequest version 3 name "alice" simple "secret"`, the search; exit 0 |

### 2. ADR-0072 decision 4's `GSSAPI` line is confirmed

Upstream curl's `GSSAPI` never asks for a security layer: whatever the offer, its answer selects
"no security layer" with max size 0, and the session goes on in clear. So surl's offer of "no
security layer" only (`01000000`) is exactly what curl answers, and the line stands unchanged;
ADR-0072 gains no "Amended" line. Three things the measurement adds, none of which changes surl:

- **curl never asks for mutual authentication**, so surl's first `GSSAPI` challenge carries the
  security-layer offer, not an AP-REP. surl still sends an AP-REP when a client asks.
- **An offer without the "no layer" bit makes curl abort with an empty mechanism** and then search
  unbound whatever the abort is answered. surl never makes that offer. It answers an empty
  mechanism `authMethodNotSupported` like any mechanism it does not offer (ADR-0072 decision 4,
  `LdapSaslBindJudge`), and RFC 4513 section 5.2.1.2 asks exactly that; the search that follows is
  an anonymous client's, judged as decision 6 says.
- **The authzid** is curl's `--sasl-authzid`, else empty; surl's check (ADR-0057) is unchanged.

### 3. The build replaces ADR-0076's pin

One build per protocol and role (ADR-0030 decision 5): the GSS-API build replaces ADR-0076's at the
same `/opt/upstream-curl/8.21.0-openldap/curl`, as the supplementary `linux-x64` build for `ldap`
and `ldaps` only, re-pinned in `UpstreamCurlBuilds.json` with the `version` and `features` above.
Two builds both listing `ldap` would make `LocateForProtocol` and CI's "exactly one supplementary
ldap pin" ambiguous, and keeping the old one would leave no build that does both halves.

Linking Kerberos changes no byte ADR-0076 recorded but one case: `AUTH=*` with a user that names a
domain now picks `GSSAPI` before `DIGEST-MD5`, which is curl's own preference when it has GSS-API.
Every ADR-0076 case uses `alice`, so none changes; the simple bind and `AUTH=*` with `alice` were
re-run above and match.

### 4. CI

Unchanged in shape: the Linux leg's cache key holds the SHA-256, so the first run after this ADR
misses and builds the new pin (about a minute longer than before, for krb5), then verifies it
before anything runs it, never re-pinning (ADR-0076 decision 3).

`GSSAPI` against `surl` is proved there since BL-351 (2026-10-01): the Linux leg installs Ubuntu's
`krb5-user` for `kinit`, and
`UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry`
serves the test KDC in-process on an ephemeral loopback port (so the runner needs no root for port
88) named in its own `krb5.conf`, the one decision 1 measured with, fills a credential cache with
`kinit`, and has this build bind to `surl --auth gssapi --keytab` and search, exit 0. It found that
surl answered the service `pop` for `ldap`; [ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
Amendment 2 corrects it.

## Consequences

- The OpenLDAP build now measures `GSSAPI` too, and `Record-CurlExchange.ps1 -KerberosTestKdc`
  works on Linux wherever `kinit` is on `PATH` and port 88 can be bound.
- Re-pinning krb5 (a new MIT release) is a new ADR, as for any source of a pinned build.
- The Windows builds' SSPI `GSSAPI` (ADR-0072 Amendment 1's host) is untouched.

## Alternatives considered

- **A second, GSS-only build beside ADR-0076's.** Rejected: two `ldap` pins for one platform and
  role, which the locator and CI would have to tell apart for no gain - the GSS build does
  everything the plain one did, byte for byte in every recorded case.
- **Heimdal instead of MIT Kerberos.** Rejected: MIT is what curl's `configure` and the major
  distributions build against, so it is the GSS-API upstream curl is normally seen with.
- **OpenSSL crypto for krb5.** Rejected: the built-in crypto keeps krb5 independent of the
  OpenSSL version and sends the same Kerberos bytes.
- **Measuring in WSL.** Rejected for this run: WSL has no .NET SDK for the test KDC, and the SDK
  container gives root for port 88 without touching the host.
