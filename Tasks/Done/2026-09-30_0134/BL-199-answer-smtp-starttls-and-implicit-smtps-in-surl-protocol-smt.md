---
id: BL-199
title: Answer SMTP STARTTLS and implicit smtps in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-198]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: FR-043
created: 2026-09-29
completed: 2026-09-30
---
# BL-199 — Answer SMTP STARTTLS and implicit smtps in Surl.Protocol.Smtp

## Goal

`SmtpProtocolServer` answers `STARTTLS` (RFC 3207) and serves implicit `smtps://`, with the
capability list and session state BL-186's ADR gives for each TLS state, so
`curl --ssl-reqd smtp://...` and `curl smtps://...` can deliver.

## Context

- Decisions: BL-186's ADR (replies, capabilities after TLS, `STARTTLS` without a certificate);
  ADR-0010 (the server calls `IConnection.UpgradeToTlsAsync` after `220`, discarding every byte
  read past the `STARTTLS` line - BL-192's helper); RFC 3207 section 4.2 (the session resets
  after the upgrade: the client must `EHLO` again); ADR-0032 section 10 (no certificate: the
  upgrade refused in SMTP's own words).
- `smtps`: the engine completes the implicit handshake before `ServeAsync`, so the server claims
  `smtps` too (or `Surl.Console` wraps it, as BL-207 decides); record which in Notes.
- Tests: `InMemoryConnection`'s upgrade simulation (ADR-0010); fixtures from BL-186's
  `--ssl-reqd` and `smtps://` recordings.

## Acceptance criteria

- [x] A fast test replays the `--ssl-reqd` and `smtps://` fixtures and asserts surl's replies and
      upgrade point.
- [x] Fast tests cover: bytes pipelined after `STARTTLS` discarded, never run; `STARTTLS` twice;
      `STARTTLS` with no certificate; a failed upgrade; `MAIL` before the new `EHLO`; the
      capability list before and after TLS.
- [x] `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- **Fixtures.** BL-186 committed none, so `Fixtures/starttls` (`-k --ssl-reqd`, ADR-0053 row 40)
  and `Fixtures/smtps` (row 44) were recorded here with `Record-CurlExchange.ps1 -Smtp` (and
  `-Tls`) against pinned curl 8.21.0, fed exactly the replies Surl sends; both exit 0. The
  command lines are in `Fixtures/README.md`. The recorder needed no change.
- **`smtps`.** As ADR-0053 decision 5 already decides: `SmtpProtocolServer` claims `smtp` only,
  and `Surl.Console` registers it for `smtps` through `ImplicitTlsSchemeServer` (BL-207). The
  server tells TLS by `connection.TlsSession`, so the `smtps` replay is the same server on a
  connection that is TLS from the start. No new ADR: every choice below is a default inside
  ADR-0053.
- **Defaults taken:**
  - `isStartTlsAvailable` is an optional constructor parameter defaulting to `false` (no
    certificate, the secure answer `454`), so BL-198's call sites and tests stay as they were.
  - `STARTTLS` is checked in the order argument (`501`), already TLS (`503`), no certificate
    (`454`).
  - The handshake runs on the exchange's own cancellation token; a handshake cut off by the
    engine's limits ends as any cancelled exchange does, and a failed one throws
    `TlsHandshakeException` out of `ServeAsync` for the engine's `TLS handshake failed:` note.
  - The upgrade point is pinned by `UpgradePointRecordingConnection` (tests only), which fails
    a test if the server reads the post-`STARTTLS` chunk before upgrading.
- **Measured:** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`: 100% line,
  100% branch, 75 members, 0 failing, worst CRAP 10. `Surl.Protocol.Smtp.UnitTests`: 146 tests.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SmtpProtocolServer answers STARTTLS (220, discard, upgrade, session reset; 454 without a certificate, 503 on TLS) and serves smtps on an implicit-TLS connection, replaying pinned curl 8.21.0's --ssl-reqd and smtps:// fixtures
