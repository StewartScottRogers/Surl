---
id: BL-1387
title: Make the IMAP max-connections conformance test wait for the SMTP send's exchange to end before the holder dials
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlReadsMailFromSurlOverImapTests.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1387 — Make the IMAP max-connections conformance test wait for the SMTP send's exchange to end before the holder dials

## Goal

`UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply`
cannot see its own holder refused, on any platform.

## Context

- Flaked on macOS in CI run 44455066818 (PR #110, 2026-11-19, attempt 1; passed on rerun): at
  `UpstreamCurlReadsMailFromSurlOverImapTests.cs:502` the holder's greeting was
  `* BYE surl Too many connections, closing`, not `* OK`.
- The test (`:492-512`) starts surl with `--max-connections 1`, sends the mail over SMTP
  (`SendMailOverSmtpAsync`), then dials the holder at once. The SMTP client has closed, but surl's SMTP exchange
  can still be counted against the limit when the holder connects: a race, likelier on slower runners. BL-1344
  fixed the other half (curl dialling before the holder's exchange is counted).

## Acceptance criteria

- [x] Before the holder dials, the test waits (bounded, e.g. 10 s, failing with a clear message) until surl
      reports no exchange in progress - preferably through the instance's event stream (the SMTP exchange's end
      event) or another observable surl already offers, not a fixed delay.
- [x] Any sibling max-connections test in the same file that seeds over another connection first gets the same wait,
      through one shared helper.
- [x] The assertions are otherwise unchanged.
- [x] `dotnet build` is clean and the fast tests are green; the changed tests pass under
      `dotnet test --filter "FullyQualifiedName~MaxConnections"` on Windows.

## Notes

- If surl offers no way for a conformance test to see an exchange's end, say so in the Log and fall back to
  retrying the holder's dial until its greeting is `* OK` (bounded), rather than adding production code here.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: surl offers a conformance test no observable for an exchange's end, so the test dials the holder
  in a bounded (10 s) retry loop until the greeting is `* OK` (`DialUntilGreetedAsync`); no production code.
  The file has no other max-connections test that seeds first.
- 2026-11-19: Doing -> Done. Holder dial retries until greeted * OK
