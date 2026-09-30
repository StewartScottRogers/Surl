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
completed:
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

- [ ] An ADR records the options, their syntax and their refusals.
- [ ] A fast `Surl.Console.UnitTests` test shows `--ssh-ciphers blowfish-cbc --ssh-macs hmac-ripemd160`
      (with `--allow-weak-ssh-algorithms`) makes the SSH server's KEXINIT list exactly those names
      in the cipher and MAC lists; without the options the default offer is unchanged.
- [ ] An unknown name, or a weak name without `--allow-weak-ssh-algorithms`, is refused with the
      ADR's exit code and message, each pinned in a test.
- [ ] `AiHelpFactsTests`, `AiHelpTextTests`, `ManualTextTests` and `HelpTextTests` pass.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
