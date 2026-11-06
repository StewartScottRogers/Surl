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
completed:
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

- [ ] `UpstreamTestCaseServerCommandsTests` pins the `Mail` row for `imap`, `pop3` and `smtp`, equal to
      `MailUpstreamTestCaseReplies.SupportedServerCommands`, and shows `imaps` still unsupported.
- [ ] The Windows run of `UpstreamTestCasesAgainstSurlTests` with the reference pin from
      `UpstreamCurlBuilds.json` covers every runnable IMAP, POP3 and SMTP case. Notes record the pin's SHA-256,
      the date, the total ran, passed, failed and skipped, the mail family's own counts, and its skips by
      reason.
- [ ] `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` gains the mail cases that pass. The
      `win-x64` row and skip table of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` state the new
      counts, and the fast pass-rate guard test is green.
- [ ] The failing mail cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes. A cause that is the upstream test server's arbitrary choice goes to
      `Upstream-Test-Case-Exclusions.md` as decision 5 allows.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Conformance.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-395 (timed out after 120 min; Stewart asked for the split 2026-11-05).
