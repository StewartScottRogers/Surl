---
id: BL-779
title: Start a new handshake on an epoch 0 hello with a valid cookie on an established Surl.Dtls association
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-738, BL-1035]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Documentation/Planning/Decisions/ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md]
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

- The implementation is done on branch `factory/BL-779-lane-6-20261111-060243` (commits 6bb00f42 code
  and tests, 4eebd3fe ADR-0087 amendment; `touches` widened to ADR-0087 for it, no task in Doing names
  it). Lane 3 cherry-picked both on 2026-11-11: with `global.json` temporarily pointed at SDK 11.0.100,
  `dotnet build -warnaserror` of `Surl.Dtls.UnitTests` is clean and its 271 fast tests pass.
- What stops integration is the pinned SDK 10.0.401 missing on the dark factory machine: every build
  under the committed `global.json` fails SDK resolution. So the task waits on BL-1035. Once that is
  Done, start with `git cherry-pick --no-commit 6bb00f42 4eebd3fe`, build, test, measure coverage.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-779-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-779-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so no build under global.json can run; the work is ready on factory/BL-779-lane-6-20261111-060243 (6bb00f42, 4eebd3fe) and passes under SDK 11.
