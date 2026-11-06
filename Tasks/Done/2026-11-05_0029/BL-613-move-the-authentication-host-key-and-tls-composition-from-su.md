---
id: BL-613
title: Move the authentication, host key and TLS composition from Surl.Console into Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-609]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Documentation/Wiki/Glossary.md, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] The files listed above (or the ADR's list) are in `Surl.Composition.UnitLibrary`, and none remains in
      `Application/Surl.Console`.
- [x] Every test that covered them still exists under the same method name and passes; every
      `CommandLineRunner*Tests` class in `Surl.Console.UnitTests` passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` and
      `Surl.Console` have 100% line and branch coverage.
- [x] `Application/Surl.Composition.UnitLibrary/CLAUDE.md`, `Application/Surl.Console/CLAUDE.md` and
      `Documentation/Wiki/Glossary.md` name the project each moved type now lives in.

## Notes

- 2026-11-04 (lane 3): `Conformance/Surl.Conformance.UnitTests/KerberosTestKdcOnLoopback.cs` constructs
  `RandomKerberosRandomSource` through `using Surl.Console;`, so moving that type into `Surl.Composition`
  needs a `using Surl.Composition;` there (and `Surl.Conformance.UnitTests` keeps building through its
  `Surl.Console` reference, which brings `Surl.Composition` transitively). Added it to `touches`. BL-393 in
  `Doing` touches the same project, so the task went back to Backlog until BL-393 is finished.
- What the first attempt did, to redo quickly: `git mv` the nine files to `Surl.Composition.UnitLibrary`
  (namespace `Surl.Composition`, types `public`; `KeytabComposition.NameEncryptionType` `public` because
  `CommandLineRunnerKeytabTests` calls it; `MapCertificateFormat`/`MapKeyFormat` stay `internal`, their tests
  move); `CommandLineRunner.DescribeTlsFileFailure` becomes `public static ServerTlsComposition.DescribeTlsFileFailure`;
  `Surl.Composition.UnitLibrary.csproj` gains references to `Surl.Kerberos`, `Surl.Networking` and `Surl.Output`;
  `ServerTlsCompositionTests`, `DataDirectoryLockTests` and `DataDirectoryProbeTests` move to
  `Surl.Composition.UnitTests` with a copy of `TestCertificateFiles` (still used by `CommandLineRunner*Tests`);
  `FakeLockHolder` stays in `Surl.Console.UnitTests` (only `CommandLineRunner*Tests` use it); `Program.cs`,
  `CommandLineRunnerAiHelpTests.cs` and the conformance file gain `using Surl.Composition;`. Coverage is
  merged across test projects by `Measure-CodeQuality.ps1`, so `CommandLineRunner*Tests` still cover the moved code.
- 2026-11-04 (lane 5): Done as the note above describes. The compositions already returned their refusals as
  `(SurlExitCode, message)` (and `DataDirectoryLockOutcome`), so no behaviour changed; the only reshaping is
  `DescribeTlsFileFailure` moving onto `ServerTlsComposition`. `CommandLineRunner*Tests` keep every method and
  assertion unchanged; the only edit to them is a `using Surl.Composition;` line, which C# needs for the moved
  types. `KerberosTestKdcOnLoopback.cs` swapped `using Surl.Console;` for `using Surl.Composition;` (it used
  nothing else from `Surl.Console`). `DataDirectoryProbe`'s coverage-exclusion justification now names
  `DataDirectoryProbeTests`, which moved with it. Measured: `Surl.Composition.UnitLibrary` 100% line and branch
  (88 members, worst CRAP 10), `Surl.Console` 100%/100% (52 members, worst CRAP 10). No ADR: ADR-0099 decision 1
  already decided the move.
- Follow-up: FR-039 in `Documentation/Product/Requirements.md` still says `SshHostKeyComposition` in
  `Surl.Console`; BL-642 (in Doing) touches that file, so BL-692 was filed for it rather than widening this task.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Backlog. Needs Conformance/Surl.Conformance.UnitTests (RandomKerberosRandomSource's using), which BL-393 in Doing also touches; resume once BL-393 is finished
- 2026-11-04: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The start compositions (authentication, keytab, SSH host keys and algorithms, server TLS, data-directory probe and lock) live in Surl.Composition; surl's output and exit codes unchanged
