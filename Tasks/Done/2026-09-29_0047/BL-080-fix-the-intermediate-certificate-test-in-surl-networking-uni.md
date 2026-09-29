---
id: BL-080
title: Fix the intermediate-certificate test in Surl.Networking.UnitTests that fails chain building on Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-080 — Fix the intermediate-certificate test in Surl.Networking.UnitTests that fails chain building on Windows

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

- [x] The cause is named under Notes, with how it was reproduced.
- [x] `dotnet test Surl.Networking.UnitTests --filter "TestCategory!=Integration"` passes
      ten runs in a row on Windows.
- [x] If the fix is in `ServerTlsSettings`, `Measure-CodeQuality.ps1 -Library
      Surl.Networking.UnitLibrary` reports no failing member. (The fix is not in
      `ServerTlsSettings`: only `Surl.Networking.UnitTests` changed, so this does not apply.)

## Notes

- **Cause.** Lane 1 ran the test as it stood before `5e3d631`/`049cba0` (23:43 and 23:47
  local, minutes after its failures): the "intermediate" was a *self-signed* CA with the fixed
  subject `CN=surl test intermediate`. On Windows `SslStreamCertificateContext.Create` adds
  intermediates the OS cannot chain to `CurrentUser\CA`, and never removes them. Every run
  therefore left one more self-signed `CN=surl test intermediate` with a different key in the
  store (52 were there on this machine). Once namesakes sit in the store, the Windows chain
  engine's `CertGetCertificateChain` fails outright for a leaf issued by a new namesake, and
  .NET reports a failure with no chain status as "An unknown chain building error occurred"
  (`X509Chain.Build`). Two passes then four failures fits: the first runs had no or few namesakes.
- **Reproduced** with a throwaway `dotnet run old.cs` app outside the repository that builds
  the pre-`5e3d631` shape (self-signed `CN=surl test intermediate`, leaf under it,
  `SslStreamCertificateContext.Create(leaf, [authority], offline: true)`): runs 0 and 2 of 3
  threw exactly that exception. The current shape (root -> unique intermediate -> leaf)
  never failed: 10 project runs, the full fast suite twice, 4 parallel processes x 5 runs,
  and 6 parallel processes x 100 context builds with ~740 certificates in the store.
- **Already fixed by `049cba0`** (unique subjects, a real root-signed intermediate). What was
  still wrong: each run still leaked its unique intermediate into `CurrentUser\CA` forever.
  This task makes the test remove it again (`TestCertificates.RemoveFromWindowsIntermediateStores`,
  CurrentUser and, when writable, LocalMachine; a no-op off Windows, where the context writes
  no store). Verified: the store's `surl test` count was the same before and after ten runs.
- **Machine cleanup.** Removed 745 leaked test certificates (`CN=surl test intermediate*`
  and this task's own `CN=surl stress*`) from Stewart's `CurrentUser\CA`. Left `CN=inter` (4)
  alone: its origin is not this test. Other lanes still running pre-fix code may add a few
  more until they rebase.
- Production note, not widened into this task: `surl --cert` with an intermediate that does
  not chain to a trusted root will likewise add it to the user's CA store on Windows; that is
  .NET's documented behaviour for Schannel and harmless with real certificates.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Cause named (leaked self-signed namesakes in CurrentUser\CA from the pre-049cba0 test); the test now removes its intermediate from the store, 10/10 Windows runs green
