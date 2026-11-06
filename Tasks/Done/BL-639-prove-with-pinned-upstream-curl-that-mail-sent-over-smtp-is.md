---
id: BL-639
title: Prove with pinned upstream curl that mail sent over SMTP is read over IMAP and POP3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-622]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-639 — Prove with pinned upstream curl that mail sent over SMTP is read over IMAP and POP3

## Goal

A conformance test class shows, with pinned upstream curl 8.21.0 at both ends against one `surl` serving `smtp`,
`imap` and `pop3` (and their TLS schemes), that mail sent over SMTP is read byte for byte over IMAP and POP3.

## Context

- Decision: BL-622's ADR, decision 5 (planning class `UpstreamCurlSharesMailAcrossSurlProtocolsTests` in
  `Conformance/Surl.Conformance.UnitTests`). The store is ADR-0050's `MailboxStore`, already composed once
  (BL-207). `UpstreamCurlReadsMailFromSurlOverImapTests.FetchByUid_MailSentOverSmtp_…` already proves one IMAP
  case; do not duplicate it - this class covers the matrix.
- Cases: `curl --mail-from a@x --mail-rcpt alice@x -T m smtp://…` then `curl -u alice:… imap://…/INBOX;UID=1` and
  `curl -u alice:… pop3://…/1`; two recipients each reading their own copy; `smtps` then `imaps`/`pop3s` with
  `--self-signed` and curl `-k`; with `--directory` across a `surl` restart; POP3 `DELE` then IMAP no longer
  finding it. Byte expectations follow ADR-0050 decision 3 (the store adds nothing) and the SMTP ADR's trace
  fields (ADR-0053).
- Helpers: `StoredMail.cs`, `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`. Missing pinned
  build is Inconclusive. Platform-neutral.

## Acceptance criteria

- [x] The class exists with a named test per case above, each asserting curl's exit codes and the fetched message
      bytes equal to what ADR-0050/ADR-0053 say the store holds.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- `Conformance/Surl.Conformance.UnitTests/UpstreamCurlSharesMailAcrossSurlProtocolsTests.cs`, five
  `Integration` tests, all passing live against pinned curl 8.21.0 (win-x64) on 2026-11-05:
  `SendOverSmtp_ThenReadOverImapAndPop3_…`, `SendOverSmtpToTwoRecipients_EachReadsTheirOwnCopyOverImapAndPop3`,
  `SendOverSmtps_ThenReadOverImapsAndPop3s_…` (`--self-signed`, curl `-k`, trace word `ESMTPSA`),
  `SendOverSmtp_ThenRestartSurlOverTheSameDirectory_…` (first surl serves `smtp` only, second `imap`+`pop3`),
  `DeleOverPop3_ThenFetchOverImap_ExitsRemoteFileNotFoundAndTheStoreHoldsNothing` (curl exit 78).
- Choice: a test-local `--user-file` with two accounts, `alice` and `bob`, rather than widening the shared
  `AccountsFile` helper (one account), so the two-recipient case needs no change outside this class. SMTP
  logs in as alice in every case; bob's copy reaches bob by local part (ADR-0050 decision 5).
- Every read is compared byte for byte with the stopped surl's persisted store (`StoredMail`), and the stored
  bytes are checked to be ADR-0053 decision 6's two trace fields followed by `mail.txt` and nothing else.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Pinned upstream curl sends over smtp/smtps and reads the same stored bytes over imap, pop3, imaps and pop3s, across a restart, with POP3 DELE seen by IMAP
