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
completed:
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

- [ ] A fast `Surl.Console.UnitTests` test shows `--allow-weak-ssh-algorithms` with an `sftp://`
      URL starts a listener, and the SSH server's `KEXINIT` (through `FakeConnection`) names a weak
      algorithm BL-221 added; without the option it names none.
- [ ] The warning line of ADR-0051 decision 11 is written from the info level up, whenever the
      option is given, after the `--throwaway-hostkey` line; `-s` hides it.
- [ ] `surl: (2) --allow-weak-ssh-algorithms is not available in this build` is gone from the
      code, the manual and `--aihelp`; `AiHelpTextTests`, `ManualTextTests` and `HelpTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Console` and
      `Surl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
