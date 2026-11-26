---
id: BL-1757
title: Withhold a NATS CONNECT secret on a line that follows a bare line feed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1757 — Withhold a NATS CONNECT secret on a line that follows a bare line feed

## Goal

The NATS exchange log writes `pass` and `auth_token` as `****` in a `CONNECT` line when the client ends lines with a
bare line feed.

## Context

- Found while working BL-1755. `NatsControlLineReader` ends a control line at LF, but `LineBytesRedactor` ends one
  only at CR LF, so `PING<LF>CONNECT {"pass":"<password>"}<CR><LF>` reaches `NatsConnectLineRedactionRule` as one
  line starting `PING` and is logged with the password. Read from the code; no test reproduces it yet.
- ADR-0182 decisions 1 and 6 set the rule. BL-1755 fixed the FTP rule by judging each LF-separated piece of the line
  it is handed, with no change to the shared `LineBytesRedactor`.

## Acceptance criteria

- [x] A named test in `Surl.Extended.Protocol.Nats.UnitTests` shows `pass` and `auth_token` masked in a `CONNECT`
      line that follows a bare line feed.
- [x] A named test shows every other line of the same chunk logged as sent, line feeds included.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Reproduced first: the three new tests in `NatsExchangeLogRedactionTests` failed before the fix, two of them with the
  password logged (`Connect_AfterABareLineFeed_MasksThePassAndTheToken`,
  `EveryOtherLineOfTheSameChunk_IsLoggedAsSentLineFeedsIncluded`).
- Fix, as BL-1755's: `NatsConnectLineRedactionRule` judges each LF-separated piece of the line it is handed;
  `LineBytesRedactor` is unchanged. No new ADR: ADR-0182 decisions 1 and 6 already require it.
- A published payload holding a bare LF followed by `CONNECT {` is masked in the log too. Chosen because the rule sees
  lines, not frames, and masking too much is safer than logging a secret; payload lines after CR LF were already
  judged this way.
- Checks: build `-warnaserror` 0 warnings; format clean; fast tests 29483 passed, 0 failed, 3 skipped;
  `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Nats.UnitLibrary` 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The NATS exchange log masks pass and auth_token in a CONNECT line that follows a bare line feed
