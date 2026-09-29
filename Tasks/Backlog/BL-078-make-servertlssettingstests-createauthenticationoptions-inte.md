---
id: BL-078
title: Make ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext pass reliably on Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-078 — Make ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext pass reliably on Windows

## Goal

`ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext`
passes on every run of the fast tests on Windows, Linux and macOS.

## Context

- Found by BL-062 on lane 3, 2026-09-28. The test passed in one fast-test run of the
  solution, then failed on the next five runs in the same checkout with no source change
  in `Surl.Networking.*`:
  `CryptographicException: An unknown chain building error occurred.` thrown by
  `X509Chain.Build` inside `SslStreamCertificateContext.Create(target, additional, offline)`
  from `Surl.Networking.UnitLibrary/ServerTlsSettings.cs` line 54.
- The certificates come from `Surl.Networking.UnitTests/TestCertificates.cs`
  (`CreateCertificateAuthority`, `CreateSignedCertificate`), whose `Now` is
  `DateTimeOffset.UtcNow` captured once per test run.
- The failure makes `Measure-CodeQuality.ps1` refuse to measure any library, because it
  runs the whole solution's fast tests first.
- Start by finding what makes Windows' chain engine fail here (the `offline` flag, a
  freshly generated CA not in any store, a chain-engine cache) and whether
  `ServerTlsSettings` itself should tolerate it, since a real `--cert` with intermediates
  goes through the same call.

## Acceptance criteria

- [ ] `dotnet test Surl.Networking.UnitTests --filter "TestCategory!=Integration"` passes
      ten runs in a row on Windows.
- [ ] The cause and the fix are recorded under Notes.

## Notes

## Log

- 2026-09-28: Created.
