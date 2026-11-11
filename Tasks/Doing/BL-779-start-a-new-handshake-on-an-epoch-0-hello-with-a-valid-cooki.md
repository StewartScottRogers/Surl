---
id: BL-779
title: Start a new handshake on an epoch 0 hello with a valid cookie on an established Surl.Dtls association
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-738]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-779 — Start a new handshake on an epoch 0 hello with a valid cookie on an established Surl.Dtls association

## Goal

An epoch 0 `ClientHello` arriving on an established `DtlsServerSession`, as when a client restarts, goes
through the cookie exchange, and one with a valid cookie starts a new handshake that replaces the old
association once it completes (ADR-0087 decision 6, RFC 6347 section 4.2.8).

## Context

- ADR-0087 decision 6, third bullet: no alert on the old association, and its records are still accepted
  until the new handshake completes.
- Today `DtlsServerSession.AcceptEstablishedRecord` treats an epoch 0 handshake record as the client's
  final flight sent again (it resends the server's final flight until the first application data, then
  ignores it). BL-738 added the epoch 1 `ClientHello` refusal (warning `no_renegotiation`) beside it.
- The cookie check is `DtlsCookieExchange.Answer`, used by `AwaitVerifiedClientHelloAsync`; the test
  client is `UnitTestDtlsClient` in `Surl.Dtls.UnitTests`.
- Filed by BL-738, which kept to its acceptance criteria.

## Acceptance criteria

- [ ] Named tests show: an epoch 0 `ClientHello` without a cookie on an established association answered
      with `HelloVerifyRequest` and the old association still carrying application data; one with a valid
      cookie completing a new handshake, after which application data flows under the new keys and the old
      epoch 1 keys are no longer accepted; no alert sent on the old association.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
