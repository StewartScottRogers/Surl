---
id: BL-250
title: Serve --allow-weak-ssh-algorithms in surl once the SSH server offers the weak algorithms
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-221]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-041
created: 2026-09-30
completed: 2026-09-30
---
# BL-250 — Serve --allow-weak-ssh-algorithms in surl once the SSH server offers the weak algorithms

## Goal

`surl --allow-weak-ssh-algorithms --hostkey <file> sftp://...` starts, passes the option to the
SSH server so its offer holds ADR-0051 decision 2's weak algorithms, and writes decision 11's
warning, instead of refusing the option as not available in this build.

## Context

- BL-171 composed the SSH server but kept `--allow-weak-ssh-algorithms` refused
  (`CommandLineRunner.FindUnavailableOption`), because until BL-221 the server offers none of the
  weak algorithms and decision 11's warning ("SHA-1, MD5, CBC, RC4, 3DES and 1024-bit
  Diffie-Hellman SSH algorithms are offered") would be false. `SshHostKeyComposition` already
  passes the option to `SshHostKeyFile.Read`, so short RSA host keys are read with it.
- Code: `Surl.Console/CommandLineRunner.cs` (`UnavailableOptions`, `ComposeProtocolServers`:
  build the offer with whatever setting BL-221 adds), `Surl.Console/AuthenticationComposition.cs`
  `WriteLooseningWarnings` or `SshHostKeyComposition.WriteStartLines` for the warning (after the
  `--throwaway-hostkey` line, decision 11's order). `Surl.Cli.UnitLibrary`: `AiHelpProse`
  (security topic), `ManualText` (SSH OPTIONS, and the LOOSENING OPTIONS wording if it becomes a
  warned option), `CommandLineOptions` (its row).

## Acceptance criteria

- [x] A fast `Surl.Console.UnitTests` test shows `--allow-weak-ssh-algorithms` with an `sftp://`
      URL starts a listener, and the SSH server's `KEXINIT` (through `FakeConnection`) names a weak
      algorithm BL-221 added; without the option it names none.
- [x] The warning line of ADR-0051 decision 11 is written from the info level up, whenever the
      option is given, after the `--throwaway-hostkey` line; `-s` hides it.
- [x] `surl: (2) --allow-weak-ssh-algorithms is not available in this build` is gone from the
      code, the manual and `--aihelp`; `AiHelpTextTests`, `ManualTextTests` and `HelpTextTests` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Console` and
      `Surl.Cli.UnitLibrary`.

## Notes

- Plan (direct wiring, no new seam): `ComposeProtocolServers` takes `allowWeakSshAlgorithms` and passes it to `SshAlgorithmOffer.Default` (BL-221's parameter); `FindUnavailableOption` keeps only `--hostcert`. `SshHostKeyComposition.WriteStartLines` writes the warning (through `WriteWarnings`, split out to keep complexity at or under 10) after the `--throwaway-hostkey` line and before the verbose host-key notes. It is written whenever the option is given, whether or not an SSH URL is served (decision 11: "the second whenever the option is given").
- Decision (default taken): `--allow-weak-ssh-algorithms` stays in the `security` help category, not `testing`, and is not added to the manual's LOOSENING OPTIONS list. It is a compatibility choice for peers with nothing stronger, like `--tlsv1.0`, not a test-only loosening. The manual's SSH OPTIONS paragraph and the `security` topic now say it writes a warning. ADR-0051 already fixes the warning text and order, so no new ADR.
- Tests: `CommandLineRunnerSshTests.RunAsync_AllowWeakSshAlgorithms_TheServersKexInitNamesTheWeakAlgorithmsOnlyWithIt` checks the KEXINIT sent through `FakeConnection`. It names `diffie-hellman-group14-sha1`, `aes128-cbc`, `3des-cbc`, `arcfour`, `hmac-sha1` and `hmac-md5` only when the option is given. Other tests cover the warning at the info level, its order, `-s`, and a 1024-bit RSA host key served with the option.
- Product-Overview, Requirements, Glossary and README still say the option is refused. BL-214 (in Doing) names them in its `touches`, so BL-265 is filed to update them.
- Measured: `Measure-CodeQuality.ps1` reports 100% line and branch coverage and 0 failing members for Surl.Console and Surl.Cli.UnitLibrary. `dotnet format --verify-no-changes` reports only line-ending markers that were already there, in files this task did not touch.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl --allow-weak-ssh-algorithms offers ADR-0051's weak SSH algorithms and writes decision 11's warning
