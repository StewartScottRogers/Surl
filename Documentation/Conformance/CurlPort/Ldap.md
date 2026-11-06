# Phase 7 findings: LDAP and LDAPS

What `CurlPortBesideUpstreamCurlOverLdapTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every LDAP and LDAPS conversation of `UpstreamCurlSearchesSurlOverLdapTests`,
`UpstreamCurlBindsToSurlOverLdapWithKerberosTests` and `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`
through pinned upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-586).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl, Windows | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build; `lib/ldap.c`) |
| Upstream curl, Linux | SHA-256 `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480`, `curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.9 mit-krb5/1.22.2 OpenLDAP/2.6.15` (ADR-0076's OpenLDAP build; `lib/openldap.c`). Paired with the `OpenLdap_` tests; not run, as below |
| Surl | commit `00351846bd295957d066acd7ac6a9faa68c78e84` |
| Platform | Windows (`win-x64`) |
| Date | 2026-11-05 |

## Result

On Windows, 22 cases ran against the `WinLDAP` build: 3 agreements, 19 port defects, no Surl defect,
none not comparable. Upstream's run was what the mirrored test asserts in every case.

The 47 `OpenLdap_` cases are Inconclusive on Windows, as their mirrored tests are: no upstream curl linked
against OpenLDAP is pinned for `win-x64`, and no Curl port is pinned for `linux-x64`, where the OpenLDAP
build is (ADR-0089 decisions 2 and 8). They run, with no change, once a `linux-x64` port pin exists, and
their findings then go in a Linux section of this file.

Compared, per ADR-0089 decision 4: exit code, standard output, the standard error line holding the text
the mirrored test pins, `surl`'s `-v` notes (with each exchange's lines grouped by exchange number, since
two connections open at once are noted in whichever order `surl` serves them), and the bytes each client
sent on every `ldap://` connection. Bytes are not compared for `ldaps://`, nor where the bind seals the
conversation or carries a random challenge (`--ntlm`, `--negotiate`, `--digest`, Kerberos).

Where bytes were compared, the LDAP messages the port sends are byte for byte the ones `WinLDAP` sends,
down to its four-byte long-form BER lengths (`30 84 00 00 00 1B …`).

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree. A data-driven case is named `Method[data row]`.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `WinLdap_Search_BaseWithSimpleBind_Exits0WithTheBaseEntry` | `UpstreamCurlSearchesSurlOverLdapTests.Search_BaseWithSimpleBind_Exits0WithTheBaseEntry` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes` | `UpstreamCurlSearchesSurlOverLdapTests.Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_SubtreeWithFilter_Exits0WithAliceOnly` | `UpstreamCurlSearchesSurlOverLdapTests.Search_SubtreeWithFilter_Exits0WithAliceOnly` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_NonAsciiValue_Exits0WithTheValueInBase64` | `UpstreamCurlSearchesSurlOverLdapTests.Search_NonAsciiValue_Exits0WithTheValueInBase64` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_NoEntryMatches_Exits0WithNothingWritten` | `UpstreamCurlSearchesSurlOverLdapTests.Search_NoEntryMatches_Exits0WithNothingWritten` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_BaseNotInTheDirectory_Exits39NoSuchObject` | `UpstreamCurlSearchesSurlOverLdapTests.Search_BaseNotInTheDirectory_Exits39NoSuchObject` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries` | `UpstreamCurlSearchesSurlOverLdapTests.Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Bind_WrongPassword_Exits38InvalidCredentials` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_WrongPassword_Exits38InvalidCredentials` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Bind_NoAccounts_Exits38InvalidCredentials` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_NoAccounts_Exits38InvalidCredentials` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Bind_AllowAnonymous_Exits0WithTheBaseEntry` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_AllowAnonymous_Exits0WithTheBaseEntry` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Search_InMemoryDirectory_Exits39NoSuchObject` | `UpstreamCurlSearchesSurlOverLdapTests.Search_InMemoryDirectory_Exits39NoSuchObject` | [F1](#f1-no-unused-connection-before-winldaps-own) |
| `WinLdap_Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer[ntlm, --ntlm]` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer` | [F2](#f2---ntlm---negotiate-and---digest-bind-simply) |
| `WinLdap_Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer[negotiate, --negotiate]` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer` | [F2](#f2---ntlm---negotiate-and---digest-bind-simply) |
| `WinLdap_Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer[digest-md5, --digest]` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer` | [F2](#f2---ntlm---negotiate-and---digest-bind-simply) |
| `WinLdap_Bind_NtlmWrongPassword_Exits38InvalidCredentials` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_NtlmWrongPassword_Exits38InvalidCredentials` | [F2](#f2---ntlm---negotiate-and---digest-bind-simply) |
| `WinLdap_Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported` | `UpstreamCurlSearchesSurlOverLdapTests.Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported` | [F2](#f2---ntlm---negotiate-and---digest-bind-simply) |
| `WinLdapKerberos_NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer` | `UpstreamCurlBindsToSurlOverLdapWithKerberosTests.NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer` | [F2](#f2---ntlm---negotiate-and---digest-bind-simply) |
| `WinLdap_Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself` | `UpstreamCurlSearchesSurlOverLdapTests.Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself` | [F3](#f3--k-over-ldaps-skips-the-certificate-check-winldap-still-makes) |

The three that agree: `WinLdap_Bind_NoUserTheWindowsUserWithoutAnAccount_Exits38InvalidCredentials`,
`WinLdap_Ldaps_UntrustedCertificate_Exits60` and `WinLdap_Search_SslReqd_Exits4ExplicitTlsNotSupported`.
Upstream opens no unused connection in the first: there both clients open two connections.

### F1: No unused connection before WinLDAP's own

- **Cases:** the twelve rows marked F1, every `ldap://` case with a simple or anonymous bind.
- **Command line, e.g.:** `curl -sS -m 20 -u alice:secret ldap://127.0.0.1:<port>/dc=example,dc=com`.
- **Upstream:** two TCP connections. On connection 0 it sends nothing (0 bytes) and closes it; on
  connection 1 it sends the bind, search and unbind, e.g. 112 bytes starting
  `30 84 00 00 00 1B 02 01 01 60 84 …` for the case above. `surl` notes two exchanges.
- **Port:** one TCP connection, carrying exactly the bytes upstream sends on its connection 1. `surl`
  notes one exchange.
- **First differing request byte:** connection 0, offset 0: upstream's connection ends there, the port
  sends `0x30`. Exit codes, standard output and the pinned standard error line agree.
- **Why upstream does it:** curl 8.21.0's `lib/ldap.c`, the `WinLDAP` backend, does not use the socket curl's
  connect phase opens to the URL's host and port: `ldap_do()` passes `conn->host.name` and the remote port
  to `ldap_init()` (or `ldap_sslinit()` for `ldaps`), and `WinLDAP` opens its own connection. curl's own
  connection stays idle until the transfer ends. `lib/openldap.c` hands curl's socket to OpenLDAP instead,
  so the OpenLDAP build opens one connection. Upstream is right by ADR-0003 decision 6; the port's
  maintainer decides whether to reproduce the idle connection or leave the difference recorded.

### F2: `--ntlm`, `--negotiate` and `--digest` bind simply

- **Cases:** the six rows marked F2.
- **Command line, e.g.:** `curl -sS -m 20 --ntlm -u alice:secret ldap://127.0.0.1:<port>/dc=example,dc=com`
  against `surl --user alice:secret --auth ntlm`.
- **Upstream:** binds with the chosen mechanism through `ldap_bind_s()` (`LDAP_AUTH_NTLM` for `--ntlm`,
  `LDAP_AUTH_NEGOTIATE` for `--negotiate`, `LDAP_AUTH_DIGEST` for `--digest`), searches over the
  security layer and exits 0 with the base entry. For a wrong password it exits 38 with
  `curl: (38) LDAP local: bind via ldap_win_bind Invalid Credentials`. Where `surl` does not accept NTLM it
  exits 38 with `… Authentication Method Not Supported`. With the Kerberos account it exits 0 with
  `DN: cn=alice,dc=example,dc=com`.
- **Port:** sends a simple bind with the plain-text password whatever the option, which `surl` refuses
  over plain text. It exits 38 with `curl: (38) LDAP local: bind via ldap_win_bind Confidentiality Required`
  in every one of the six cases.
- **First differing items:** the exit code (upstream 0, port 38) and the first output line
  (upstream `DN: dc=example,dc=com`, port none) for the three successful binds and Kerberos. For the
  wrong-password and not-accepted cases the exit codes agree (38) and the pinned standard error line
  differs as above.
- **Specification:** curl 8.21.0's `lib/ldap.c`, `ldap_win_bind_auth()`: with `CURLAUTH_NTLM`,
  `CURLAUTH_NEGOTIATE` or `CURLAUTH_DIGEST` set by `--ntlm`, `--negotiate` or `--digest`, it binds through
  SSPI with that method instead of `LDAP_AUTH_SIMPLE`. The man page (`--ntlm`, `--negotiate`, `--digest`)
  names the method curl uses to authenticate. A port with `-u` and one of these options must not send the
  password in a simple bind.

### F3: `-k` over `ldaps` skips the certificate check WinLDAP still makes

- **Case:** `WinLdap_Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself`.
- **Command line:** `curl -sS -m 20 -k -u alice:secret ldaps://127.0.0.1:<port>/dc=example,dc=com`
  against `surl --user alice:secret --self-signed`.
- **Upstream:** exits 38, writes nothing, with `curl: (38) LDAP local: bind via ldap_win_bind Server Down`.
- **Port:** exits 0 and writes `DN: dc=example,dc=com`.
- **First differing items:** exit code (38, 0), the first output line, and the pinned standard error
  line (upstream as above, port none).
- **Specification:** curl 8.21.0's `lib/ldap.c` turns `ldaps` over to `WinLDAP`'s own TLS
  (`ldap_sslinit()` with `LDAP_OPT_SSL` on). `-k` turns off curl's own peer verification, but the
  `WinLDAP` backend does not pass that to `WinLDAP`, which checks the self-signed certificate against the
  Windows store itself, so the connection fails and the bind reports `LDAP_SERVER_DOWN`. The mirrored test
  pins this answer from the pinned build. Upstream is right by ADR-0003 decision 6: on Windows, `-k` does not make an
  `ldaps` connection to an untrusted server succeed.
