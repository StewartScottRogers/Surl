---
id: BL-1527
title: Make the POP3 and SMTP max-connections conformance tests wait for the seed's exchange to end before the holder dials, as BL-1387 did for IMAP
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlReadsMailFromSurlOverPop3Tests.cs, Conformance/Surl.Conformance.UnitTests/UpstreamCurlSendsMailToSurlOverSmtpTests.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1527 — Make the POP3 and SMTP max-connections conformance tests wait for the seed's exchange to end before the holder dials, as BL-1387 did for IMAP

## Goal

`UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply`,
and any SMTP max-connections test that seeds over another connection first, can never see its own holder refused.

## Context

- CI run 44869580341 (PR #122, 2026-11-21, macos-latest Build and test): at
  `Conformance/Surl.Conformance.UnitTests/UpstreamCurlReadsMailFromSurlOverPop3Tests.cs:441` the holder's greeting
  was `-ERR surl Too many connections, closing`, not `+OK`. The test (`:432-451`) seeds over SMTP
  (`SendMailOverSmtpAsync`) under `--max-connections 1`, then dials the holder at once, while the SMTP exchange can
  still be counted.
- This is the race BL-1387 fixed for IMAP (commit `7f7889bb8`: retry the holder's dial until it is greeted `* OK`).
  The POP3 test, and possibly the SMTP one, never got the fix.
- This flake held the dark factory's merge of PR #122 (rerun started).

## Acceptance criteria

- [x] The POP3 test retries the holder's dial, bounded, until it is greeted `+OK`, the way BL-1387's IMAP fix does
      (or both share one helper).
- [x] Every other max-connections conformance test that seeds over another connection before dialling its holder
      (search `Conformance/` for `MaxConnections1WhileAnotherClientHoldsIt`) gets the same wait; the Log lists them.
- [x] Assertions are otherwise unchanged.
- [x] `dotnet build` clean and the fast tests green.

## Notes

- POP3 test now dials until greeted +OK (DialUntilGreetedAsync, 10 s bound), as BL-1387 did for IMAP.
- SMTP test (Send_MaxConnections1...) seeds nothing before its holder dials, so it cannot race; unchanged. FTP test also seeds nothing. Only POP3 and IMAP seed over SMTP.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. POP3 holder dial retries until greeted +OK; build clean, fast tests green
