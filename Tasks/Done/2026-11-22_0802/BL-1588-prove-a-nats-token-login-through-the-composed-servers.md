---
id: BL-1588
title: Prove a NATS token login through the composed servers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1587]
touches: [Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1588 — Prove a NATS token login through the composed servers

## Goal

A fast test composes `--user :tok --allow-plaintext-auth nats://127.0.0.1:0` with the real `AuthenticationPolicy`
and shows a token `CONNECT` accepted and a wrong token refused, so the bug in BL-1586/BL-1587 cannot come back behind
a fake policy.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 1. The NATS
  unit tests use `UnitTestNatsAuthenticationPolicy`, and `Surl.Authentication`'s tests do not speak NATS; only the
  composition joins the CLI's `--user :tok` account, the per-scheme policy and the NATS server.
- Pattern: `Application/Surl.Composition.UnitTests/SurlServerCompositionTests.Start.cs:13-43` composes with
  `StartTestSeams` and serves through `FakeListenerFactory`, which hands a scripted `FakeConnection` per scheme
  (`FakeListenerFactory.cs`, `ConnectionsByScheme`). NATS is an extended server, so set `AreExtensionsTurnedOn = true`
  and `ComposeExtendedProtocolServers` as `ServerLifeIdentifiersCompositionTests.cs:81-91` does.
- Bytes are written from the NATS client protocol specification ("CONNECT"), never from the Curl port (ADR-0083).
- Stand-in: this is the cross-layer proof a test using `surl --user :tok nats://...` relies on; it also checks the
  `LoginJudgedEvent` the instance publishes (Account `bearer token`, Mechanism `password`, ADR-0128 decision 2).

## Acceptance criteria

- [x] A new fast test class in `Application/Surl.Composition.UnitTests` (e.g. `NatsTokenLoginCompositionTests`) with
      `ServeAsync_NatsTokenOfTheEmptyNameAccount_IsAccepted`: after `CONNECT {"verbose":true,"auth_token":"tok"}\r\n`
      the client reads `+OK\r\n`.
- [x] `ServeAsync_NatsWrongToken_IsAuthorizationViolation`: `auth_token` `nope` reads `-ERR 'Authorization Violation'\r\n`
      (use a fake `TimeProvider` or the seam's clock so the refusal delay does not slow the test).
- [x] The accepted case observes one `LoginJudgedEvent` with `IsAccepted` true, Account `bearer token`, scheme `nats`.
- [x] No test needs `TestCategory=Integration`; `dotnet build Application/Surl.Composition.UnitTests -warnaserror` clean
      and the fast tests green.

## Notes

Direct test, no ADR needed. The refusal delay did not slow the test (2 tests in ~1 s on the system clock), so no fake clock was added.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. Composed NATS token login proven: tok accepted, wrong token refused, LoginJudgedEvent observed
