---
id: BL-1344
title: Make the POP3, IMAP and SMTP max-connections tests hold their one connection before upstream curl dials, as BL-1320 did for FTP
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlReadsMailFromSurlOverPop3Tests.cs, Conformance/Surl.Conformance.UnitTests/UpstreamCurlReadsMailFromSurlOverImapTests.cs, Conformance/Surl.Conformance.UnitTests/UpstreamCurlSendsMailToSurlOverSmtpTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverMailTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1344 — Make the POP3, IMAP and SMTP max-connections tests hold their one connection before upstream curl dials, as BL-1320 did for FTP

## Goal

Every `*_MaxConnections1WhileAnotherClientHoldsIt_*` test in the mail families gives the same verdict on every run on Windows, Linux and macOS.

## Context

- Merge PR #102 (2026-11-17), "Build and test (macos-latest)", run 44052838598, job 133935384332: `UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply` expected the holder's `+OK`, got `-ER`. It is the same timing race BL-1320 fixed for FTP. The test let upstream curl dial before the first client's exchange was proven live, so on macOS curl sometimes took the one slot.
- BL-1320 (Done, archived 2026-11-16_1750) fixed `UpstreamCurlFetchesFromSurlOverFtpTests` by proving the holder's exchange is live (`NOOP` -> 200) before curl dials. Reuse that approach. If a shared helper fits, put it in one place for FTP and the mail families.
- The same test shape exists in `UpstreamCurlReadsMailFromSurlOverImapTests.cs`, `UpstreamCurlSendsMailToSurlOverSmtpTests.cs` and the port mirror `CurlPortBesideUpstreamCurlOverMailTests.cs`. Fix them all, not only POP3.
- No sleeps or fixed delays (CLAUDE.md, testing rules). Wait on the holder's reply (POP3 `NOOP` -> `+OK`, IMAP `NOOP` -> tagged `OK`, SMTP `NOOP` -> `250`).

## Acceptance criteria

- [x] Each mail-family max-connections test proves the holder's session answers a command before upstream curl dials, with no sleep or fixed delay.
- [x] Each passes 20 consecutive local runs on Windows, and CI's macOS and Linux build-and-test and published-binary conformance jobs pass on the task's commit.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- POP3 refuses `NOOP` before login (`-ERR [AUTH]`), so the POP3 holder (and the port mirror's Greeting hold, which picks the command from the greeting) sends `CAPA` -> `+OK`, which is allowed unauthenticated. IMAP uses `h1 NOOP` -> `h1 OK`, SMTP `NOOP` -> `250`.
- Local proof: 20 consecutive runs of all 8 `*MaxConnections1WhileAnotherClientHoldsIt*` tests passed on Windows. The macOS and Linux jobs run on the shift's merge CI; a local lane cannot produce them.

## Log

- 2026-11-17: Created from merge PR #102's macOS failure (same race as BL-1320).
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. POP3, IMAP, SMTP and port-mirror max-connections tests prove the holder's session is live before curl dials; 20/20 local runs
