---
id: BL-1873
title: Count amqp1 as a listen URL that TLS-SRP can secure
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1868]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1873 — Count amqp1 as a listen URL that TLS-SRP can secure

## Goal

The TLS-SRP options are accepted when the only listen URL is `amqp1://`, whose server can upgrade to TLS under TLS-SRP.

## Context

Found while working BL-1867 (2026-11-27), read from source; nothing was run.

- `Application/Surl.Cli.UnitLibrary/TlsSrpCombination.cs`, `UpgradableSchemes`, holds `ftp`, `imap`, `ldap`, `natss`, `pop3`, `smtp` and `xmpp`. `amqp1` is not in it and is not in `TlsSchemes` (`amqp1s` is), so `--tlsuser u --tlspassword p amqp1://127.0.0.1:0/` appears to be refused `option --tlsuser: needs a listen URL that uses TLS`.
- `Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs`, `UpgradableSchemes`, holds `amqp1` (ADR-0107 decision 1), and `ExtendedProtocolServers` gives the AMQP 1.0 container `CanUpgradeToTls = services.IsTlsUpgradeAvailable`, which TLS-SRP makes true (ADR-0122 decision 5). The CLI list is the composition's `UpgradableSchemes` plus `natss` (which the composition keeps in `UpgradeRequiredSchemes`), less `amqp1`.
- The same list is written in `TlsSrpCombination`'s doc comment, in `Application/Surl.Cli.UnitLibrary/CLAUDE.md` (the TLS-SRP bullet) and in ADR-0122 Amendment 2 ("A listen URL that uses TLS").
- Tests live in `Application/Surl.Cli.UnitTests/CommandLineParserTlsSrpTests.cs`. `amqp1` is an extended scheme, so the parser must be given it (the overload taking `extendedSchemes`).

Stand-in: a test runs an AMQP 1.0 client over a TLS-SRP upgrade against Surl alone. No store, event or fault changes: this is a command-line refusal only.

## Acceptance criteria

- [ ] A new test in `CommandLineParserTlsSrpTests` parses `--tlsuser u --tlspassword p amqp1://127.0.0.1:0/` and expects `Serve`; it is written first and seen to fail before `TlsSrpCombination` changes (say so under Notes).
- [ ] A data-driven test accepts each of `amqp1`, `ftp`, `imap`, `ldap`, `natss`, `pop3`, `smtp` and `xmpp` as the only listen URL under TLS-SRP, and the existing refusal of `http` and `dict` listen URLs (`FailedInit`, `needs a listen URL that uses TLS`) still passes.
- [ ] The `TlsSrpCombination.UpgradableSchemes` doc comment says how the list relates to `ServerTlsComposition`'s `UpgradableSchemes` and `UpgradeRequiredSchemes` as the code has them.
- [ ] The TLS-SRP bullet in `Application/Surl.Cli.UnitLibrary/CLAUDE.md` lists `amqp1`, and ADR-0122 gains a dated amendment, marked "Decided by Claude under Stewart's delegation", that adds `amqp1` to Amendment 2's list.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the fast tests of `Application/Surl.Cli.UnitTests` pass.

## Notes

## Log

- 2026-11-27: Created.
