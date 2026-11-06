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
completed: 2026-11-05
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

- [x] The test class exists with one test per test in the three classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Mail.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Built: `CurlPortBesideUpstreamCurlOverMailTests`, 83 tests (24 SMTP, 31 IMAP, 28 POP3; 121 runs with data
  rows), each `Smtp_`/`Imap_`/`Pop3_` + the mirrored method's name, with a `<see cref>` doc comment naming the
  mirrored test. Measured 2026-11-05 on Windows, port b42c319b, upstream 8.21.0 reference build: every run is
  **agreement**; upstream's run matched every mirrored assertion, so **no Surl defect** and nothing filed.
  `Mail.md` lists no findings and holds the table the tests read.
- Choices with a sensible default, all inside ADR-0089 (no new ADR):
  - `PinnedCurlPortBesideUpstreamCurl` handles one command line on one listener. The mail cases need `smtp`
    beside `imap`/`pop3`, an inbox seeded first, several client runs in a row, a held connection and the mail
    store read after `surl` stops, so the class runs those itself and hands each client run to
    `CurlPortComparison.Compare`. The helper's library and the ADR stay as they are.
  - The seed is delivered by upstream curl in both runs: it is the case's content, not what is measured.
  - Multi-run cases (append/fetch, create/list, store/expunge, dele/list) compare every run; stored mail and
    `surl` notes are compared on the last.
  - Request bytes are not recorded for a conversation that upgrades with `STARTTLS`/`STLS` (the
    `RecordsClientBytes` default the case documents); the plaintext twin compares curl's own bytes. ADR-0089
    decision 4 compares nothing after the upgrade, and the relay cannot cut a transcript at the upgrade
    without a change to `Surl.Conformance.UnitLibrary`, outside this task's `touches`.
  - Extra normalisation for values the clock or `surl` picks per run (listed in `Mail.md`): the `Received:`
    date, `UIDVALIDITY`, the CRAM-MD5 and APOP digests (surl's random challenge), DIGEST-MD5's server `nonce`,
    and OAUTHBEARER's GS2 `port=`. The first full run without them showed 7 false port defects, all of these.
  - A data row's case name is the method name plus its values in parentheses; the findings table's first
    column uses that name.
  - The findings file is read from the repository root; a row counts when its first cell is in backticks.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. 83 Phase 7 tests run every SMTP, IMAP and POP3 conversation through pinned upstream curl and the pinned Curl port; all agree, Mail.md records no findings
