---
id: BL-583
title: Measure the Curl port beside upstream curl against surl over SMTP, IMAP and POP3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverMailTests.cs, Documentation/Conformance/CurlPort/Mail.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-583 — Measure the Curl port beside upstream curl against surl over SMTP, IMAP and POP3

## Goal

`Integration` tests run every SMTP, IMAP and POP3 conversation (with `smtps`, `imaps`, `pop3s` and
STARTTLS) that pinned upstream curl already runs against a live `surl`, through both pinned upstream curl
and the pinned Curl port, using BL-578's helper. `Documentation/Conformance/CurlPort/Mail.md` lists every
port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror these existing classes in `Conformance/Surl.Conformance.UnitTests`, one Phase 7 test per
  upstream test, each naming the test it mirrors: `UpstreamCurlSendsMailToSurlOverSmtpTests`,
  `UpstreamCurlReadsMailFromSurlOverImapTests` and `UpstreamCurlReadsMailFromSurlOverPop3Tests`, with
  `StoredMail` and `AccountsFile`. Do not edit them.
- Commands before STARTTLS are plaintext and are compared through BL-579's relay. The ADR's decision 4 says
  what is compared after the upgrade.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [ ] The test class exists with one test per test in the three classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [ ] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [ ] `Documentation/Conformance/CurlPort/Mail.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [ ] Any Surl defect found is filed as a task and named in Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
