---
id: BL-262
title: Let surl narrow its SSH cipher and MAC offer with --ssh-ciphers and --ssh-macs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-250]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-262 — Let surl narrow its SSH cipher and MAC offer with --ssh-ciphers and --ssh-macs

## Goal

`surl --allow-weak-ssh-algorithms --ssh-ciphers blowfish-cbc --ssh-macs hmac-ripemd160 sftp://...`
offers only the named cipher and MAC in its `SSH_MSG_KEXINIT`, so a test can make one algorithm
the only overlap with a client (sshd's `Ciphers` and `MACs`, as a surl option).

## Context

- Upstream curl has no option to choose its SSH cipher or MAC: libssh2 picks the first name in
  its own list that the server offers, so surl, the server, has to narrow its offer for a named
  weak algorithm to be agreed (BL-261 needs this to prove `blowfish-cbc`, `cast128-cbc`,
  `hmac-ripemd160` and `hmac-ripemd160@openssh.com` against pinned upstream curl).
- The offer is built by `SshAlgorithmOffer.Default` (`Surl.Protocol.Ssh.UnitLibrary`) and composed
  in `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers`. Rewriting a KEXINIT on the wire
  is not an option: both KEXINITs are hashed into the exchange hash.
- Decide the option names, list syntax (comma-separated, sshd-style), and what an unknown name or a
  weak name without `--allow-weak-ssh-algorithms` does (likely exit 2 with a `surl: (2)` line);
  record it in an ADR "Decided by Claude under Stewart's delegation".
- Every new option needs its `--aihelp` facts, topics and manual entry (root CLAUDE.md).

## Acceptance criteria

- [x] An ADR records the options, their syntax and their refusals.
- [x] A fast `Surl.Console.UnitTests` test shows `--ssh-ciphers blowfish-cbc --ssh-macs hmac-ripemd160`
      (with `--allow-weak-ssh-algorithms`) makes the SSH server's KEXINIT list exactly those names
      in the cipher and MAC lists; without the options the default offer is unchanged.
- [x] An unknown name, or a weak name without `--allow-weak-ssh-algorithms`, is refused with the
      ADR's exit code and message, each pinned in a test.
- [x] `AiHelpFactsTests`, `AiHelpTextTests`, `ManualTextTests` and `HelpTextTests` pass.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan and decisions: ADR-0066 (Decided by Claude under Stewart's delegation). Comma-separated,
  exact-case names, sshd's `Ciphers`/`MACs` without `+`/`-`/`^`; offered in the order given, a
  repeat once, the last option wins; exit 2 for `surl: (2) --ssh-ciphers: surl does not offer the
  SSH cipher <name>` and `surl: (2) --ssh-ciphers: <name> needs --allow-weak-ssh-algorithms`
  (`--ssh-macs` likewise), checked before any file is read.
- Where: `SshAlgorithmOffer.Narrowed` (Ssh), `SshAlgorithmComposition` (Console: `FindRefusal`,
  `Compose`), `OptionArgumentReader.SshAlgorithmNames` and `SurlCommandLine.SshCiphers`/`SshMacs`
  (Cli). Cli does not know the names, so they live only in `SshAlgorithmOffer`; a Console test
  (`AiHelpSshTopic_NamesEveryCipherAndMacSurlCanOffer`) fails when `--aihelp ssh` misses one.
- Default taken: `aes*-gcm@openssh.com` counts as offered only where `AesGcm.IsSupported`, so
  "unknown" means "not offered on this machine"; no separate message for it (no platform-only branch).
- Tests: `CommandLineRunnerSshTests.RunAsync_SshCiphersAndSshMacs_*` checks the KEXINIT holds the
  length-prefixed lists `blowfish-cbc` and `hmac-ripemd160` alone in both directions;
  `RunAsync_SshAlgorithmNameSurlCannotOffer_*` pins five refusals. Fast tests: Cli 707, Console 367,
  Ssh 1019, all green.
- Not touched: `dotnet format` flags CRLF in `Surl.Console/ImplicitTlsSchemeServer.cs`,
  `Surl.Output.UnitLibrary/SilentExchangeLog.cs` and `Surl.Output.UnitTests/VerboseLogEscapingTests.cs`,
  which were already LF before this task.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl --ssh-ciphers and --ssh-macs narrow the SSH KEXINIT's cipher and MAC lists (ADR-0066)
