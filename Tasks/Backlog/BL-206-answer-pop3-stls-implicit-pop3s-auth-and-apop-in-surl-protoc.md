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
completed:
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

- [ ] A fast test replays each fixture named in Context and asserts surl's replies byte for byte.
- [ ] Fast tests cover: bytes pipelined after `STLS` discarded; `STLS` with no certificate;
      `CAPA` before and after TLS; `AUTH` accepted, refused (with the note) and cancelled with
      `*`; an initial response; `APOP` accepted and refused; a plain-text mechanism over no TLS
      refused as the ADR says.
- [ ] `dotnet build Surl.Protocol.Pop3.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
