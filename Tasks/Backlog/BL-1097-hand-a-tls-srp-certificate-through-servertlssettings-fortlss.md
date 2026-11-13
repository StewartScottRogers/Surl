---
id: BL-1097
title: Hand a TLS-SRP certificate through ServerTlsSettings.ForTlsSrp to TlsSrpServer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-911]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1097 — Hand a TLS-SRP certificate through ServerTlsSettings.ForTlsSrp to TlsSrpServer

## Goal

`ServerTlsSettings.ForTlsSrp` takes an optional `TlsSrpServerCertificate`, and `ServerTlsHandshake` hands it to `TlsSrpServer`, so a TLS-SRP listener with a certificate offers the RSA-signed SRP suites (ADR-0122 decision 2).

## Context

- Split from BL-1051 (timed out). Its run wrote this whole change; it is in the stash named
  "darkfactory BL-1051 20261112-162230" (find N with `git stash list`, matching that message).
  Take it with:
  - `git diff "stash@{N}^" "stash@{N}" -- Foundation/Surl.Networking.UnitLibrary/ServerTlsSettings.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Foundation/Surl.Networking.UnitLibrary/ServerTlsHandshake.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Foundation/Surl.Networking.UnitTests/StreamConnectionTlsSrpTests.cs | git apply`
- The stash adds `TlsSrpCertificate` (internal) and a `certificate = null` parameter to `ForTlsSrp`;
  the new test feeds the recorded `CurlTlsSrpJsmithAbc` ClientHello and reads the suite from the ServerHello.
- Then build, test, and measure coverage; that is what the timed-out run never reached.

## Acceptance criteria

- [ ] `Surl.Networking.UnitTests`: a TLS-SRP handshake with an RSA certificate in the settings
      chooses `C0 21` from pinned upstream curl's recorded ClientHello.
- [ ] `ForTlsSrp` without a certificate still serves the unsigned suites (existing tests pass unchanged).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Networking.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

- Filed by splitting BL-1051 on 2026-11-13.

## Log

- 2026-11-13: Created.
