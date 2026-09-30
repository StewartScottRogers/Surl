---
id: BL-204
title: Answer IMAP STARTTLS, implicit imaps and AUTHENTICATE in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-201, BL-193]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md, Documentation/Planning/Decisions/README.md]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-30
---
# BL-204 — Answer IMAP STARTTLS, implicit imaps and AUTHENTICATE in Surl.Protocol.Imap

## Goal

`ImapProtocolServer` answers `STARTTLS`, serves implicit `imaps://`, and answers `AUTHENTICATE`
(with `SASL-IR`) through BL-193's SASL contract, with the capabilities BL-187's and BL-185's
ADRs give for each state.

## Context

- Decisions: BL-187's ADR (capabilities per state, `STARTTLS` without a certificate); BL-185's
  ADR (mechanisms per TLS state, plain-text rule, failures); ADR-0010 (upgrade by the server,
  bytes after the `STARTTLS` line discarded - BL-192's helper); ADR-0032 section 10 and criteria
  1 to 3; ADR-0038 (the login note); RFC 3501 section 6.2.1 and 6.2.2, RFC 4959 (`SASL-IR`).
- `imaps`: claimed by the server or wrapped by `Surl.Console` (BL-208); record which in Notes.
- Tests: `InMemoryConnection`'s upgrade simulation; a scripted double of BL-193's interface;
  BL-185's IMAP recordings and BL-187's `--ssl-reqd` and `imaps://` recordings.

## Acceptance criteria

- [x] A fast test replays each fixture named in Context and asserts surl's responses byte for
      byte.
- [x] Fast tests cover: bytes pipelined after `STARTTLS` discarded; `STARTTLS` with no
      certificate; capabilities before and after TLS and after login; `AUTHENTICATE` accepted,
      refused (with the note) and cancelled with `*`; a plain-text mechanism over no TLS refused as
      the ADR says; `LOGIN` accepted after TLS.
- [x] `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Built as ADR-0055 decisions 2, 10 and 11 and ADR-0049 sections 6 and 7 decide, the POP3 server's
  `STLS`/`AUTH` (BL-206) as the pattern. `ImapProtocolServer` gains `isStartTlsAvailable` (default
  `false`, as SMTP's and POP3's); `STARTTLS` and `AUTHENTICATE` live in
  `ImapSession.Authentication.cs`.
- `imaps`: wrapped by `Surl.Console` through `ImplicitTlsSchemeServer` (BL-208), as ADR-0055
  decision 11 says. The server claims `imap` only and tells implicit TLS apart by
  `connection.TlsSession`; the `imaps` fixture replays on a connection that is TLS from the start.
- Fixtures: BL-185 and BL-187 measured their cases into ADR-0049 and ADR-0055 but committed no
  IMAP `STARTTLS`/`AUTHENTICATE` files, so 15 were recorded with pinned curl 8.21.0 (win-x64)
  through `Record-CurlExchange.ps1 -Imap` on 2026-09-30 (no script extension needed): `starttls`,
  `starttls-logindisabled-remembered` (67), `starttls-authenticate`, `starttls-not-offered` (64),
  `imaps`, eight `authenticate-<mechanism>` cases and `authenticate-refused` (67). See
  `Surl.Protocol.Imap.UnitTests/Fixtures/README.md`.
- **Finding, recorded as ADR-0055 decision 18**: curl 8.21.0 remembers `LOGINDISABLED` seen
  before `STARTTLS`; if TLS then offers no `AUTH=`, curl exits 67 without sending `LOGIN`.
  `LOGINDISABLED` stays (leaving it out would make curl without `--ssl-reqd` send the password in
  clear). ADR-0055 and the ADR index were edited for it, so both were added to `touches`; no task
  in `Doing` names them (BL-170: SSH; BL-182: Cli/Console).
- Choices with a sensible default (in decision 18): `STARTTLS` with an argument is
  `BAD Invalid arguments`; `AUTHENTICATE` with more than a mechanism and one word is
  `BAD Invalid arguments`, and an initial response that is not base64 (`*` included) is
  `BAD Cannot decode response` without starting an exchange; after `STARTTLS` the anonymous-login
  verdict is asked again with the TLS session.
- The tests' shared `Server(store)` now uses a `ScriptedLoginPolicy` answering every login
  `AcceptedUnchecked` with no SASL offer, in place of `AnonymousAuthenticationPolicy`, whose
  `PLAIN` offer would otherwise add `AUTH=PLAIN` to every recorded capability line.
  `ScriptedLoginPolicy` gained per-TLS-state offers and scripted SASL steps.
- `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`: 100% line, 100% branch,
  497 members, 0 failing, worst CRAP 10. `Surl.Protocol.Imap.UnitTests`: 661 tests.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ImapProtocolServer answers STARTTLS, serves implicit imaps by TlsSession, and logs in with AUTHENTICATE (SASL-IR) through IMailAuthenticationPolicy, replayed against 15 new pinned-curl fixtures
