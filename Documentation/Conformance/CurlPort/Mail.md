# Phase 7 findings: SMTP, IMAP and POP3

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7` (2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339) is the current linux-x64 outcome and supersedes the run below. Nothing threw `UnpinnedCurlPortClientException`: the test job accepted the hash its own build job reproduced (BL-1340, BL-1339), so cases compared pinned upstream curl with the Curl port. This family: 108 passed, 13 failed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work, not a CI finding.

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

## osx-arm64

Run [44000208980](https://github.com/StewartScottRogers/Surl/actions/runs/44000208980) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `c87b86d5c2ed723aeaf615bbc980b8d31d44b462`, Curl port commit
`74f3f45c47e86300b2b262dc1dacd43b508b09aa`) completed with the `Phase 7 families (osx-arm64)` job green
(BL-1324). Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes: 1 passed, 131 not
executed, and every case of this family Inconclusive or failed before any comparison.

Findings:

- **Nothing was compared.** `UpstreamCurlBuilds.json` pins no upstream curl build for `osx-arm64`, so the mirrored
  tests are Inconclusive there with "pins no upstream curl build for osx-arm64" (upstream curl is the only oracle,
  ADR-0003). Pinning one means downloading a build, which needs Stewart's approval (CLAUDE.md, "Decisions").
- **The osx-arm64 port build is not pinned by hash on a CI runner.** The runner's published `curl` hashed
  `71F9BB5EED0DCAB034D33883365082D2BA19758E9549B140037C4E663A98A0D3`, not the pinned
  `B5B48DC7CB11F42F41BD7F45E127ADF08E6BD1A0D3CAA0F80840E1971046BBEE` (`ciHashStable: false`), so cases that reach the port
  throw `UnpinnedCurlPortClientException`. The hash changes with each publish; it is not a port defect.
- **No port defect and no Surl defect is recorded for osx-arm64.** The family's findings table is unchanged.

## linux-x64

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 108 passed, 13 failed.
- **Failures are triaged by BL-1343.** No failing case is recorded in this family's findings table yet.
