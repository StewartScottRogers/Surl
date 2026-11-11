---
id: BL-779
title: Start a new handshake on an epoch 0 hello with a valid cookie on an established Surl.Dtls association
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-738]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Documentation/Planning/Decisions/ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
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

- [x] Named tests show: an epoch 0 `ClientHello` without a cookie on an established association answered
      with `HelloVerifyRequest` and the old association still carrying application data; one with a valid
      cookie completing a new handshake, after which application data flows under the new keys and the old
      epoch 1 keys are no longer accepted; no alert sent on the old association.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Design: `DtlsServerSession` now keeps each association's records, keys and handshake state in a
  private nested `Association`; `current` carries application data, `handshaking` is what the running
  handshake builds. A valid-cookie epoch 0 hello in `ReceiveAsync` runs `CompleteHandshakeAsync` into a
  new `Association` (fresh epoch 0 replay window, since a restarted client numbers from 0 again) and
  swaps it in on completion; failure, timeout or cancellation restores the old one.
- While the new handshake runs, each epoch 1 record is offered to the old association first (its
  application data is queued, anything else it authenticates is dropped), so old-key records never
  count as the new `Finished` failing its tag. Recorded as ADR-0087's BL-779 amendment.
- `touches` widened to ADR-0087 to record the amendment; no task in Doing names it.
- Tests: `DtlsServerSessionRestartedClientTests` (4 tests). `UnitTestDatagramFlow.Client` is now
  settable so a test can swap in a restarted client on the same endpoint.
- Verification: the pinned SDK 10.0.401 is not installed on this machine (BL-1035), so `dotnet build
  -warnaserror`, the fast tests (all green, Dtls 271) and `dotnet format --verify-no-changes` ran under
  the installed SDK 11.0.100 with `global.json` overridden temporarily and restored, never committed.
  Coverage of `Surl.Dtls.UnitLibrary`: 100% line, 100% branch. CI checks it again on the pinned SDK.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. An epoch 0 hello on an established DTLS association goes through the cookie exchange, and one with a valid cookie runs a new handshake that replaces the old association
