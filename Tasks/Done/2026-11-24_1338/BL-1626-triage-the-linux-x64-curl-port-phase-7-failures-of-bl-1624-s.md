---
id: BL-1626
title: Triage the linux-x64 curl-port-phase-7 failures of BL-1624's run against the linux-x64 findings tables
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1624]
touches: [Documentation/Conformance/CurlPort, Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1626 — Triage the linux-x64 curl-port-phase-7 failures of BL-1624's run against the linux-x64 findings tables

## Goal

Every linux-x64 failure of the `curl-port-phase-7` run BL-1624 recorded is resolved against the `### Findings (linux-x64)` tables, so the linux-x64 findings are stable from one run to the next and a rerun fails no case its family file does not list.

## Context

BL-1343 (archived in `Tasks/Done/2026-11-17_1037/`) triaged run 44044472038 into linux-x64 tables: Ldap.md L-1 to L-4, Mail.md M-1, Ftp.md FTP-1, Ssh.md SSH-2, Upstream-Test-Cases.md UTC-5. Its third box ("a rerun fails no case its family file does not list") was ticked by construction only. The first rerun says otherwise: scheduled run 45198867449 (2026-11-23, `master` at `986449355167a9c352b9d4a0fdb6bda17e9d0239`) failed 21 linux-x64 cases, mostly listed findings that no longer show ("lists …, but the port now agrees with upstream curl; remove the finding"):
- FTP: `Download_AccountOverImplicitFtps_WritesTheFile`, three `Download_AccountOverExplicitTls_WritesTheFile` rows;
- Mail: 13 cases (STARTTLS / IMAPS logins: `PLAIN after STARTTLS`, `LOGIN after STARTTLS`, `LOGIN after STARTTLS with --sasl-ir`, `AUTHENTICATE CRAM-MD5`, `the LOGIN command`, `Smtp_Send_SslReqdWithSelfSigned_…`, three `Smtp_Send_ForcedBearerMechanismAfterStartTls_…` rows, three `Imap_Fetch_ImapsForcedBearerMechanism_…` rows, `Imap_Fetch_ImapsForcedNamedChoiceMechanism_…[DIGEST-MD5]`);
- SSH: `Login_PublicKey_DownloadsTheFile ("scp")`;
- HTTP: `Head_File_ExitsZeroWithAnHttp11OkStatusLine` (an unlisted port defect);
- HTTP/3: `Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort` (an unlisted port defect);
- upstream test cases: `RunBesideAsync_EveryCaseOnThePassingList_PortDefectsAreTheListedFindings`.
Many of the listed findings are about how a TLS session or an scp channel ends, which suggests they differ from run to run rather than being the port's protocol behaviour. Compare BL-1624's run with 45198867449 and 44044472038 (all at port commit `74f3f45c…`): a difference that flips between runs for a reason that is not curl's protocol behaviour is left out of the comparison as ADR-0089 Amendments 1, 5 and 6 already do for such items (record which amendment's rule you apply in the family file); a difference that holds in every run stays a finding; a new stable one is added.

Judge by pinned upstream curl (linux-x64 reference `153CA463…4E45` and, for `OpenLdap_`, the OpenLDAP build `4418AD02…9480`), never by the Curl port (ADR-0003). Changes stay inside `Conformance/Surl.Conformance.UnitTests` and the family files. If a needed normalisation is not covered by an existing ADR-0089 amendment, do not invent one: file a Claude `docs` decision task and list it in Notes.

## Acceptance criteria

- [x] Each linux-x64 failure of BL-1624's run is listed under its family's `### Findings (linux-x64)`, removed from it as no longer a defect, left out by a named ADR-0089 amendment's rule, or fixed as a test defect; the Notes give each case's outcome.
- [x] The Notes compare BL-1624's run with runs 45198867449 and 44044472038 and name each case whose verdict differed between them.
- [x] Each family file's `## linux-x64` section says it was triaged by BL-1626 and what changed.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests (`TestCategory!=Integration`), including `CurlPortFindingsFileTests`, pass.

## Notes

- 2026-11-24: Read run 45412760425's `phase-7-linux-x64-results` trx (30 failures; the task Context's 21 were run 45198867449's). Outcomes: 
  - FTP 4 (`Download_AccountOverImplicitFtps`, 3 x `Download_AccountOverExplicitTls`): FTP-1 removed as run-to-run noise, left out by ADR-0089 Amendment 1 (how a TLS connection ended). The FTP test now passes `WithoutTlsEndings`.
  - Mail 13 (CRAM-MD5, 2 x LOGIN after STARTTLS, PLAIN after STARTTLS, the LOGIN command, SslReqdWithSelfSigned, 3 x SMTP bearer, 3 x IMAP bearer, DIGEST-MD5 named choice): M-1 removed, same rule; the Mail test's `NormaliseSide` applies `WithoutTlsEndings`.
  - Ssh 1 (`Login_PublicKey_DownloadsTheFile` scp): SSH-2 removed, Amendment 6 (teardown notes); `TeardownNote` now names `SSH channel N handler failed: The SSH channel was closed before the data was sent`.
  - Http 1, Http3 1, Http2 `GetWithHttp2AndInclude_File` 1: `ETag:` test defect, already fixed by BL-1625's normaliser; no change.
  - Http2 8 `https`-by-ALPN cases: stale on linux-x64 as on osx-arm64; BL-1625 already scoped them to `### Findings (win-x64)`; no change.
  - Http2 `GetWithDigest_PriorKnowledge` 1: finding 2 (extra RST_STREAM) also shows on linux-x64; added under `### Findings (linux-x64)`.
  - Count: 4 FTP + 13 Mail + 1 SSH + 1 Http + 1 Http3 + 10 Http2 (8 stale, 1 Digest prior knowledge, 1 ETag) = 30.
- Run comparison. 44044472038 (BL-1343): FTP-1, M-1 and SSH-2 showed (listed findings held), Http2 not executed on linux. 45198867449: FTP 4, Mail 13 and SSH scp reported stale (none of the TLS-ending / teardown notes showed), Head_File and Head_OverTcp failed on ETag, and `test251` was reported stale (UTC failed). 45412760425 (BL-1624): the same FTP, Mail, SSH and ETag verdicts as 45198867449, `test251` showed again, and the Http2 cases ran for the first time. Cases whose verdict differed between runs: the 4 FTP, 13 Mail and the scp SSH case (held in 44044472038, stale in the later two); `test251` (stale only in 45198867449). Upstream-Test-Cases UTC-5 and Ldap L-1 to L-4 held in all three.
- Decision: no new ADR; the two normalisations apply Amendments 1 and 6 to notes of the same kind. `test251` is a timing race no amendment covers, so it stays listed and BL-1647 (Claude `docs` decision) is filed.
- Port-Board-Handoff.md still names M-1, FTP-1 and SSH-2; BL-1628 owns that file.
- Build `-warnaserror` clean; Conformance fast tests 1054/1054.
## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. linux-x64 failures of run 45412760425 triaged: TLS-ending and scp teardown notes left out (Amendments 1, 6), Http2 prior-knowledge finding added, rest already fixed by BL-1625
