---
id: BL-206
title: Answer POP3 STLS, implicit pop3s, AUTH and APOP in Surl.Protocol.Pop3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-205, BL-193]
touches: [Surl.Protocol.Pop3.UnitLibrary, Surl.Protocol.Pop3.UnitTests]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-30
---
# BL-206 — Answer POP3 STLS, implicit pop3s, AUTH and APOP in Surl.Protocol.Pop3

## Goal

`Pop3ProtocolServer` answers `STLS`, serves implicit `pop3s://`, and logs users in with `AUTH`
(RFC 5034, SASL through BL-193's contract) and `APOP` (RFC 1939 section 7, through the same
contract), with the `CAPA` list BL-188's and BL-185's ADRs give for each state.

## Context

- Decisions: BL-188's ADR (`CAPA` per state, `STLS` without a certificate); BL-185's ADR
  (mechanisms per TLS state, plain-text rule, `APOP`, failures); ADR-0010 (upgrade by the
  server, bytes after the `STLS` line discarded - BL-192's helper); ADR-0032 section 10 and
  criteria 1 to 3; ADR-0038.
- `APOP`'s digest is checked by the policy against the timestamp the greeting sent; the server
  passes that timestamp to the contract.
- `pop3s`: claimed by the server or wrapped by `Surl.Console` (BL-209); record which in Notes.
- Tests: `InMemoryConnection`'s upgrade simulation; a scripted double of BL-193's interface;
  BL-185's POP3 recordings and BL-188's `--ssl-reqd` and `pop3s://` recordings.

## Acceptance criteria

- [x] A fast test replays each fixture named in Context and asserts surl's replies byte for byte.
- [x] Fast tests cover: bytes pipelined after `STLS` discarded; `STLS` with no certificate;
      `CAPA` before and after TLS; `AUTH` accepted, refused (with the note) and cancelled with
      `*`; an initial response; `APOP` accepted and refused; a plain-text mechanism over no TLS
      refused as the ADR says.
- [x] `dotnet build Surl.Protocol.Pop3.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Built as ADR-0056 decisions 2, 3, 4 and 8 and ADR-0049 sections 6 and 7 decide; no new ADR was
  needed. `Pop3ProtocolServer` gains `isStlsAvailable` (default `false`, as SMTP's
  `isStartTlsAvailable`) and an optional `RandomNumberGenerator` for the `APOP` timestamp
  (default: `RandomNumberGenerator.Create()`); `Surl.Console` sets the flag in BL-209.
- `pop3s`: wrapped by `Surl.Console` through `ImplicitTlsSchemeServer` (BL-209), as ADR-0056
  decision 8 says. The server claims `pop3` only and tells implicit TLS apart by
  `connection.TlsSession`; the `pop3s` fixture replays on a connection that is TLS from the start.
- Choices with a sensible default: `STLS` with an argument is `-ERR Invalid arguments`;
  `AUTH` with more than a mechanism and one initial response is `-ERR Invalid arguments`, and an
  initial response that is not base64 (or `*`) is `-ERR Cannot decode response` without starting
  an exchange; `APOP` without exactly a name and a digest is `-ERR Invalid arguments`. A
  `Challenge` handed back by `CheckApopLoginAsync`, which the contract rules out, is answered as a
  failed login. After `STLS` the anonymous-login verdict is asked again with the TLS session.
- Fixtures were recorded with pinned curl 8.21.0 (win-x64) through `Record-CurlExchange.ps1 -Pop3`
  on 2026-09-30, since BL-185 and BL-188 measured these cases into their ADRs but committed no
  POP3 fixture files: `stls`, `stls-not-offered` (exit 64), `pop3s`, `apop`, `apop-forced`,
  `apop-refused` (67), `auth-refused` (67) and 13 `auth-<mechanism>` cases. The script needed no
  extension. See `Surl.Protocol.Pop3.UnitTests/Fixtures/README.md`.
- Learned: `ReadOnlyMemory<byte>? x = cond ? null : someMemory` makes `null` an *empty memory*
  (the literal converts through `byte[]`), which would have turned "no initial response" into
  `=`; a test caught it and the session assigns explicitly.
- `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary`: 100% line, 100% branch,
  97 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pop3ProtocolServer answers STLS, serves implicit pop3s by TlsSession, and logs in with SASL AUTH and APOP through IMailAuthenticationPolicy, replayed against 20 new pinned-curl fixtures
