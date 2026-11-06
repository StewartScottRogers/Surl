---
id: BL-879
title: Run the upstream imap, pop3 and smtp test cases against the mail server halves and count them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-876, BL-877, BL-878]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-879 — Run the upstream imap, pop3 and smtp test cases against the mail server halves and count them

## Goal

The upstream test-case run covers every runnable case whose `<server>` is `imap`, `pop3` or `smtp`, played by
BL-876, BL-877 and BL-878's server halves, and the pass-rate file counts them. This completes what BL-395 set
out to do.

## Context

- Last of BL-395's split (timed out after 120 min; Stewart asked for the split 2026-11-05). The protocol work is
  done in BL-875 to BL-878. This task wires the halves in, runs the suite on Windows, records the counts and
  files the failures.
- Decision: ADR-0080 decisions 2, 3 and 5. One `UpstreamTestCaseServerCommands` row per server name says which
  `<servercmd>` words its half implements. Logins go to the accept-all `AnonymousAuthenticationPolicy`. The
  passing list only rises (BL-393): the current table's passed count equals the line count of
  `upstream-test-cases-passing.win-x64.txt`.
- FTP precedent, to copy: BL-394's Notes (archived `Tasks/Done/2026-11-05_0953/BL-394-...`). They describe
  `UpstreamTestCaseServerHalves` (in `Conformance/Surl.Conformance.UnitTests`), the `Ftp` row, how the
  combined `<protocol>` of a two-server case is assembled, and how counts and failures were recorded.
- Port variables `%IMAPPORT`, `%IMAP6PORT`, `%POP3PORT`, `%POP36PORT`, `%SMTPPORT` and `%SMTP6PORT` are already
  in `UpstreamTestCaseVariables`, and `UpstreamTestCaseServers` already marks `imap`, `pop3` and `smtp` as
  built. The `<protocol>` check reads the half's `MailUpstreamTestCaseReplies.ReceivedBytes`; `<upload>` reads
  `UploadedBytes`.
- Out of scope, as in BL-395: `imaps`, `pop3s` and `smtps` (one case each, ADR-0080 Context). Record in Notes
  what those cases skip as after this task.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-395`; call it `stash@{N}`. Apply
  `git diff "stash@{N}^1" "stash@{N}" -- Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs Conformance/Surl.Conformance.UnitTests/UpstreamTestCaseServerCommandsTests.cs Conformance/Surl.Conformance.UnitTests/UpstreamTestCaseServerHalves.cs | git apply -3`.
  This adds the `Mail` row for `imap`, `pop3` and `smtp`, its tests, and the six plaintext mail listeners on
  `127.0.0.1` and `[::1]` with their port properties. The lane stopped here, before handing a case's reply to
  the halves and before any run. `UpstreamTestCasesAgainstSurlTests.cs` changed on master since then and is
  not in the stash: extend it yourself to map the six ports to their variables, build one
  `MailUpstreamTestCaseReplies` per case, hand it to the half the case names, and read its bytes for
  `<protocol>` and `<upload>`. Ignore the stash's protocol-library files, which BL-876 to BL-878 took. Do not
  apply or drop the stash.
- Stand-in (ADR-0124): none applies. This is the conformance harness, not a server feature.

## Acceptance criteria

- [x] `UpstreamTestCaseServerCommandsTests` pins the `Mail` row for `imap`, `pop3` and `smtp`, equal to
      `MailUpstreamTestCaseReplies.SupportedServerCommands`, and shows `imaps` still unsupported.
- [x] The Windows run of `UpstreamTestCasesAgainstSurlTests` with the reference pin from
      `UpstreamCurlBuilds.json` covers every runnable IMAP, POP3 and SMTP case. Notes record the pin's SHA-256,
      the date, the total ran, passed, failed and skipped, the mail family's own counts, and its skips by
      reason.
- [x] `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` gains the mail cases that pass. The
      `win-x64` row and skip table of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` state the new
      counts, and the fast pass-rate guard test is green.
- [x] The failing mail cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes. A cause that is the upstream test server's arbitrary choice goes to
      `Upstream-Test-Case-Exclusions.md` as decision 5 allows.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Conformance.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

- **Stash reused** (`darkfactory BL-395 20261105-054539`, applied with `git apply -3`, not dropped). Its
  three files conflicted with BL-396/BL-397's table-driven `UpstreamTestCaseServerHalves`; resolved by
  adding `imap`, `pop3`, `smtp` (each on `127.0.0.1` and `[::1]`) to the `Listeners` table, with port
  properties read from the bound-port map like the others. `Play` takes one `MailUpstreamTestCaseReplies`
  per case, shared by the three halves (it is thread-safe; a case names one mail server).
- **Choices** (sensible defaults, no ADR needed: all inside ADR-0080 decision 2): the mail halves get
  every `<data*>` part, empty ones kept (`MailReplyParts`), unlike FTP's empty-is-no-file rule, since
  upstream's `getreplydata` sends what the part holds; `<servercmd>` goes to the case's first server's
  half only, as for every other family (cases 779 and 795, http then imap, so skip
  `servercmd-unsupported:AUTH`; BL-898 decides that).
  `UpstreamTestCaseSelectorTests.ForBuild_AsSurlStandsToday_...` now expects `smtp REPLY` supported.
- **Run**, Windows, reference pin `C:\Program Files\Git\mingw64\bin\curl.exe` SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, 2026-11-05, about 92 s:
  of 2,013 cases **1,124 ran: 1,041 pass, 83 fail; 889 skip** (was 964 / 895 / 69 / 1,049). No regression.
- **Mail family** (216 cases naming `imap`, `pop3` or `smtp`, `s` forms included): **160 ran, 146 pass,
  14 fail; 56 skip**: `feature-missing:!SSPI` 12, `feature-missing:!win32` 12, `debug-build` 9,
  `libtest` 8, `disabled-upstream` 3, `feature-missing:!IDN` 3, `server-not-built:http-proxy` 3,
  `servercmd-unsupported:AUTH` 2 (779, 795), `servercmd-unsupported:SLOWDOWN` 1, and
  `server-half-not-composed:imaps`, `:pop3s`, `:smtps` 1 each - the three TLS cases, out of scope as in
  BL-395, still skip that way.
- **Failures grouped and filed:**
  - SMTP half accepts what upstream's server refuses (RCPT without `@`, MAIL SIZE over CAPA SIZE):
    913, 916, 918, 3006, 3007 -> **BL-899**.
  - SMTP `HELP` should send `<data>`: 928 -> **BL-900**.
  - SMTP upload empty after `REPLY`'d MAIL and RCPT: 3215 -> **BL-896**.
  - IMAP custom `FETCH` should echo the requested number: 841, 3206, 1847 -> **BL-897**.
  - servercmd for a second named server: 779, 795 (skips) -> **BL-898**.
  - Upstream test server's arbitrary choice, added to `Upstream-Test-Case-Exclusions.md` (decision 5):
    802 (`UIDVALIDITY` value), 818 (`* 22 EXPUNGE` on `NOOP`), 861 (hard-coded `UIDL` table), 897
    (ASCII-art IMAP greeting in `-D`). They still count as failed until BL-689 reads the file.
- `upstream-test-cases-passing.win-x64.txt` is the run's own list (1,041 lines, +146 mail cases);
  pass-rate row, prose and skip table updated; `UpstreamTestCasePassRateStatementTests` green.
- Coverage (`Measure-CodeQuality -Library Surl.Conformance.UnitLibrary`): 100% line, 100% branch; its 9
  members over complexity 10 predate this task (same list as BL-394's Notes).

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-395 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. imap, pop3 and smtp upstream cases run against the mail halves: 146 of 160 pass, win-x64 list 1,041
