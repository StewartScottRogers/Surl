---
id: BL-642
title: Decide what the mail, file, message and group options configure in the shared stores
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-622, BL-641]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-642 — Decide what the mail, file, message and group options configure in the shared stores

## Goal

An ADR decides what each layer-2 option does to the shared stores - mail domains, mailboxes, aliases,
catch-alls, quotas and shared folders in the mail store; shares, share access and buckets in the content store;
declared topics and queues, name maps and topic access on the message bus; groups and per-protocol account
restrictions - while a `surl` given none of them behaves exactly as today.

## Context

- Approved by Stewart on 2026-11-04 (layer 2); design by Claude under his delegation. The surface (option names,
  item grammar, config file, refusals, bounds) is BL-641's ADR; the stores are BL-622's ADR, ADR-0050 (mail) and
  `Foundation/Surl.Content.UnitLibrary` (files). This ADR decides only meaning.
- **The default must not move.** With no mail domain configured, ADR-0050 stays as it is: owners are account
  names, a recipient's domain is ignored (decision 5), unknown recipients are accepted and discarded, and every
  existing test and recorded upstream-curl exchange holds. Real domains and addresses apply **only when a mail
  domain is configured**. The same holds for shares (no `--share`: one served root as today), topic access (none
  configured: every logged-in account may publish and subscribe as today) and groups.
- **Oracle.** Every reply a mail server sends upstream curl for a new case (a recipient outside the configured
  domains, a mailbox over quota, a shared folder in `LIST`) is measured against pinned upstream curl 8.21.0
  (ADR-0003) and its exit code recorded: for example what `curl --mail-rcpt` does with `550 5.7.1` at `RCPT`
  (and with `--mail-rcpt-allowfails`), and what `curl imap://…/` prints for a shared folder.
- **Secure by default.** ADR-0032 section 8 forbids telling a peer which accounts exist; ADR-0050 decision 5
  applied it to recipients. Decide whether a configured domain changes that (refusing a recipient outside every
  configured domain as relaying reveals nothing about accounts; refusing an unknown local part does). A catch-all,
  a share without `readonly`, an anonymous topic grant and the like widen access: say which, and their warnings.

Decide and record:

1. **Mail.** Owners as full addresses once a domain is configured; domain matching (case, IDNA, subdomains);
   `--mailbox <addr>:<password>` - does it create the account too (planning: yes, an account named by the
   address, joining `--user`'s accounts, refused if both name it); `protocols=` restricting the account's logins;
   `quota=` per mailbox and the reply when exceeded (ADR-0050 decision 6's outcomes per protocol); `folders=`
   created at start; `--mail-alias` and `--mail-catch-all` resolution order and loops; `--shared-folder` members
   (accounts and `@group`s), how IMAP shows it (RFC 2342 `NAMESPACE`, or plain `LIST` names) and who may delete
   from it; SMTP relaying (a recipient outside every configured domain) and the reply code; how owners already in
   a persisted store under the old naming are kept (ADR-0050 decision 2's "kept, unreached").
2. **Files.** `--share <name>=<path>` as a top-level folder mapped to a disk path (refused without `--directory`?
   in memory?), `readonly`, `protocols=` per share, `--share-access` per account or group with `r`/`rw`, the
   default for an account not listed once any access is configured (planning: no access), how `--allow-uploads`
   and `--allow-anonymous` combine with share access, `--bucket <name>=<path>` (a bucket name mapped onto a content
   path for S3, Azure, Cloud Storage and OCI), and the refusal each protocol answers when access is denied (HTTP
   `403`, FTP `550`, SFTP `SSH_FX_PERMISSION_DENIED`, SMB `ERRDOS/ERRnoaccess`, … measured against upstream curl).
3. **Messages.** `--topic` pre-declaring a topic with `retain` and a maximum `qos`; `--queue` pre-declaring a
   queue, `durable`; `--topic-map` aliasing one protocol's name onto another's; `--topic-access` granting `pub`
   and `sub` per account or group; the default once any access is configured; what each broker answers on a
   denied publish or subscribe (MQTT 3.1.1 SUBACK `0x80` and ADR-0014's publish handling, measured against
   upstream curl; each extended broker per its specification).
4. **Accounts.** `--group <name>=<members>`; where groups are resolved (planning: expanded at composition, so
   stores see only account names); `@name` syntax in member lists; how `protocols=` is enforced (planning: the
   composition hands each server a view of `AuthenticationPolicy` that only sees accounts allowed for its
   scheme, so no contract in `Surl.Protocol.Abstractions` changes).
5. **What `Surl.Composition` composes** from each option, and what `surl -v` notes at start for each.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation", citing Stewart's approval of 2026-11-04, BL-622's and BL-641's ADRs.
      It answers decisions 1 to 5 and is listed in `Documentation/Planning/Decisions/README.md`.
- [ ] Every new wire reply to an original-protocol client cites a measurement of the pinned upstream curl 8.21.0
      build (path and SHA-256) with curl's exit code; every extended-protocol reply cites its specification.
- [ ] The ADR states, in one paragraph, that a `surl` given no layer-2 option behaves as before, and names the
      existing test classes that prove it (e.g. `UpstreamCurlSendsMailToSurlOverSmtpTests`).
- [ ] `ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md` carries an "Amended" line naming
      this ADR for decisions 2 and 5.
- [ ] `Documentation/Product/Requirements.md`'s rows added by BL-622 and BL-641 cite this ADR where it applies.
- [ ] The ADR's Consequences section lists the build tasks by ID (BL-646 to BL-666) and names any whose Goal it
      changes.

## Notes

## Log

- 2026-11-04: Created.
