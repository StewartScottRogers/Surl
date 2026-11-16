# Conformance outcomes on Linux and macOS (one CI run)

What the pinned upstream curl reference builds did against `surl` on `linux-x64` and `osx-arm64`, test by test, measured once by CI and recorded here so an ADR can cite a measurement rather than a prediction. Written for BL-1289 (2026-11-16). It is a record of that one run, not a living status.

## The run

- Run: https://github.com/StewartScottRogers/Surl/actions/runs/43906686682 (`CI`, `workflow_dispatch` on `factory/phase-1` with `attempts=2`).
- Commit it ran: `a06ae86113e511d6d6204949c1fbf80e29b41ba9`.
- Results read: the `.trx` files of artifacts `test-results-ubuntu-latest-attempt-1` and `test-results-macos-latest-attempt-1`, the `Surl.Conformance.UnitTests` results only. Attempt 1 of both legs: **no test failed.**
- Attempt 2: the `ubuntu-latest` leg was green; the `macos-latest` leg ended `cancelled` after 30 minutes 28 seconds, so it gave no second measurement. No attempt-1 failure existed to compare against, so attempt 2 is not used below.
- Reference pins (`UpstreamCurlBuilds.json`, role `reference`), both curl 8.21.0 from stunnel/static-curl:
  - `linux-x64`, `curl-linux-x86_64-musl-8.21.0.tar.xz`, SHA-256 `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45`.
  - `osx-arm64`, `curl-macos-arm64-8.21.0.tar.xz`, SHA-256 `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF`.
- The Linux leg also ran the `supplementary` OpenLDAP build (SHA-256 `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480`) for the OpenLDAP tests.
- Totals over the `Upstream*` conformance test classes below (not the upstream test-case engine tests): linux-x64 P=704 NE=55 F=0; osx-arm64 P=641 NE=118 F=0 (P = Passed, NE = Inconclusive or skipped, F = Failed).

In a `.trx`, an Inconclusive test and a test skipped for its platform both have the outcome `NotExecuted`; the message tells them apart: `Assert.Inconclusive. ...` is Inconclusive, `Test is only supported on ...` is skipped by an `[OSCondition]` attribute. The messages are copied from the `.trx`; a test with no message in the `.trx` says so.

## Counts per test class

Per-class counts of test results (a parameterised test counts once per row). Classes of the Curl port's extended servers (`CurlPort*`, `Pinned*`) and the `UpstreamTestCase*` engine tests are not in the table: they are not behind the original protocols the open ADRs wait on, and the Curl port is never a verdict for Surl (ADR-0003).

| Class | linux Passed | linux Inconclusive/skipped | linux Failed | macOS Passed | macOS Inconclusive/skipped | macOS Failed |
|---|---|---|---|---|---|---|
| UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests | 4 | 2 | 0 | 4 | 2 | 0 |
| UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests | 47 | 0 | 0 | 0 | 47 | 0 |
| UpstreamCurlBindsToSurlOverLdapWithKerberosTests | 0 | 1 | 0 | 0 | 1 | 0 |
| UpstreamCurlBuildFeaturesTests | 22 | 0 | 0 | 22 | 0 | 0 |
| UpstreamCurlFetchesFromSurlApiServerTests | 5 | 0 | 0 | 5 | 0 | 0 |
| UpstreamCurlFetchesFromSurlMcpServerTests | 1 | 0 | 0 | 1 | 0 | 0 |
| UpstreamCurlFetchesFromSurlOverFtpTests | 20 | 3 | 0 | 20 | 3 | 0 |
| UpstreamCurlFetchesFromSurlOverGopherTests | 3 | 0 | 0 | 3 | 0 | 0 |
| UpstreamCurlFetchesFromSurlOverHttp11Tests | 6 | 0 | 0 | 6 | 0 | 0 |
| UpstreamCurlFetchesFromSurlOverHttp2Tests | 15 | 0 | 0 | 15 | 0 | 0 |
| UpstreamCurlFetchesFromSurlOverHttp3Tests | 13 | 0 | 0 | 13 | 0 | 0 |
| UpstreamCurlFetchesFromSurlOverHttpsTests | 7 | 2 | 0 | 7 | 2 | 0 |
| UpstreamCurlFetchesFromSurlOverTftpTests | 6 | 0 | 0 | 6 | 0 | 0 |
| UpstreamCurlLocationTests | 10 | 0 | 0 | 10 | 0 | 0 |
| UpstreamCurlLocatorTests | 98 | 0 | 0 | 98 | 0 | 0 |
| UpstreamCurlLogsInToSurlOverFtpTests | 10 | 0 | 0 | 10 | 0 | 0 |
| UpstreamCurlLogsInToSurlOverHttpTests | 21 | 6 | 0 | 21 | 6 | 0 |
| UpstreamCurlLogsInToSurlOverMqttTests | 5 | 0 | 0 | 5 | 0 | 0 |
| UpstreamCurlLogsInToSurlOverSshTests | 17 | 3 | 0 | 17 | 3 | 0 |
| UpstreamCurlLogsInToSurlWithKerberosTests | 0 | 11 | 0 | 0 | 11 | 0 |
| UpstreamCurlOffersSshAlgorithmsTests | 2 | 2 | 0 | 2 | 2 | 0 |
| UpstreamCurlQueriesSurlOverDictTests | 5 | 0 | 0 | 5 | 0 | 0 |
| UpstreamCurlReadsMailFromSurlOverImapTests | 44 | 0 | 0 | 44 | 0 | 0 |
| UpstreamCurlReadsMailFromSurlOverPop3Tests | 41 | 0 | 0 | 41 | 0 | 0 |
| UpstreamCurlRunnerTests | 45 | 0 | 0 | 45 | 0 | 0 |
| UpstreamCurlRunResultTests | 10 | 0 | 0 | 10 | 0 | 0 |
| UpstreamCurlSearchesSurlOverLdapTests | 0 | 23 | 0 | 0 | 23 | 0 |
| UpstreamCurlSendsMailToSurlOverSmtpTests | 36 | 0 | 0 | 36 | 0 | 0 |
| UpstreamCurlSharesFilesAcrossSurlProtocolsTests | 8 | 0 | 0 | 8 | 0 | 0 |
| UpstreamCurlSharesFilesFromFtpToSmbTests | 2 | 0 | 0 | 2 | 0 | 0 |
| UpstreamCurlSharesMailAcrossSurlProtocolsTests | 5 | 0 | 0 | 5 | 0 | 0 |
| UpstreamCurlSharesTopicsAcrossSurlListenersTests | 4 | 0 | 0 | 4 | 0 | 0 |
| UpstreamCurlShowsItsBuildOptionsTests | 2 | 0 | 0 | 2 | 0 | 0 |
| UpstreamCurlTalksToAConfiguredSurlTests | 9 | 0 | 0 | 9 | 0 | 0 |
| UpstreamCurlTalksToSurlOverMqttTests | 7 | 0 | 0 | 7 | 0 | 0 |
| UpstreamCurlTalksToSurlOverRtspTests | 16 | 0 | 0 | 0 | 16 | 0 |
| UpstreamCurlTalksToSurlOverTelnetTests | 3 | 0 | 0 | 3 | 0 | 0 |
| UpstreamCurlTalksToSurlOverWebSocketTests | 20 | 0 | 0 | 20 | 0 | 0 |
| UpstreamCurlTestCaseClientTests | 4 | 0 | 0 | 4 | 0 | 0 |
| UpstreamCurlTransfersFilesWithSurlOverScpTests | 11 | 0 | 0 | 11 | 0 | 0 |
| UpstreamCurlTransfersFilesWithSurlOverSftpTests | 41 | 0 | 0 | 41 | 0 | 0 |
| UpstreamCurlTransfersFilesWithSurlOverSmbTests | 26 | 0 | 0 | 26 | 0 | 0 |
| UpstreamCurlTrustsSurlsHostCertificateTests | 2 | 2 | 0 | 2 | 2 | 0 |
| UpstreamCurlUploadsToSurlOverFtpTests | 13 | 0 | 0 | 13 | 0 | 0 |
| UpstreamCurlUsesSurlOverFtpOnAUnixSocketTests | 6 | 0 | 0 | 6 | 0 | 0 |
| UpstreamTestCertificateDescriptionTests | 16 | 0 | 0 | 16 | 0 | 0 |
| UpstreamTestCertificateGeneratorTests | 16 | 0 | 0 | 16 | 0 | 0 |

Classes behind the milestone ADRs: SSH = `UpstreamCurlLogsInToSurlOverSshTests`, `UpstreamCurlTransfersFilesWithSurlOverScpTests`, `...OverSftpTests`, `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests`, `UpstreamCurlOffersSshAlgorithmsTests`; SMTP = `UpstreamCurlSendsMailToSurlOverSmtpTests`; IMAP = `UpstreamCurlReadsMailFromSurlOverImapTests`; POP3 = `UpstreamCurlReadsMailFromSurlOverPop3Tests`; WebSocket = `UpstreamCurlTalksToSurlOverWebSocketTests`; SMB = `UpstreamCurlTransfersFilesWithSurlOverSmbTests`; RTSP = `UpstreamCurlTalksToSurlOverRtspTests`.

## What the numbers say

- Nothing failed on either platform.
- SMTP, IMAP, POP3, WebSocket, SMB, scp and sftp: every test passed on both platforms.
- RTSP: passed on linux-x64 (16); on osx-arm64 all 16 are Inconclusive because the macOS reference build lists no `rtsp` protocol.
- OpenLDAP (`UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`): 47 passed on linux-x64 with the supplementary build; 47 Inconclusive on osx-arm64, which has no OpenLDAP build.
- Everything else Inconclusive or skipped is a Windows-only, macOS-only or WinLDAP-only test, or needs a prerequisite the runner lacks (listed below).

## linux-x64: Inconclusive and skipped tests

- **UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests** (NotExecuted, 2): `Test is only supported on OSX`
  - `Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes ("blowfish-cbc")`
  - `Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes ("cast128-cbc")`
- **UpstreamCurlBindsToSurlOverLdapWithKerberosTests** (NotExecuted, 1): `Test is only supported on Windows`
  - `NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer`
- **UpstreamCurlFetchesFromSurlOverFtpTests** (NotExecuted, 3): `Test is only supported on Windows`
  - `List_ListingsOnOnWindows_WritesTheLongListingWithCrlf`
  - `ListMachineReadable_ListingsOnOnWindows_WritesEachEntrysFactsWithCrlf`
  - `ListNames_ListingsOnOnWindows_WritesEachNameWithCrlf`
- **UpstreamCurlFetchesFromSurlOverHttpsTests** (NotExecuted, 2): `(the .trx gives no message)`
  - `Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild`
  - `Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel`
- **UpstreamCurlLogsInToSurlOverHttpTests** (NotExecuted, 1): `(the .trx gives no message)`
  - `NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused`
- **UpstreamCurlLogsInToSurlOverHttpTests** (NotExecuted, 5): `Test is only supported on Windows`
  - `DigestOverHttpOnSspi_OnlyMd5Offered_ExitsZeroWithTheFilesBytes`
  - `DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError ("SHA-256")`
  - `DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError ("SHA-512-256")`
  - `NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes`
  - `NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError`
- **UpstreamCurlLogsInToSurlOverSshTests** (NotExecuted, 2): `(the .trx gives no message)`
  - `HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm ("ecdsa-sha2-nistp256")`
  - `HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm ("ssh-ed25519")`
- **UpstreamCurlLogsInToSurlOverSshTests** (NotExecuted, 1): `Assert.Inconclusive. The account's own home directory holds no .ssh/known_hosts for curl to fall back to.`
  - `HostKey_NoPinAndOnlyTheAccountsOwnKnownHostsFile_Exits60`
- **UpstreamCurlLogsInToSurlWithKerberosTests** (NotExecuted, 11): `Test is only supported on Windows`
  - `GssapiOverImap_NoAccount_ExitsLoginDenied`
  - `GssapiOverPop3_NoAccount_ExitsLoginDenied`
  - `GssapiOverSmtp_NoAccount_ExitsLoginDenied`
  - `NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes`
  - `NegotiateOverHttp_NoAccount_IsRefusedWithHttpReturnedError`
  - `with --sasl-ir`
  - `with --sasl-ir`
  - `with --sasl-ir`
  - `without --sasl-ir`
  - `without --sasl-ir`
  - `without --sasl-ir`
- **UpstreamCurlOffersSshAlgorithmsTests** (NotExecuted, 1): `(the .trx gives no message)`
  - `KexInit_WindowsBuild_ListsTheWinCngAlgorithms`
- **UpstreamCurlOffersSshAlgorithmsTests** (NotExecuted, 1): `Test is only supported on Windows`
  - `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms`
- **UpstreamCurlSearchesSurlOverLdapTests** (NotExecuted, 23): `Assert.Inconclusive. The reference upstream curl UpstreamCurlBuilds.json pins for linux-x64, /opt/upstream-curl/8.21.0/curl (curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0), lists no ldap.`
  - `Bind_AllowAnonymous_Exits0WithTheBaseEntry`
  - `Bind_NoAccounts_Exits38InvalidCredentials`
  - `Bind_NoUserTheWindowsUserWithoutAnAccount_Exits38InvalidCredentials`
  - `Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported`
  - `Bind_NtlmWrongPassword_Exits38InvalidCredentials`
  - `Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired`
  - `Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer ("digest-md5","--digest")`
  - `Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer ("negotiate","--negotiate")`
  - `Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer ("ntlm","--ntlm")`
  - `Bind_WrongPassword_Exits38InvalidCredentials`
  - `Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself`
  - `Ldaps_UntrustedCertificate_Exits60`
  - `Search_BaseNotInTheDirectory_Exits39NoSuchObject`
  - `Search_BaseWithSimpleBind_Exits0WithTheBaseEntry`
  - `Search_ChosenErrorBusy_Exits39LikeEveryOtherNonSuccessResultCode`
  - `Search_ChosenErrorSizeLimitExceeded_Exits0`
  - `Search_InMemoryDirectory_Exits39NoSuchObject`
  - `Search_NoEntryMatches_Exits0WithNothingWritten`
  - `Search_NonAsciiValue_Exits0WithTheValueInBase64`
  - `Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes`
  - `Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries`
  - `Search_SslReqd_Exits4ExplicitTlsNotSupported`
  - `Search_SubtreeWithFilter_Exits0WithAliceOnly`
- **UpstreamCurlTrustsSurlsHostCertificateTests** (NotExecuted, 2): `Test is only supported on Windows`
  - `Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60`
  - `Download_RsaHostCertificateOnTheWindowsBuild_AgreesThePlainKeyAndExits60`

## osx-arm64: Inconclusive and skipped tests

- **UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests** (NotExecuted, 2): `Test is not supported on OSX`
  - `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile ("blowfish-cbc")`
  - `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile ("cast128-cbc")`
- **UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests** (NotExecuted, 47): `Initialization method Surl.Conformance.UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.RequireTheOpenLdapBuildAsync threw exception. Assert.Inconclusive. UpstreamCurlBuilds.json pins no upstream curl linked against OpenLDAP for osx-arm64; it pins /opt/upstream-curl/8.21.0-openldap/curl (curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.9 mit-krb5/1.22.2 OpenLDAP/2.6.15) for linux-x64..`
  - `Bind_NoAccounts_Exits67LoginDenied`
  - `Bind_NoUserUnderAllowAnonymous_BindsAnonymouslyAndExits0WithTheBaseEntry`
  - `Bind_NoUserWithoutAllowAnonymous_Exits38CannotBind`
  - `Bind_PlainTextWithoutAllowPlaintextAuth_Exits38CannotBind`
  - `Bind_WrongPassword_Exits67LoginDenied`
  - `Ldaps_CertificateTrustedThroughCacert_Exits0WithTheBaseEntry`
  - `Ldaps_Insecure_Exits0WithTheBaseEntry`
  - `Ldaps_UntrustedCertificate_Exits60`
  - `SaslBind_AnyMechanism_PicksOneSurlOffersAndExits0WithTheBaseEntry`
  - `SaslBind_BearerToken_Exits0WithTheBaseEntry ("OAUTHBEARER",False)`
  - `SaslBind_BearerToken_Exits0WithTheBaseEntry ("OAUTHBEARER",True)`
  - `SaslBind_BearerToken_Exits0WithTheBaseEntry ("XOAUTH2",False)`
  - `SaslBind_BearerToken_Exits0WithTheBaseEntry ("XOAUTH2",True)`
  - `SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry ("cram-md5","--login-options","AUTH=CRAM-MD5")`
  - `SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry ("digest-md5","--digest",null)`
  - `SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry ("digest-md5","--login-options","AUTH=DIGEST-MD5")`
  - `SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry ("ntlm","--login-options","AUTH=NTLM")`
  - `SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry ("ntlm","--ntlm",null)`
  - `SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied ("digest-md5","--digest")`
  - `SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied ("ntlm","--ntlm")`
  - `SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry`
  - `SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied`
  - `SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry`
  - `SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind`
  - `SaslBind_PlainNotOfferedInClearWithoutAllowPlaintextAuth_Exits67LoginDenied`
  - `SaslBind_PlainOverStartTls_Exits0WithTheBaseEntryWithoutAllowPlaintextAuth`
  - `SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry ("LOGIN",False)`
  - `SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry ("LOGIN",True)`
  - `SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry ("PLAIN",False)`
  - `SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry ("PLAIN",True)`
  - `SaslBind_WrongBearerToken_Exits67LoginDenied`
  - `SaslBind_WrongPassword_Exits67LoginDenied ("CRAM-MD5")`
  - `SaslBind_WrongPassword_Exits67LoginDenied ("LOGIN")`
  - `SaslBind_WrongPassword_Exits67LoginDenied ("PLAIN")`
  - `Search_BaseNotInTheDirectory_Exits39NoSuchObject`
  - `Search_BaseWithSimpleBind_Exits0WithTheBaseEntry`
  - `Search_InMemoryDirectory_Exits39NoSuchObject`
  - `Search_NoEntryMatches_Exits0WithNothingWritten`
  - `Search_NonAsciiValue_Exits0WithTheValueInBase64`
  - `Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes`
  - `Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries`
  - `Search_SubtreeWithFilter_Exits0WithAliceOnly`
  - `StartTls_Insecure_UpgradesAndExits0WithTheBaseEntryWithoutAllowPlaintextAuth ("--ssl-reqd")`
  - `StartTls_Insecure_UpgradesAndExits0WithTheBaseEntryWithoutAllowPlaintextAuth ("--ssl")`
  - `StartTls_SslReqdWithNoCertificate_Exits1UnsupportedProtocol`
  - `StartTls_SslWithNoCertificate_CarriesOnInClearWithAVersion2BindAndExits0`
  - `StartTls_UntrustedCertificate_Exits64UseSslFailedNamingTheVerifyResult`
- **UpstreamCurlBindsToSurlOverLdapWithKerberosTests** (NotExecuted, 1): `Test is only supported on Windows`
  - `NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer`
- **UpstreamCurlFetchesFromSurlOverFtpTests** (NotExecuted, 3): `Test is only supported on Windows`
  - `List_ListingsOnOnWindows_WritesTheLongListingWithCrlf`
  - `ListMachineReadable_ListingsOnOnWindows_WritesEachEntrysFactsWithCrlf`
  - `ListNames_ListingsOnOnWindows_WritesEachNameWithCrlf`
- **UpstreamCurlFetchesFromSurlOverHttpsTests** (NotExecuted, 2): `(the .trx gives no message)`
  - `Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild`
  - `Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel`
- **UpstreamCurlLogsInToSurlOverHttpTests** (NotExecuted, 1): `(the .trx gives no message)`
  - `NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused`
- **UpstreamCurlLogsInToSurlOverHttpTests** (NotExecuted, 5): `Test is only supported on Windows`
  - `DigestOverHttpOnSspi_OnlyMd5Offered_ExitsZeroWithTheFilesBytes`
  - `DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError ("SHA-256")`
  - `DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError ("SHA-512-256")`
  - `NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes`
  - `NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError`
- **UpstreamCurlLogsInToSurlOverSshTests** (NotExecuted, 2): `(the .trx gives no message)`
  - `HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm ("ecdsa-sha2-nistp256")`
  - `HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm ("ssh-ed25519")`
- **UpstreamCurlLogsInToSurlOverSshTests** (NotExecuted, 1): `Assert.Inconclusive. The account's own home directory holds .ssh/known_hosts, which curl falls back to.`
  - `HostKey_NoPinAndNoKnownHostsFile_Exits2`
- **UpstreamCurlLogsInToSurlWithKerberosTests** (NotExecuted, 11): `Test is only supported on Windows`
  - `GssapiOverImap_NoAccount_ExitsLoginDenied`
  - `GssapiOverPop3_NoAccount_ExitsLoginDenied`
  - `GssapiOverSmtp_NoAccount_ExitsLoginDenied`
  - `NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes`
  - `NegotiateOverHttp_NoAccount_IsRefusedWithHttpReturnedError`
  - `with --sasl-ir`
  - `with --sasl-ir`
  - `with --sasl-ir`
  - `without --sasl-ir`
  - `without --sasl-ir`
  - `without --sasl-ir`
- **UpstreamCurlOffersSshAlgorithmsTests** (NotExecuted, 1): `(the .trx gives no message)`
  - `KexInit_WindowsBuild_ListsTheWinCngAlgorithms`
- **UpstreamCurlOffersSshAlgorithmsTests** (NotExecuted, 1): `Test is only supported on Windows`
  - `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms`
- **UpstreamCurlSearchesSurlOverLdapTests** (NotExecuted, 23): `Assert.Inconclusive. The reference upstream curl UpstreamCurlBuilds.json pins for osx-arm64, /opt/upstream-curl/8.21.0/curl (curl 8.21.0 (aarch64-apple-darwin) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0), lists no ldap.`
  - `Bind_AllowAnonymous_Exits0WithTheBaseEntry`
  - `Bind_NoAccounts_Exits38InvalidCredentials`
  - `Bind_NoUserTheWindowsUserWithoutAnAccount_Exits38InvalidCredentials`
  - `Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported`
  - `Bind_NtlmWrongPassword_Exits38InvalidCredentials`
  - `Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired`
  - `Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer ("digest-md5","--digest")`
  - `Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer ("negotiate","--negotiate")`
  - `Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer ("ntlm","--ntlm")`
  - `Bind_WrongPassword_Exits38InvalidCredentials`
  - `Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself`
  - `Ldaps_UntrustedCertificate_Exits60`
  - `Search_BaseNotInTheDirectory_Exits39NoSuchObject`
  - `Search_BaseWithSimpleBind_Exits0WithTheBaseEntry`
  - `Search_ChosenErrorBusy_Exits39LikeEveryOtherNonSuccessResultCode`
  - `Search_ChosenErrorSizeLimitExceeded_Exits0`
  - `Search_InMemoryDirectory_Exits39NoSuchObject`
  - `Search_NoEntryMatches_Exits0WithNothingWritten`
  - `Search_NonAsciiValue_Exits0WithTheValueInBase64`
  - `Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes`
  - `Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries`
  - `Search_SslReqd_Exits4ExplicitTlsNotSupported`
  - `Search_SubtreeWithFilter_Exits0WithAliceOnly`
- **UpstreamCurlTalksToSurlOverRtspTests** (NotExecuted, 16): `Assert.Inconclusive. UpstreamCurlBuilds.json pins no upstream curl build for osx-arm64 that supports rtsp.`
  - `Options_AccountsButNoLogin_Exits22`
  - `Options_AllowAnonymous_Exits0WithoutALogin`
  - `Options_AnyAuth_Exits0WithDigest`
  - `Options_BasicInPlainTextWithoutAllowPlaintextAuth_Exits22With403 (False)`
  - `Options_BasicInPlainTextWithoutAllowPlaintextAuth_Exits22With403 (True)`
  - `Options_BasicWithAllowPlaintextAuth_Exits0`
  - `Options_BasicWrongPasswordWithAllowPlaintextAuth_Exits22With401`
  - `Options_BearerWithAllowPlaintextAuth_Exits0`
  - `Options_Digest_Exits0OnOneConnection`
  - `Options_DigestWrongPassword_Exits22With401`
  - `Options_HeadPastMaxRequestHead_Exits22With431`
  - `Options_Include_WritesTheHeadWithCSeqDateServerAndPublic`
  - `Options_MissingPathWithFieldsVerbose_Exits0AndSendsOptionsStar`
  - `Options_ServedRoot_Exits0WithNothingWritten`
  - `Options_WriteOutResponseCode_Writes200`
  - `Upload_UploadsOn_Exits0AndStoresNothingBecauseCurlSendsNoBody`
- **UpstreamCurlTrustsSurlsHostCertificateTests** (NotExecuted, 2): `Test is only supported on Windows`
  - `Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60`
  - `Download_RsaHostCertificateOnTheWindowsBuild_AgreesThePlainKeyAndExits60`

## `SSH negotiated` lines

`UpstreamCurlLogsInToSurlOverSshTests` and every other test that starts an SSH server writes surl's `SSH negotiated` note to its `TestContext` (`WriteNegotiatedAlgorithms`); each distinct line is quoted once with the tests that wrote it (the leading `#n *` counter is dropped). Lines differ between the platforms only in the cipher tests below.

### linux-x64

- `SSH negotiated kex curve25519-sha256, host key rsa-sha2-256, cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, compression none/none, strict kex on`
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_DifferentKeyInKnownHosts_Exits60 (x1)
- `SSH negotiated kex curve25519-sha256, host key rsa-sha2-512, cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, compression none/none, strict kex on`
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_NotInKnownHosts_Exits60 (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_OtherSha256Pinned_Exits60 (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.Login_WrongPassword_Exits67LoginDenied ("scp") (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.Login_WrongPassword_Exits67LoginDenied ("sftp") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive (".hidden.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive ("dir") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive ("missing.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_ChosenErrorLine_Exits78 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_ChosenErrorLineBeforeTheFirstNul_Exits25AndStoresNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_MissingDirectory_Exits25 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_PastMaxFilesize_Exits25AndWritesNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_UploadsOff_Exits25AndWritesNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile (".hidden.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile (".surl/lock") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile ("missing.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_ChosenErrorStatus3_Exits9 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_Directory_Exits79 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Listing_ListingsOff_Exits78 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_ChangeWithUploadsOff_Exits21 ("mtime \"Sun, 27 Sep 2026 12:34:56 GMT\" /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_ChangeWithUploadsOff_Exits21 ("rm /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_MakeThenRemoveDirectory_ListsItEmptyThenFindsItGone (x2)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_RenameOntoExistingFile_Exits21 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("chgrp 1000 /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("chmod 0600 /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("chown 1000 /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("ln /a.txt /l.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("statvfs /") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("symlink /a.txt /l.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_ChosenErrorStatus2_Exits78AndStoresNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_MissingDirectory_Exits78 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_PastMaxFilesize_Exits79AndWritesNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_UploadsOff_Exits9AndWritesNothing (x1)
- `SSH negotiated kex curve25519-sha256, host key ssh-ed25519, cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, compression none/none, strict kex on`
  - UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnOpenSslBuilds_AgreesThePlainKeyAndExits60 (x1)

### osx-arm64

- `SSH negotiated kex curve25519-sha256, host key rsa-sha2-256, cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, compression none/none, strict kex on`
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_DifferentKeyInKnownHosts_Exits60 (x1)
- `SSH negotiated kex curve25519-sha256, host key rsa-sha2-512, cipher blowfish-cbc/blowfish-cbc, MAC hmac-sha2-256/hmac-sha2-256, compression none/none, strict kex on`
  - UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes ("blowfish-cbc") (x1)
- `SSH negotiated kex curve25519-sha256, host key rsa-sha2-512, cipher cast128-cbc/cast128-cbc, MAC hmac-sha2-256/hmac-sha2-256, compression none/none, strict kex on`
  - UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes ("cast128-cbc") (x1)
- `SSH negotiated kex curve25519-sha256, host key rsa-sha2-512, cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, compression none/none, strict kex on`
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_NoPinAndOnlyTheAccountsOwnKnownHostsFile_Exits60 (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_NotInKnownHosts_Exits60 (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.HostKey_OtherSha256Pinned_Exits60 (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.Login_WrongPassword_Exits67LoginDenied ("scp") (x1)
  - UpstreamCurlLogsInToSurlOverSshTests.Login_WrongPassword_Exits67LoginDenied ("sftp") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive (".hidden.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive ("dir") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive ("missing.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_ChosenErrorLine_Exits78 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_ChosenErrorLineBeforeTheFirstNul_Exits25AndStoresNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_MissingDirectory_Exits25 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_PastMaxFilesize_Exits25AndWritesNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_UploadsOff_Exits25AndWritesNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile (".hidden.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile (".surl/lock") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile ("missing.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_ChosenErrorStatus3_Exits9 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_Directory_Exits79 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Listing_ListingsOff_Exits78 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_ChangeWithUploadsOff_Exits21 ("mtime \"Sun, 27 Sep 2026 12:34:56 GMT\" /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_ChangeWithUploadsOff_Exits21 ("rm /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_MakeThenRemoveDirectory_ListsItEmptyThenFindsItGone (x2)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_RenameOntoExistingFile_Exits21 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("chgrp 1000 /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("chmod 0600 /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("chown 1000 /a.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("ln /a.txt /l.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("statvfs /") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21 ("symlink /a.txt /l.txt") (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_ChosenErrorStatus2_Exits78AndStoresNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_MissingDirectory_Exits78 (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_PastMaxFilesize_Exits79AndWritesNothing (x1)
  - UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_UploadsOff_Exits9AndWritesNothing (x1)
- `SSH negotiated kex curve25519-sha256, host key ssh-ed25519, cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, compression none/none, strict kex on`
  - UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnOpenSslBuilds_AgreesThePlainKeyAndExits60 (x1)
