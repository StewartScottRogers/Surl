# Phase 7 findings: LDAP and LDAPS

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 47 passed, 0 failed, 22 not executed; osx-arm64 0 passed, 0 failed, 69 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What `CurlPortBesideUpstreamCurlOverLdapTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every LDAP and LDAPS conversation of `UpstreamCurlSearchesSurlOverLdapTests`,
`UpstreamCurlBindsToSurlOverLdapWithKerberosTests` and `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`
through pinned upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-586).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl, Windows | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build; `lib/ldap.c`) |
| Upstream curl, Linux | SHA-256 `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480`, `curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.9 mit-krb5/1.22.2 OpenLDAP/2.6.15` (ADR-0076's OpenLDAP build; `lib/openldap.c`). Paired with the `OpenLdap_` tests; not run, as below |
| Surl | commit `00351846bd295957d066acd7ac6a9faa68c78e84` |
| Platform | Windows (`win-x64`) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

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

### Findings (linux-x64)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) (BL-1343): 27 `OpenLdap_`
cases showed a port defect on linux-x64, where upstream curl is the OpenLDAP build pinned for the platform
(`UpstreamCurlBuilds.json`) and the port is the linux-x64 Curl port build. They are listed for linux-x64 only (a
heading that names a platform in parentheses applies on that platform alone). The 20 `WinLdap_` cases do not run
there: they judge the WinLDAP build of upstream curl, which exists on Windows only, so they are Inconclusive off
Windows (BL-1343). Classification is the comparison's own: upstream curl's run is what the mirrored test asserts,
so each is a port defect (ADR-0003); none is a Surl defect.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `OpenLdap_SaslBind_AnyMechanism_PicksOneSurlOffersAndExits0WithTheBaseEntry` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_AnyMechanism_PicksOneSurlOffersAndExits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_BearerToken_Exits0WithTheBaseEntry[OAUTHBEARER, False]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_BearerToken_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_BearerToken_Exits0WithTheBaseEntry[OAUTHBEARER, True]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_BearerToken_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_BearerToken_Exits0WithTheBaseEntry[XOAUTH2, False]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_BearerToken_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_BearerToken_Exits0WithTheBaseEntry[XOAUTH2, True]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_BearerToken_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry[cram-md5, --login-options, AUTH=CRAM-MD5]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry[digest-md5, --digest, null]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry[digest-md5, --login-options, AUTH=DIGEST-MD5]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry[ntlm, --login-options, AUTH=NTLM]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry[ntlm, --ntlm, null]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied[digest-md5, --digest]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied[ntlm, --ntlm]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_PlainNotOfferedInClearWithoutAllowPlaintextAuth_Exits67LoginDenied` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainNotOfferedInClearWithoutAllowPlaintextAuth_Exits67LoginDenied` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_WrongBearerToken_Exits67LoginDenied` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_WrongBearerToken_Exits67LoginDenied` | [L-1](#l-1-the-linux-x64-port-exits-38-where-openldap-binds-with-sasl) |
| `OpenLdap_SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind` | [L-2](#l-2-the-linux-x64-port-binds-with-gssapi-for-a-user-naming-no-realm) |
| `OpenLdap_SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry[LOGIN, False]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry` | [L-3](#l-3-the-linux-x64-port-sends-another-sasl-bind-request-for-plain-and-login) |
| `OpenLdap_SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry[LOGIN, True]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry` | [L-3](#l-3-the-linux-x64-port-sends-another-sasl-bind-request-for-plain-and-login) |
| `OpenLdap_SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry[PLAIN, False]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry` | [L-3](#l-3-the-linux-x64-port-sends-another-sasl-bind-request-for-plain-and-login) |
| `OpenLdap_SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry[PLAIN, True]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry` | [L-3](#l-3-the-linux-x64-port-sends-another-sasl-bind-request-for-plain-and-login) |
| `OpenLdap_SaslBind_WrongPassword_Exits67LoginDenied[LOGIN]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_WrongPassword_Exits67LoginDenied` | [L-3](#l-3-the-linux-x64-port-sends-another-sasl-bind-request-for-plain-and-login) |
| `OpenLdap_SaslBind_WrongPassword_Exits67LoginDenied[PLAIN]` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_WrongPassword_Exits67LoginDenied` | [L-3](#l-3-the-linux-x64-port-sends-another-sasl-bind-request-for-plain-and-login) |
| `OpenLdap_StartTls_SslReqdWithNoCertificate_Exits1UnsupportedProtocol` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_SslReqdWithNoCertificate_Exits1UnsupportedProtocol` | [L-4](#l-4-the-linux-x64-port-differs-from-openldap-on-starttls-without-a-trusted-certificate) |
| `OpenLdap_StartTls_SslWithNoCertificate_CarriesOnInClearWithAVersion2BindAndExits0` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_SslWithNoCertificate_CarriesOnInClearWithAVersion2BindAndExits0` | [L-4](#l-4-the-linux-x64-port-differs-from-openldap-on-starttls-without-a-trusted-certificate) |
| `OpenLdap_StartTls_UntrustedCertificate_Exits64UseSslFailedNamingTheVerifyResult` | `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_UntrustedCertificate_Exits64UseSslFailedNamingTheVerifyResult` | [L-4](#l-4-the-linux-x64-port-differs-from-openldap-on-starttls-without-a-trusted-certificate) |

Each finding below is read from the verdict text of run 44044472038 (`phase-7-linux-x64-results`); a cause inside
the port is not traced, only what differs.

#### L-1: the linux-x64 port exits 38 where OpenLDAP binds with SASL

- **Cases:** the 17 rows above that name L-1: challenge-response (`cram-md5`, `digest-md5`, `ntlm`), bearer
  (`OAUTHBEARER`, `XOAUTH2`), `EXTERNAL`, `GSSAPI` with a KDC ticket and `AnyMechanism`.
- **Upstream:** exit 0 with the base entry on stdout, or exit 67 for the wrong-password and not-offered cases.
- **Port:** exit 38 (cannot bind) in every one, nothing on stdout. For the bearer cases the first request's BER
  length differs too (upstream `0x3E` at offset 1, port `0x12`): the port sends a shorter first bind.
- **Classification:** port defect.

#### L-2: the linux-x64 port binds with GSSAPI for a user naming no realm

- **Case:** `OpenLdap_SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind`.
- **Upstream:** exit 67 with no bind and nothing on stdout. **Port:** exit 0 and the base entry; its first request's
  length byte is `0x17` against upstream's `0x3E`.
- **Classification:** port defect.

#### L-3: the linux-x64 port sends another SASL bind request for PLAIN and LOGIN

- **Cases:** `SaslBind_PlainTextMechanismWithAllowPlaintextAuth` (four rows) and `SaslBind_WrongPassword` (two).
- **Measured:** exit codes and output agree; the first request's BER length byte differs, upstream `0x3E`,
  port `0x16` or `0x17`.
- **Classification:** port defect (wire bytes).

#### L-4: the linux-x64 port differs from OpenLDAP on StartTLS without a trusted certificate

- **`StartTls_SslReqdWithNoCertificate`:** upstream exits 1 (`curl: (1) Unsupported protocol`); the port exits 64.
  Upstream sends a byte at offset 31 where the port's request ends.
- **`StartTls_SslWithNoCertificate`:** the carried-on version 2 bind differs at offset 40 (upstream `0x02`, port `0x03`).
- **`StartTls_UntrustedCertificate`:** upstream exits 64, `curl: (64) SSL certificate OpenSSL verify result:
  self-signed certificate (18)`; the port exits 60 with the same verify result. Upstream's `surl` also notes
  `TLS handshake failed: Cannot determine the frame size or a corrupted frame was received.`, the port's does not.
- **Classification:** port defect.

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

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 0 passed, 0 failed, 69 not executed.
- **Detail:** Not executed: all 69. 21 are the `WinLdap_` cases (Windows-only, as on linux-x64), 1 is "Test is only supported on Windows", and the 47 `OpenLdap_` cases are Inconclusive with "UpstreamCurlBuilds.json pins no upstream curl linked against OpenLDAP for osx-arm64; it pins /opt/upstream-curl/8.21.0-openldap/curl" (the `ldap` protocol is not in the osx-arm64 reference build). That is the locator's message, not a finding.
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **Triaged by BL-1625.** No case of this family failed on osx-arm64, so nothing is added to its findings table. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 47 passed, 0 failed, 22 not executed.
- **Detail:** Not executed: 22. 21 are the `WinLdap_` cases, Inconclusive with "WinLdap_ cases judge the WinLDAP build of upstream curl, which is Windows-only; the OpenLdap_ twins cover this platform."; 1 is "Test is only supported on Windows". The 47 `OpenLdap_` cases all passed on this run.
- **Not classified here.** BL-1626 classifies the linux-x64 failures.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 21 passed, 47 failed, 1 not executed; of the 47 `OpenLdap_` cases 20 passed and 27 failed (the other 20 failures are `WinLdap_` cases, which are Windows-only).
- **Triaged by BL-1343.** The 27 `OpenLdap_` failures are listed under "Findings (linux-x64)" above (L-1 to L-4); the 20
  `WinLdap_` failures were test defects (a WinLDAP-only judgement run against OpenLDAP upstream) and are now
  Inconclusive off Windows.
- **The 47 `OpenLdap_` cases are no longer Inconclusive on linux-x64.** They ran against the OpenLDAP upstream build and compared: 20 passed, 27 failed. The 27 are real differences between the Curl port and pinned upstream curl, triaged by BL-1343 (L-1 to L-4).
