---
id: BL-1008
title: Serve ffdhe2048 and ffdhe3072 through a HelloRetryRequest in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1005, BL-1035]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests]
requirement: FR-055
created: 2026-11-06
completed: 2026-11-11
---
# BL-1008 — Serve ffdhe2048 and ffdhe3072 through a HelloRetryRequest in Surl.Tls13

## Goal

`Surl.Tls13` serves ffdhe2048 (`0x0100`) and ffdhe3072 (`0x0101`, RFC 7919) after ADR-0082 decision 3's five
groups: a ClientHello offering only one of them gets a HelloRetryRequest naming it and then completes, and the
recorded upstream curl ClientHello for `--curves ffdhe2048` or `--curves ffdhe3072` completes without a retry.

## Context

- Decision: the ADR BL-1005 records (decisions 1 and 2): Y and the secret padded to the length of p,
  1 < Y < p - 1 checked, a 256-bit (ffdhe2048) or 320-bit (ffdhe3072) server exponent from the injected
  `RandomNumberGenerator`, `System.Numerics.BigInteger.ModPow`; anything else `illegal_parameter`.
- First of four `Surl.Tls13` tasks split from BL-545 (timed out after 120 min, 2026-11-06); this one also
  brings in the recorded fixtures and the test-side groups machinery the later three use. Reuse from the stash
  whose message is `darkfactory BL-545 20261106-033059` (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`,
  called `S`; find its index with `git stash list`). None of these files changed on the shared branch since:
  - untracked, with `git checkout "S^3" -- <path>`: `Cryptography/Surl.Tls13.UnitLibrary/TlsFiniteFieldGroup.cs`,
    `Cryptography/Surl.Tls13.UnitTests/TlsFiniteFieldGroupTests.cs`,
    `Cryptography/Surl.Tls13.UnitTests/UnitTestFiniteFieldPrimes.cs`, and all fourteen
    `Cryptography/Surl.Tls13.UnitTests/Fixtures/ClientHello/Curves/static-8.21.0-*-curves-*.bin` (pinned
    upstream curl 8.21.0, OpenSSL 4.0.1, Windows and Linux, recorded 2026-11-06 with
    `Record-CurlExchange.ps1 -QuicInitial`).
  - `Cryptography/Surl.Tls13.UnitTests/TlsServerHandshakeTests.Groups.cs` from `S^3`, keeping only the
    `0x0100` and `0x0101` data rows (and the ffdhe-specific tests); the other groups' rows go back in with their
    own tasks.
  - tracked, from `git diff "S^" "S" -- Cryptography/Surl.Tls13.UnitLibrary Cryptography/Surl.Tls13.UnitTests`:
    apply whole the changes to `UnitTestFixtures.cs`, `TlsKeyExchangeTests.cs` and `TlsServerHandshakeTests.cs`
    (unserved-group tests move from x448 `0x001E` to ffdhe4096 `0x0102`, and the class becomes `partial`).
    From `TlsNamedGroup.cs`, `TlsServerNegotiation.cs`, `TlsKeyExchange.cs` and `UnitTestTlsClient.cs` take by
    hand only the ffdhe parts: the `Ffdhe3072` member, `Ffdhe2048, Ffdhe3072` at the end of `GroupPreference`,
    the `AgreeClassical` dispatch with `AgreeFiniteField`, and `WithFiniteFieldShare`.
- Add the ffdhe bullet of the stash's `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md` diff, citing BL-1005's
  ADR number, and widen its `illegal_parameter` row with "an ffdhe Y outside 1 < Y < p - 1".
- Stand-in: not applicable; the TLS handshake gains groups and no store, event or fault.

## Acceptance criteria

- [x] `Receive_ClientHelloOfferingOnlyOneRemainingGroupWithNoShare_RetriesForItAndCompletesOnTheSecondClientHello`
      passes for `0x0100` and `0x0101`.
- [x] `Receive_RecordedUpstreamCurlClientHelloForOneCurve_AnswersItsShareAndCompletesWithoutARetry` passes for the
      four `ffdhe2048` and `ffdhe3072` fixtures (Windows and Linux).
- [x] `Receive_RemainingGroupShareOfTheWrongLength_FailsWithIllegalParameter` passes for `(0x0100, 255)` and
      `(0x0101, 385)`, and a share Y of 1 or p - 1 fails with `illegal_parameter`.
- [x] `TlsFiniteFieldGroupTests` passes, and every existing `Surl.Tls13.UnitTests` test passes.
- [x] `dotnet build Cryptography/Surl.Tls13.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Tls13.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 6): `git cherry-pick --no-commit fd3f336f` (lane 4's work) applies cleanly onto lane 6's base, but `dotnet build` cannot run: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Parked behind BL-1035 (reinstall the SDK). Resume with `git cherry-pick --no-commit fd3f336f` (branch factory/BL-1008-lane-4-20261111-060243) and build.

- 2026-11-11 (lane 1): SDK 10.0.404 is now installed, so global.json's 10.0.401 rolls forward (latestFeature). `git cherry-pick --no-commit fd3f336f` applied cleanly; `dotnet build -warnaserror` clean, 167 Surl.Tls13 tests and all 79 fast test assemblies green, Surl.Tls13.UnitLibrary 100% line and branch coverage.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its ffdhe work and recorded fixtures from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-1008-lane-4-20261111-060243; start with git cherry-pick --no-commit factory/BL-1008-lane-4-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Tls13 serves ffdhe2048 and ffdhe3072 through a HelloRetryRequest and completes pinned upstream curl's --curves ffdhe ClientHellos
