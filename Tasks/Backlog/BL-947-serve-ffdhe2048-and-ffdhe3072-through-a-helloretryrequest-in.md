---
id: BL-947
title: Serve ffdhe2048 and ffdhe3072 through a HelloRetryRequest in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-943]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-947 — Serve ffdhe2048 and ffdhe3072 through a HelloRetryRequest in Surl.Tls13

## Goal

`Surl.Tls13` serves ffdhe2048 (`0x0100`) and ffdhe3072 (`0x0101`, RFC 7919) after ADR-0082 decision 3's five
groups: a ClientHello offering only one of them gets a HelloRetryRequest naming it and then completes, and the
recorded upstream curl ClientHello for `--curves ffdhe2048` or `--curves ffdhe3072` completes without a retry.

## Context

- Decision: the ADR BL-943 records (decisions 1 and 2): Y and the secret padded to the length of p,
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
- Add the ffdhe bullet of the stash's `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md` diff, citing BL-943's
  ADR number, and widen its `illegal_parameter` row with "an ffdhe Y outside 1 < Y < p - 1".
- Stand-in: not applicable; the TLS handshake gains groups and no store, event or fault.

## Acceptance criteria

- [ ] `Receive_ClientHelloOfferingOnlyOneRemainingGroupWithNoShare_RetriesForItAndCompletesOnTheSecondClientHello`
      passes for `0x0100` and `0x0101`.
- [ ] `Receive_RecordedUpstreamCurlClientHelloForOneCurve_AnswersItsShareAndCompletesWithoutARetry` passes for the
      four `ffdhe2048` and `ffdhe3072` fixtures (Windows and Linux).
- [ ] `Receive_RemainingGroupShareOfTheWrongLength_FailsWithIllegalParameter` passes for `(0x0100, 255)` and
      `(0x0101, 385)`, and a share Y of 1 or p - 1 fails with `illegal_parameter`.
- [ ] `TlsFiniteFieldGroupTests` passes, and every existing `Surl.Tls13.UnitTests` test passes.
- [ ] `dotnet build Cryptography/Surl.Tls13.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Tls13.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its ffdhe work and recorded fixtures from stash `darkfactory BL-545 20261106-033059`.
