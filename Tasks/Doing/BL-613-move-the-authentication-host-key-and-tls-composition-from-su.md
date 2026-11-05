---
id: BL-613
title: Move the authentication, host key and TLS composition from Surl.Console into Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-609]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-613 — Move the authentication, host key and TLS composition from Surl.Console into Surl.Composition

## Goal

The compositions that read and check what `surl` is given at start - accounts and `--auth` methods, the keytab, SSH
host keys and certificates, the SSH algorithm offer, server TLS, and the data directory's probe and lock - live in
`Surl.Composition`, so `Surl.Api` can reuse them. Each returns its refusal as a `SurlExitCode` and message text
instead of writing it, and `surl`'s output and exit codes are unchanged byte for byte.

## Context

- Decision: BL-611's ADR, decisions 1 and 2 (what moves and the failure shape). The library exists from BL-609.
- What moves, from `Application/Surl.Console` (the ADR's list is authoritative): `AuthenticationComposition.cs`,
  `KeytabComposition.cs`, `RandomKerberosRandomSource.cs`, `SshHostKeyComposition.cs`, `SshAlgorithmComposition.cs`,
  `ServerTlsComposition.cs`, `DataDirectoryProbe.cs`, `DataDirectoryLock.cs`, `DataDirectoryLockOutcome.cs`, and the
  tests and fakes that cover them (`ServerTlsCompositionTests`, `DataDirectoryLockTests`, `DataDirectoryProbeTests`,
  `FakeLockHolder`, `TestCertificateFiles`, `TestKeytabFiles`, `TestSshKeyFiles`, `TestSshCertificateFiles` as
  needed).
- The parts of them that write to a `TextWriter` (`AuthenticationComposition.WriteLooseningWarnings`,
  `KeytabComposition.WriteStartLines`, `SshHostKeyComposition.WriteStartLines`) take their writer as a parameter
  today; they may move as they are, because `Surl.Api` also writes start-up warnings to a writer it is given
  (BL-611's ADR decision 6).
- Exit codes and texts are ADR-0007 section 5's and each composition's ADR (ADR-0020, ADR-0031 decision 7, ADR-0032,
  ADR-0051 decisions 4 and 6, ADR-0057 decision 1, ADR-0066); `Surl.Console`'s existing tests pin them.
- `Application/Surl.Console/CLAUDE.md` describes these types as part of `Surl.Console`; the Glossary
  (`Documentation/Wiki/Glossary.md`) names `SshHostKeyComposition` and `DataDirectoryLock` "in `Surl.Console`" - the
  `document` stage updates both.

## Acceptance criteria

- [ ] The files listed above (or the ADR's list) are in `Surl.Composition.UnitLibrary`, and none remains in
      `Application/Surl.Console`.
- [ ] Every test that covered them still exists under the same method name and passes; every
      `CommandLineRunner*Tests` class in `Surl.Console.UnitTests` passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` and
      `Surl.Console` have 100% line and branch coverage.
- [ ] `Application/Surl.Composition.UnitLibrary/CLAUDE.md`, `Application/Surl.Console/CLAUDE.md` and
      `Documentation/Wiki/Glossary.md` name the project each moved type now lives in.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
