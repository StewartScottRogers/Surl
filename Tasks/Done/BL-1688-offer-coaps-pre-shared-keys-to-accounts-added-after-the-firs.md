---
id: BL-1688
title: Offer coaps pre-shared keys to accounts added after the first flow
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0170-how-the-coaps-pre-shared-keys-the-proxy-accounts-and-the-tls-srp-accounts-are-judged-published-and-controlled.md, Documentation/Planning/Decisions/ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary/CoapDtlsCredentials.cs, Cryptography/Surl.Dtls.UnitLibrary/DtlsServerOptions.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1688 — Offer coaps pre-shared keys to accounts added after the first flow

## Goal

An account added while a `coaps` server runs can log in with its pre-shared key, even when no account was admitted
to `coaps` at the server's first flow.

## Context

- `DeferredCoapsProtocolServer.Build` runs once, at the first flow, and passes the pre-shared-key lookup only when
  `book.ForScheme("coaps").HasAccounts` is true then. With a certificate and no account at that moment, the PSK suites
  stay off for the server's life, so an account added later makes reads `4.01` (BL-1683) but cannot log in by PSK.
  ADR-0170 decisions 1 and 5 say an added account decides who may log in over `coaps`.
- With no certificate, and the last account removed before the first flow, `Build` throws `ArgumentException`, which
  the `Lazy` caches: every later flow fails even after an account is added.
- With no account but a lookup present, `Surl.Dtls` treats the PSK suites as live and prefers the certificate suites
  (`DtlsCipherSuite.AllSuitesInServerOrder`). Decide what a PSK-only client gets while no account exists, and record it
  as an ADR-0170 amendment (add that file to `touches`).
- Found while delivering BL-1683.

## Acceptance criteria

- [x] A named test in `Surl.Composition.UnitTests` serves a first `coaps` flow with a certificate and no account, adds
      an account, and shows a later pre-shared-key handshake with it completes.
- [x] A named test shows a flow that arrives with no certificate and no account does not stop a flow after an account
      is added from being served.
- [x] `dotnet build` is clean, the fast tests are green, and every changed member keeps 100% line and branch coverage.

## Notes

- Fix: `DeferredCoapsProtocolServer.Build` always passes the live pre-shared-key lookup, so `SecureCoapProtocolServer`
  never lacks a credential and the `Lazy` cannot cache that `ArgumentException`.
- Choice (ADR-0170 amendment 1): a PSK-only client while no account admits `coaps` gets `decrypt_error`, as any unknown
  identity does, rather than `handshake_failure`. Nobody is admitted, the answer no longer shows whether an account
  exists, and no `Surl.Dtls` option is needed. The `(58)` refusal at start is unchanged.
- Both new tests failed before the fix: `handshake_failure` with a certificate, `ArgumentException` without one.
- Added to `touches`: ADR-0170 (the amendment), ADR-0087 (pointer to it), and the doc comments of
  `CoapDtlsCredentials` and `DtlsServerOptions`, which said no PSK lookup exists without an account. No task in Doing
  in this checkout names them.
- `Surl.Composition.UnitTests` links `UnitTestDtlsClient` and `UnitTestDtlsDatagramFlow` from
  `Surl.Extended.Protocol.Coap.UnitTests`.
- Tests: `CoapsCompositionTests.ServeAsync_AccountAddedAfterAFirstFlowWithACertificateAndNoAccount_CompletesItsPreSharedKeyHandshake`
  and `...ServeAsync_FirstFlowWithNeitherCertificateNorAccount_StillServesAFlowAfterAnAccountIsAdded`.
- Verify: build 0 warnings; fast tests 28945 passed, 0 failed, 3 skipped; `DeferredCoapsProtocolServer` 100% line and
  branch coverage. `dotnet format --verify-no-changes` passes on the changed `.cs` files; on the whole solution it
  fails in files this task did not change (filed already: BL-1678, BL-1686).
- The `/feature` stages ran in the session without delegation (a one-expression fix); no upstream curl conformance
  run applies, since upstream curl has no `coaps` scheme (ADR-0083).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A coaps server offers pre-shared keys from the first flow on, so an account added later logs in, with or without a certificate
