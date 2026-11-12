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
completed:
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

- [ ] The class exists with a named test per case above, each asserting curl's exit code and the bytes or stdout
      the ADRs say.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-663 replaced by BL-989 (BL-663 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-666 replaced by BL-992 (BL-666 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-654 replaced by BL-995 (BL-654 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-11: Backlog -> Doing.
