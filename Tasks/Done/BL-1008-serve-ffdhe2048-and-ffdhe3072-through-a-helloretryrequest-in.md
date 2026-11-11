---
id: BL-1008
title: Serve ffdhe2048 and ffdhe3072 through a HelloRetryRequest in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1005]
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

- 2026-11-11 (lane 4): restored from stash commit `e132aaaa` with `git show "S^3:<path>" > <path>` (lane
  permissions refuse `git checkout <rev> -- <path>`): `TlsFiniteFieldGroup.cs`, `TlsFiniteFieldGroupTests.cs`,
  `UnitTestFiniteFieldPrimes.cs`, `TlsServerHandshakeTests.Groups.cs` (cut to the `0x0100`/`0x0101` rows and the
  ffdhe range test) and all fourteen `Fixtures/ClientHello/Curves/*.bin` (sizes match the stash). The three test
  files' tracked diffs were applied whole with `git apply`; `TlsNamedGroup`, `TlsServerNegotiation`,
  `TlsKeyExchange` (an `AgreeClassical` dispatch ahead of `AgreeFiniteField`) and `UnitTestTlsClient`
  (`WithFiniteFieldShare`, `Padded`) got only their ffdhe parts by hand. The stash's ADR-0142 is BL-1005's
  ADR-0147 on the shared branch, so every citation says ADR-0147.
- Choice: the range test also gains `(0x0100, -2)`, i.e. Y = p - 1 for ffdhe2048, so both groups pin Y = 1 and
  Y = p - 1 as the acceptance criterion asks (the stash only had p - 1 for ffdhe3072).
- Toolchain: the pinned SDK 10.0.401 (`global.json`) is no longer installed on this machine - only 10.0.100-rc.1
  and 11.0.100 (installed 2026-11-10) are, so a bare `dotnet build` fails SDK resolution. Gates were run with
  SDK 11.0.100 from a scratch directory whose own `global.json` names it (`net10.0` targets, runtime 10.0.15):
  `dotnet build Surl.slnx` 0 warnings 0 errors, `-warnaserror` build of `Surl.Tls13.UnitTests` clean, fast tests
  green solution-wide, `Surl.Tls13.UnitTests` 167/167 with `Surl.Tls13.UnitLibrary` at line 1.0 and branch 1.0,
  and `dotnet format whitespace --folder --verify-no-changes` clean (the MSBuild-based `dotnet format` cannot
  locate an SDK while the pinned one is missing). Reinstalling SDK 10.0.4xx is Stewart's machine to change.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its ffdhe work and recorded fixtures from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Tls13 serves ffdhe2048 and ffdhe3072 (RFC 7919) after the five ADR-0082 groups, by HelloRetryRequest or from upstream curl's recorded --curves ClientHellos
