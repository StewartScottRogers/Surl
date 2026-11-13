---
id: BL-667
title: Prove with pinned upstream curl a surl configured from a -K file with mail domains, mailboxes, shares and topics
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-644, BL-995, BL-989, BL-992]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-667 — Prove with pinned upstream curl a surl configured from a -K file with mail domains, mailboxes, shares and topics

## Goal

A conformance test class starts one `surl` from a `-K` config file that configures a mail domain, mailboxes, a
share with per-account access and a topic grant, and shows pinned upstream curl 8.21.0 completing - or being
refused as the ADRs say - the exchanges a real deployment would see.

## Context

- Decisions: BL-641's ADR (config file), BL-642's ADR (what each option means, with its measured replies).
  Planning class name: `UpstreamCurlTalksToAConfiguredSurlTests` in `Conformance/Surl.Conformance.UnitTests`.
- Cases: `curl --mail-rcpt bob@example.test -T m smtp://…` delivered and read by bob over `imap`; a recipient in
  another domain refused with the measured reply and curl's measured exit code; an alias delivered; a mailbox
  restricted to `imap,pop3` refused over another protocol; an `r` account fetching from a share over `http` and
  `ftp` and refused an upload; an `rw` account uploading over `ftp` and the file read over `http`; an `mqtt`
  subscribe refused for an account without `sub`; a config file line in error refused with `<file>:<line>`.
- Helpers: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`, `StoredMail.cs`. Missing pinned build
  is Inconclusive. Platform-neutral paths; the config file is written to a temporary directory.

## Acceptance criteria

- [x] The class exists with a named test per case above, each asserting curl's exit code and the bytes or stdout
      the ADRs say.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Added `UpstreamCurlTalksToAConfiguredSurlTests` (9 Integration tests, all green against pinned curl 8.21.0): mail delivered and read over imap, other-domain RCPT exit 55 "RCPT failed: 550", alias, mailbox restricted to imap,pop3 (smtp login exit 67), share r account over http and ftp plus refused ftp upload (exit 25), rw account upload then http read, mqtt subscribe without sub (exit 8) and publish with pub, and a bad config line refused with `<file>:3:` (physical line number, ADR-0101 row 7; needs no curl).
- Choices: config values holding a space (a Windows temp path) are double-quoted with forward slashes; `allow-plaintext-auth` is set for ftp, http and mqtt configs, as logins over plain connections are refused otherwise (mqtt CONNACK 5, ftp 530).

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-663 replaced by BL-989 (BL-663 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-666 replaced by BL-992 (BL-666 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-654 replaced by BL-995 (BL-654 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. UpstreamCurlTalksToAConfiguredSurlTests proves pinned curl against a surl configured from a -K file: mail domain, mailboxes, alias, shares with access, topic grant, config error with file:line
