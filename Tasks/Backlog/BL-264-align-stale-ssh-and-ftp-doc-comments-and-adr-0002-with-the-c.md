---
id: BL-264
title: Align stale SSH and FTP doc comments and ADR-0002 with the code as built
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests, Surl.Console, Surl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md]
requirement: none
created: 2026-09-30
completed:
---
# BL-264 — Align stale SSH and FTP doc comments and ADR-0002 with the code as built

## Goal

The XML doc comments of the SSH server, the FTP server's constructor and `surl`'s
unavailable-option check, and ADR-0002's project map, say what the code does today.

## Context

Found by BL-213's alignment pass (2026-09-30); outside BL-213's `touches`, so filed here.

- `Surl.Protocol.Ssh.UnitLibrary/SshProtocolServer.cs` (summary, around lines 8 and 18-19)
  says it "so far" runs only the transport and user authentication, and that SCP and SFTP
  writes get `CHANNEL_FAILURE` "until BL-164". SCP and all of SFTP are served now.
- `Surl.Protocol.Ssh.UnitLibrary/SshCipherAndMacProtection.cs`: the summary lists the weak
  ciphers as AES-CBC, 3DES-CBC and RC4, leaving out `blowfish-cbc` and `cast128-cbc` (BL-258).
- `Surl.Console/CommandLineRunner.cs` (`FindUnavailableOption`, around lines 497-500): says
  `--allow-weak-ssh-algorithms` is refused until BL-221; BL-221 is Done and the refusal now
  waits on BL-250.
- One concept, two names: `FtpProtocolServer`'s constructor parameter `isAuthTlsAvailable`
  versus `isTlsUpgradeAvailable` in `CommandLineRunner.ComposeProtocolServers` and the mail
  servers. Rename the FTP parameter to `isTlsUpgradeAvailable` (Glossary term) and its uses.
- `Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md` line 10
  says the ADR-0061 projects "exist once BL-254 creates them"; they exist.

## Acceptance criteria

- [ ] None of the five statements above remains; each comment names what the code does now.
- [ ] `FtpProtocolServer`'s constructor parameter is named `isTlsUpgradeAvailable`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-30: Created.
