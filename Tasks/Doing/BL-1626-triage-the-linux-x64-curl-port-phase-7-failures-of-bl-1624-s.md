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
completed:
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

- [ ] Each linux-x64 failure of BL-1624's run is listed under its family's `### Findings (linux-x64)`, removed from it as no longer a defect, left out by a named ADR-0089 amendment's rule, or fixed as a test defect; the Notes give each case's outcome.
- [ ] The Notes compare BL-1624's run with runs 45198867449 and 44044472038 and name each case whose verdict differed between them.
- [ ] Each family file's `## linux-x64` section says it was triaged by BL-1626 and what changed.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests (`TestCategory!=Integration`), including `CurlPortFindingsFileTests`, pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
