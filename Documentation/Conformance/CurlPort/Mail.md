# Phase 7 findings: SMTP, IMAP and POP3

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 108 passed, 13 failed, 0 not executed; osx-arm64 121 passed, 0 failed, 0 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What the Curl port does against `surl` over `smtp`, `smtps`, `imap`, `imaps`, `pop3` and `pop3s`
(with `STARTTLS` and `STLS`), measured beside pinned upstream curl as
[ADR-0089](../../Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)
decides. Upstream curl is right (ADR-0003 decision 6); a disagreement is the port's defect, and
Surl is never changed to make the port pass.

## Measured

| What | Value |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, paired for every mail scheme) |
| Surl | commit `8f7deb30dcc08acfdf922c9d5404e3456d223cc7` |
| Platform | Windows (`win-x64`), 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |
| Tests | `Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverMailTests.cs`: 83 tests, 121 runs with their data rows, one per test of `UpstreamCurlSendsMailToSurlOverSmtpTests` (24), `UpstreamCurlReadsMailFromSurlOverImapTests` (31) and `UpstreamCurlReadsMailFromSurlOverPop3Tests` (28) |

Every run ended in **agreement**: exit code, standard output, the standard error lines each
mirrored test asserts on, `surl`'s notes, the request bytes of every plaintext connection and the
messages the stopped `surl`'s mail store holds are equal after normalisation. Upstream's own run
was what every mirrored test asserts, so no Surl defect was found either.

## How the family is compared

Each Phase 7 test is named after the test it mirrors, with `Smtp_`, `Imap_` or `Pop3_` before it;
a data row's case is the method name followed by its row's values, e.g.
`Smtp_Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage(CRAM-MD5, False, ESMTPA)`.

- **Content.** Each client gets its own `surl` over its own fresh `--directory`, on `smtp` alone or
  on `smtp` beside `imap`, `imaps`, `pop3` or `pop3s`, as the mirrored class starts it. The IMAP and
  POP3 cases seed the inbox over `smtp` first, through upstream curl for both runs, because the seed
  is the case's content and not what is measured. A conversation of several client runs (append then
  fetch, `CREATE` then list, `STORE` then `EXPUNGE`, `DELE` then list) compares each run.
- **Request bytes.** Recorded through a fresh relay per client run on every plaintext conversation.
  A conversation that upgrades with `STARTTLS` or `STLS` is not recorded, because after the upgrade
  the bytes are the TLS library's; its plaintext twin compares curl's own bytes. A connection the test
  holds open itself (`--max-connections 1`, a maildrop in use) goes straight to `surl`.
- **Normalisation this family adds** to ADR-0089 decision 4's table, each for a value the clock or
  `surl` chooses afresh per run and neither client decides:
  - the date in the `Received:` trace field, `<date>`, in output and stored mail;
  - the `UIDVALIDITY` value, `<uidvalidity>`, in output (`EXAMINE`, and POP3's `UIDL` unique id);
  - the `CRAM-MD5` digest and the `APOP` digest, keyed by `surl`'s random challenge, and the
    `DIGEST-MD5` server `nonce` the client echoes, in request bytes and asserted standard error lines;
  - the port in `OAUTHBEARER`'s GS2 header (`port=`), which is the run's own listener.

## Findings

None. The table lists each Phase 7 finding by test; a test fails when a port defect appears that
the table does not list, and when a listed one stops appearing (ADR-0089 decision 5).

| Test | Mirrors | Finding |
| --- | --- | --- |

### Findings (linux-x64)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) (BL-1343): 13 cases showed
one port defect on linux-x64 and are listed for linux-x64 only (a heading that names a platform in parentheses
applies on that platform alone). On Windows the family has no finding.

| Test | Mirrors | Finding |
| --- | --- | --- |
| `Imap_Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(OAUTHBEARER, False)` | `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Imap_Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(XOAUTH2, False)` | `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Imap_Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(XOAUTH2, True)` | `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Imap_Fetch_ImapsForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage(DIGEST-MD5, digest-md5)` | `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Imap_Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage(AUTH=+LOGIN, False)` | `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Imap_Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage(AUTH=CRAM-MD5, False)` | `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage(OAUTHBEARER, False)` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage(OAUTHBEARER, True)` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage(XOAUTH2, True)` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage(LOGIN, False, ESMTPSA)` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage(LOGIN, True, ESMTPSA)` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage(PLAIN, False, ESMTPSA)` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |
| `Smtp_Send_SslReqdWithSelfSigned_UpgradesWithStartTlsAndStoresTheMessage` | `UpstreamCurlSendsMailToSurlOverSmtpTests.Send_SslReqdWithSelfSigned_UpgradesWithStartTlsAndStoresTheMessage` | [M-1](#m-1-upstream-curls-tls-session-ends-with-a-broken-pipe-the-ports-does-not) |

#### M-1: upstream curl's TLS session ends with a broken pipe, the port's does not

- **Cases:** every row above (the case name's parentheses hold the data row).
- **Measured:** exit code, standard output and the stored mail agree. The only difference is `surl`'s `-v` note:
  upstream curl's run has `Exchange N ended because the protocol server threw IOException: Unable to write data
  to the transport connection: Broken pipe.` where the port's run ends the exchange without it. Upstream curl
  (OpenSSL) drops the connection after the last command; the port ends the TLS session another way, so the
  server's final write meets a closed peer only for upstream.
- **Classification:** port defect, in how the port ends a TLS session after the exchange (notes only; no
  output, exit code or stored-mail difference).

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 121 passed, 0 failed, 0 not executed.
- **Detail:** Every case passed.
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **Triaged by BL-1625.** No case of this family failed on osx-arm64, so nothing is added to its findings table. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 108 passed, 13 failed, 0 not executed.
- **Detail:** Failed: 13 cases, all SASL or STARTTLS logins: CRAM-MD5 authentication, `LOGIN` after STARTTLS (also with `--sasl-ir`), `PLAIN` after STARTTLS, the `LOGIN` command, `Smtp_Send_SslReqdWithSelfSigned_UpgradesWithStartTlsAndStoresTheMessage`, `Smtp_Send_ForcedBearerMechanismAfterStartTls_...` (OAUTHBEARER and XOAUTH2 rows), `Imap_Fetch_ImapsForcedBearerMechanism_...` (OAUTHBEARER and XOAUTH2 rows) and `Imap_Fetch_ImapsForcedNamedChoiceMechanism_...` (DIGEST-MD5).
- **Not classified here.** BL-1626 classifies the linux-x64 failures.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 108 passed, 13 failed.
- **Triaged by BL-1343.** The 13 failures are listed under "Findings (linux-x64)" above: one port defect (M-1).
