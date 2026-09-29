---
id: BL-076
title: Fix the intermediate-certificate test in Surl.Networking.UnitTests that fails chain building on Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-076 — Fix the intermediate-certificate test in Surl.Networking.UnitTests that fails chain building on Windows

## Goal

`ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext`
passes on every run, on Windows, Linux and macOS.

## Context

- Found by BL-032 on lane 1, 2026-09-28 around 23:40 local (06:40 UTC on 2026-09-29): the
  test passed twice in the fast run, then failed on every one of the next four runs with
  `System.Security.Cryptography.CryptographicException: An unknown chain building error
  occurred.` from `settings.CreateAuthenticationOptions([]).ServerCertificateContext`
  (`Surl.Networking.UnitTests/ServerTlsSettingsTests.cs:63`). No code in
  `Surl.Networking.*` had changed; BL-032 touched only `Surl.Core.*`.
- The certificates come from `TestCertificates` (`Surl.Networking.UnitTests/TestCertificates.cs`):
  a throwaway authority and a leaf valid `Now` ± 1 day, `Now` being
  `DateTimeOffset.UtcNow` at type load. `SslStreamCertificateContext.Create` builds the
  chain against the real clock and the machine's stores, so suspect whatever the Windows
  chain engine does with an untrusted, self-issued intermediate (offline revocation,
  AIA fetch, a store write), not the dates alone.
- A red Windows fast run blocks every lane's integration, so this is High.

## Acceptance criteria

- [ ] The cause is named under Notes, with how it was reproduced.
- [ ] `dotnet test Surl.Networking.UnitTests --filter "TestCategory!=Integration"` passes
      ten runs in a row on Windows.
- [ ] If the fix is in `ServerTlsSettings`, `Measure-CodeQuality.ps1 -Library
      Surl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-09-28: Created.
