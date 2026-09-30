---
id: BL-147
title: Add the Phase 2 and Phase 3 requirements to Requirements.md
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-147 — Add the Phase 2 and Phase 3 requirements to Requirements.md

## Goal

`Documentation/Product/Requirements.md` holds FR-036 to FR-047, the functional requirements
of Phase 2 (FTP, FTPS, SSH with SCP and SFTP) and Phase 3 (SMTP, IMAP, POP3 and their TLS
variants), so every Phase 2 and 3 task can cite the requirement it serves.

## Context

- Source: `Documentation/Product/Product-Overview.md`, "Phasing" rows 2 and 3, "Scope" table
  (`Surl.Protocol.Ftp`, `Surl.Protocol.Ssh`, `Surl.Protocol.Smtp`, `Surl.Protocol.Imap`,
  `Surl.Protocol.Pop3`) and "Also in scope" (authentication of the servers not yet built).
- Rules every row restates by reference, not by copy: ADR-0032 ("Protocol servers not yet
  built": every login through the contract, refused with no account, a clear password before
  TLS refused unchecked without `--allow-plaintext-auth`; SSH encrypts first, so its password is
  not plain-text), ADR-0031 (files under the data directory, service state under
  `<path>/.surl/`, in memory without `--directory`), ADR-0006 (limits, exposure defaults),
  ADR-0033 (log levels), ADR-0034 and ADR-0046 (a help category and an `--aihelp` topic per
  registered protocol).
- The planned tasks already cite these IDs, so use exactly these numbers and meanings:
  - FR-036 `surl ftp://` serves the served root over FTP: downloads (with resume and
    ranges), directory listings under `--list-directories`, uploads and file management under
    `--allow-uploads`, over passive (`EPSV`, `PASV`) and active (`EPRT`, `PORT`) data
    connections. curl feature: `curl ftp://...`, `-l`, `-T`, `-C -`, `-r`, `-P`,
    `--disable-epsv`, `--disable-eprt`, `-Q`, `--ftp-create-dirs`.
  - FR-037 FTPS: explicit TLS (`AUTH TLS`, `PBSZ`, `PROT`) on `ftp://` and implicit TLS on
    `ftps://`, with TLS data connections. curl: `--ssl-reqd`, `--ftp-ssl-control`,
    `--ftp-ssl-ccc`, `ftps://`.
  - FR-038 FTP login by `USER`/`PASS` through the authentication contract (ADR-0032).
    curl: `-u`, and curl's default anonymous login.
  - FR-039 The SSH transport for `scp://` and `sftp://`: the key exchanges, host-key
    algorithms, ciphers, MACs and compression upstream curl's libssh2 1.11.1 negotiates,
    including the hand-built `curve25519-sha256`, `ssh-ed25519` and
    `chacha20-poly1305@openssh.com`. curl: `scp://`, `sftp://`, `--hostpubsha256`,
    `--hostpubmd5`, `--compressed-ssh`, `-k`.
  - FR-040 SSH user authentication by password and public key through the contract.
    curl: `-u`, `--key`, `--pubkey`, `--pass`.
  - FR-041 SCP downloads and uploads through the content store under the exposure options.
    curl: `scp://`, `-T`.
  - FR-042 SFTP reads, directory listings, writes and curl's quote commands.
    curl: `sftp://`, `-T`, `--append`, `-C -`, `-Q`, `--ftp-create-dirs`, `--create-file-mode`.
  - FR-043 SMTP and SMTPS: receives the mail curl sends into the mail store, with `STARTTLS`
    and implicit `smtps://`. curl: `--mail-from`, `--mail-rcpt`, `--mail-rcpt-allowfails`,
    `-T`, `--ssl-reqd`.
  - FR-044 IMAP and IMAPS: mailbox listing, fetch by UID, section and partial, search,
    append, curl's custom commands, `STARTTLS`, implicit `imaps://`. curl: `imap://` URL
    forms, `-X`, `-T`.
  - FR-045 POP3 and POP3S: listing, retrieval, deletion and curl's custom commands, `STLS`,
    implicit `pop3s://`. curl: `pop3://` URL forms, `-X`, `-l`.
  - FR-046 Mail logins: IMAP `LOGIN`, POP3 `USER`/`PASS` and `APOP`, and SASL (`AUTH`,
    `AUTHENTICATE`) with the mechanisms BL-185's ADR decides, secure by default (ADR-0032).
    curl: `-u`, `--login-options`, `--sasl-ir`, `--sasl-authzid`, `--oauth2-bearer`.
  - FR-047 The mail store: kept under `<path>/.surl/mail` with `--directory`, surviving a
    restart, and in memory otherwise; bounded (ADR-0006).
- Rows are requirements, i.e. intent: Status `Draft`, Priority `Should` (as the Phase 1
  protocol rows FR-016 to FR-020), "Measured against" `curl 8.21.0 (Git for Windows)`. Do not
  write that anything is built.

## Acceptance criteria

- [ ] `Documentation/Product/Requirements.md`'s "Functional" table has rows FR-036 to FR-047,
      after FR-035, in ID order, with the meanings listed in Context and every column the
      table has filled in its existing format.
- [ ] No row states a behaviour as already built, and each row names the ADRs it relies on
      (ADR-0006, ADR-0031, ADR-0032 as they apply).
- [ ] The document's "Last updated" line (if it has one) reads 2026-09-29 or later.

## Notes

## Log

- 2026-09-29: Created.
