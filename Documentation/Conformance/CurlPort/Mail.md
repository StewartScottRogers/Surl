# Phase 7 findings: SMTP, IMAP and POP3

What the Curl port does against `surl` over `smtp`, `smtps`, `imap`, `imaps`, `pop3` and `pop3s`
(with `STARTTLS` and `STLS`), measured beside pinned upstream curl as
[ADR-0089](../../Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)
decides. Upstream curl is right (ADR-0003 decision 6); a disagreement is the port's defect, and
Surl is never changed to make the port pass.

## Measured

| What | Value |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, paired for every mail scheme) |
| Surl | commit `8f7deb30dcc08acfdf922c9d5404e3456d223cc7` |
| Platform | Windows (`win-x64`), 2026-11-05 |
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
