---
id: BL-1098
title: Compose the --cert or --self-signed certificate into the TLS-SRP settings
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1097]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1098 — Compose the --cert or --self-signed certificate into the TLS-SRP settings

## Goal

`ServerTlsComposition.ComposeTlsSrp` loads the `--cert` certificate or makes the `--self-signed`
throwaway one and passes it, with its RSA key, to `ServerTlsSettings.ForTlsSrp`; a non-RSA key is
`CertificateProblem` (58), ADR-0122 decision 6.

## Context

- Split from BL-1051 (timed out). Depends on BL-1097 for `ForTlsSrp`'s certificate parameter.
- Its run wrote this change; it is in the stash named "darkfactory BL-1051 20261112-162230"
  (find N with `git stash list`, matching that message). Take it with:
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Composition.UnitTests/ServerTlsCompositionTests.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Composition.UnitTests/TestCertificateFiles.cs | git apply`
- The stash adds `ComposeTlsSrpCertificate` (loader or throwaway, then `GetRSAPrivateKey()` or a
  `TlsFileLoadException(ServerCertificateUnusable)`), owned-disposables handling, and
  `TestCertificateFiles.CreateEcdsa`. Check every new branch is covered (null `--cert`/`--self-signed`,
  intermediates, the catch path disposing what was loaded).
- `ServerTlsComposition` is built from the parsed command line, so its tests can pass `--cert` beside
  TLS-SRP only if the parser lets them; if `Surl.Cli` still refuses it, build `SurlCommandLine`
  directly in the tests rather than editing `Surl.Cli` (that is BL-1099).

## Acceptance criteria

- [ ] `Surl.Composition.UnitTests`: a `--cert` with an ECDSA key under TLS-SRP is `CertificateProblem` (58).
- [ ] `Surl.Composition.UnitTests`: TLS-SRP with `--self-signed` composes settings with the throwaway
      certificate and reports its fingerprint; with `--cert`/`--key` it composes with that certificate.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

- Filed by splitting BL-1051 on 2026-11-13.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
