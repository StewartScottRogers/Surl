# Phase 7 findings: SCP and SFTP

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, which contains BL-1622 and BL-1623; Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`) is the current outcome on linux-x64 and osx-arm64 and supersedes every earlier run named below. Both test jobs ran. This family: linux-x64 71 passed, 1 failed, 7 not executed; osx-arm64 70 passed, 2 failed, 7 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work (BL-1625 classifies osx-arm64, BL-1626 linux-x64), not a CI finding.

What `CurlPortBesideUpstreamCurlOverSshTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every SCP and SFTP conversation of `UpstreamCurlTransfersFilesWithSurlOverScpTests`,
`UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `UpstreamCurlLogsInToSurlOverSshTests`,
`UpstreamCurlOffersSshAlgorithmsTests` and `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` through
pinned upstream curl and the pinned Curl port, each against its own fresh `surl` and with its own empty
`HOME` and `USERPROFILE` (ADR-0089, ADR-0051 decision 8, BL-582).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl, reference | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build; every case but those below) |
| Upstream curl, OpenSSL | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` (the supplementary static-curl build of ADR-0063, paired with `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` and every `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` case, as their mirrored tests run it; ADR-0089 decision 2) |
| Surl | commit `4c5010959a6096d5ee8bfcdfbef890e9d6160653` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

## Result

73 cases on Windows: 68 agreements, 5 port defects (one cause, below), no Surl defect, none not
comparable. Six cases are excluded on Windows by the `[OSCondition]` their mirrored tests carry (the
Linux and macOS `KEXINIT` lists, the two elliptic-curve host keys on OpenSSL builds, the account's own
`known_hosts`, and the two macOS cipher crashes).

Compared, per ADR-0089 decision 4 and its amendments: exit code, standard output, the `curl: (N)`
standard error line, the files `surl` stored for each upload case, and `surl`'s `*` notes with each
run's throwaway host-key fingerprint replaced by `<sha256>` and `<md5>`. No request bytes: the SSH
transport is encrypted. `HostKey_OtherSha256Pinned_Exits60` compares no standard error, since curl's
refusal names that run's own fingerprint; upstream's line is still checked against the mirrored
assertion. The three local-file cases (`-o` with `-C -`, `-R`, and `-Q "mtime …"` with `-R`) give each
run its own output file and compare its bytes and modification time as stored files.

Not compared (ADR-0089's BL-582 amendment): the notes `surl` writes as the client tears the connection
down - `SSH disconnect received`, `SSH channel N ended`, `SFTP session ended`, `Exchange N ended`, and
the SCP download's final acknowledgement note. Their order is the client SSH layer's (the port closes
the channel before it disconnects; libssh2 disconnects alone) and races the client's exit.

The `KEXINIT` cases run each client against its own `ClientKexInitRecorder` rather than `surl`, and
compare the identification line and the ten name-lists it records, beside the exit code and output
(both clients exit 2 when the recorder hangs up).

Intermittent: about one SSH connection in 256 from the reference build fails with exit 2,
`-8, Unable to exchange encryption keys`. That is libssh2 1.11.1 on WinCNG's own defect (ADR-0062), not
Surl's and not the port's; a re-run passes. It showed in several cases across this measurement's runs, so a
case where either run fails exactly that way is measured again, both runs, up to three attempts
(ADR-0089's BL-582 amendment).

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree. A data-driven case is named `Method[data row]`.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` | `UpstreamCurlOffersSshAlgorithmsTests.KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |

### Findings (win-x64)

SSH-1 shows on Windows only for these four cases: the linux-x64 port build agrees with upstream curl on them
(run 44044472038, BL-1343). A heading that names a platform in parentheses applies on that platform alone.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile[blowfish-cbc]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile[cast128-cbc]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_MacOnlySurlOffers_AgreesItAndWritesTheFile[hmac-ripemd160]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_MacOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_MacOnlySurlOffers_AgreesItAndWritesTheFile[hmac-ripemd160@openssh.com]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_MacOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |

### Findings (linux-x64)

None. Run 44044472038 (BL-1343) listed `Login_PublicKey_DownloadsTheFile[scp]` (SSH-2) because `surl`'s `-v` note
`SSH channel 0 handler failed: The SSH channel was closed before the data was sent.` showed on upstream curl's run and
not the port's. BL-1626 removed it: runs 45198867449 and 45412760425 did not show it, and it is an scp teardown note,
which ADR-0089 Amendment 6 leaves uncompared (the test's `TeardownNote` now names it).

### Findings (osx-arm64)

Run 45412760425 (BL-1624), classified by BL-1625 from its `phase-7-osx-arm64-results` artifact. Upstream curl is the
osx-arm64 reference build `04E0E69B…EEBF` (curl 8.21.0, libssh2 1.11.1).

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes[blowfish-cbc]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes` | [SSH-3](#ssh-3-upstream-curl-crashes-on-the-osx-arm64-build-where-the-port-downloads-the-file) |
| `Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes[cast128-cbc]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes` | [SSH-3](#ssh-3-upstream-curl-crashes-on-the-osx-arm64-build-where-the-port-downloads-the-file) |

#### SSH-3: upstream curl crashes on the osx-arm64 build where the port downloads the file

- **Cases:** `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://…/a.txt` against
  `surl --allow-weak-ssh-algorithms` offering only `blowfish-cbc` or only `cast128-cbc`.
- **Upstream:** both runs negotiate `curve25519-sha256`, `rsa-sha2-512`, the offered cipher and `hmac-sha2-256`, then
  upstream exits 139 (a crash) with empty output and no login reaches `surl`. The mirrored test pins that crash
  (`...AgreesItAndCurlCrashes`), so upstream completes the exchange as the test asserts.
- **Port:** negotiates the same algorithms, logs in, reads `/a.txt` (12 bytes) and exits 0 with `hello world`.
- **Classification:** port difference from upstream's measured behaviour, not a Surl defect. Upstream's crash is
  a defect of the macOS libssh2 build with these two legacy ciphers, not behaviour curl documents; the port is
  listed because the comparison cannot call a working download right where upstream crashed. The cause inside the
  port is not traced.
- **Accepted difference:** both cases, `[blowfish-cbc]` and `[cast128-cbc]`, are an accepted difference. The
  port's side decided on 2026-11-24 to keep downloading and not copy the crash; port BL-5909 records that
  decision ([handoff](Port-Board-Handoff.md) entry 28). That is the port's choice about its own behaviour, not
  evidence about Surl (ADR-0003): upstream curl's verdict on these cases is unchanged (exit 139, empty output,
  as pinned upstream curl measured), and both stay listed in the table above, which the Phase 7 test reads.

### SSH-1: the port offers only the WinCNG backend's SSH algorithms

- **Phase 7 tests:** the five rows above. Paired upstream build: the OpenSSL static-curl build
  (`589C8E4D…`), as each mirrored test runs it.
- **Command lines:**
  - `curl -sS -k sftp://127.0.0.1:<port>/x` against a `ClientKexInitRecorder` (the `KEXINIT` case);
  - `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://127.0.0.1:<port>/a.txt`
    against `surl sftp://… --throwaway-hostkey --user tester:secret --allow-weak-ssh-algorithms`, with
    `--ssh-ciphers blowfish-cbc` or `--ssh-ciphers cast128-cbc` (curl run with `OPENSSL_CONF` activating
    OpenSSL's `legacy` provider), or `--ssh-ciphers aes128-ctr --ssh-macs hmac-ripemd160` or
    `… hmac-ripemd160@openssh.com`.
- **Upstream:** the `KEXINIT` offers, in order, kex
  `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,`
  then the Diffie-Hellman list; host keys `ecdsa-sha2-nistp256,…,ssh-ed25519,ssh-ed25519-cert-v01@openssh.com,`
  then the RSA list; ciphers adding `aes256-gcm@openssh.com,aes128-gcm@openssh.com`, `blowfish-cbc` and
  `cast128-cbc`; MACs adding `hmac-ripemd160,hmac-ripemd160@openssh.com`. Each weak-algorithm download
  exits 0 with `hello world\n`, and `surl` notes `SSH negotiated … cipher blowfish-cbc/blowfish-cbc …`
  (or the cipher or MAC named).
- **Port:** the `KEXINIT` differs from line 2 (the kex name-list): it offers exactly the Windows
  reference build's WinCNG lists (`diffie-hellman-group-exchange-sha256,…,kex-strict-c-v00@openssh.com`;
  RSA host keys only; no GCM, Blowfish, CAST-128 or RIPEMD-160). Each weak-algorithm download exits 2,
  `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`, no output, and
  `surl` notes `SSH no common cipher algorithm; client offered chacha20-poly1305@openssh.com,aes256-ctr,…`
  (or `no common MAC algorithm`).
- **Agreement beside it:** `KexInit_WindowsBuild_ListsTheWinCngAlgorithms` agrees: the port's lists are
  the reference build's, identification `SSH-2.0-libssh2_1.11.1` included.
- **What is right:** upstream curl 8.21.0 with libssh2 1.11.1 offers the algorithms of the crypto
  backend libssh2 was built with (libssh2 1.11.1 `src/crypt.c`, `src/mac.c`, `src/kex.c` and
  `src/hostkey.c`, whose OpenSSL entries are compiled in under `LIBSSH2_OPENSSL`); RFC 4253 section 6.3
  names `blowfish-cbc` and `cast128-cbc` as OPTIONAL ciphers, RFC 5656 the ECDH and ECDSA names, RFC
  8731 `curve25519-sha256` and RFC 8709 `ssh-ed25519`. A client that is upstream curl's port on an
  OpenSSL-backed build offers those; the port offers the WinCNG backend's lists whatever it runs on.
  Whether the port should follow the OpenSSL builds or keep the reference build's lists is the port
  maintainer's call; against the OpenSSL pairing these cases use, it differs.

## osx-arm64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed with the `Phase 7 families (osx-arm64)` job run. Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes.

- **This family:** 70 passed, 2 failed, 7 not executed.
- **Not measurable.** not measurable on osx-arm64: the cases under `[OSCondition(OperatingSystems.Windows)]` test Windows-only behaviour (Negotiate through SSPI, Windows-build answers); the other platform's answer is pinned by its own `ConditionMode.Exclude` test where one exists, and that test is in the run (ADR-0089 Amendment 9).
- **Detail:** Failed: `Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes` (`cast128-cbc`, `blowfish-cbc`). Not executed: 3 with no message, 2 "Test is not supported on OSX", 1 "Test is only supported on Windows", 1 Inconclusive ("The account's own home directory holds .ssh/known_hosts, which curl falls back to.").
- **Cases compared.** The `UpstreamCurlBuilds.json` pin for `osx-arm64` (curl 8.21.0, SHA-256 `04E0E69B...EEBF`, pinned 2026-09-28) was found, and BL-1622 removed `UnpinnedCurlPortClientException` (357 of them in run 44044472038), so cases that reach the port compared it with upstream curl. An earlier statement that no upstream curl build is pinned for `osx-arm64` was false.
- **Triaged by BL-1625.** Both failures are one finding, SSH-3, now listed under `### Findings (osx-arm64)`. The Curl port is never an oracle (ADR-0003).

## linux-x64

Run [45412760425](https://github.com/StewartScottRogers/Surl/actions/runs/45412760425) of `curl-port-phase-7` (2026-11-24, `factory/phase-1` at Surl commit `70828fe7e0becfaef1aeec787cdb7c42023732f7`, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, BL-1624) completed. Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing. It supersedes run 44044472038 and the later scheduled run 45198867449 (2026-11-23).

- **This family:** 71 passed, 1 failed, 7 not executed.
- **Not measurable.** not measurable on linux-x64: the cases under `[OSCondition(OperatingSystems.Windows)]` test Windows-only behaviour (Negotiate through SSPI, Windows-build answers); the other platform's answer is pinned by its own `ConditionMode.Exclude` test where one exists, and that test is in the run (ADR-0089 Amendment 9).
- **Detail:** Failed: `Login_PublicKey_DownloadsTheFile` (`scp`). Not executed: 3 with no message, 2 "Test is only supported on OSX", 1 "Test is only supported on Windows", 1 Inconclusive ("The account's own home directory holds no .ssh/known_hosts for curl to fall back to.").
- **Triaged by BL-1626.** The one failure was a stale listing of SSH-2 (`SSH channel 0 handler failed: The SSH channel was closed before the data was sent.`, an scp teardown note, not compared by ADR-0089 Amendment 6): the row is removed and the test leaves that note out. Run 45198867449 failed the same case; run 44044472038 showed the note. Nothing was added.

### Earlier run (44044472038, 2026-11-17)

Run [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339, BL-1307) completed.
Its `Phase 7 families and OpenLdap_ cases (linux-x64)` job runs `continue-on-error`, so findings gate nothing.

Findings:

- **Cases compared.** The test job accepted the Curl port hash its own build job reproduced, so no
  `UnpinnedCurlPortClientException` was thrown (it replaces run 44012635963, where nothing compared).
- **This family:** 67 passed, 5 failed, 7 not executed.
- **Triaged by BL-1343.** Of the 5 failures, four were SSH-1 cases that do not show on linux-x64 (now in a
  win-x64-only table) and one is a new linux-x64 port defect (SSH-2).
