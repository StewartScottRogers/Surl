---
id: BL-1867
title: Bring the Surl.Console notes plus the readStartFile doc comment up to date with the start composition
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Console/CLAUDE.md, Application/Surl.Console/CommandLineRunner.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1867 — Bring the Surl.Console notes plus the readStartFile doc comment up to date with the start composition

## Goal

`Application/Surl.Console/CLAUDE.md` and the `readStartFile` XML doc comment in `CommandLineRunner.cs` describe the extended servers, the start-up warnings, server TLS and the start files as the code composes them.

## Context

Found while working BL-1828 and checked against the code on 2026-11-27. Doc and comment text only; no behaviour change. Line numbers are as of that date.

- `Application/Surl.Console/CLAUDE.md`, "Extended protocol servers" bullet: says of the servers `ExtendedProtocolServers.Compose` builds "none yet". `Surl.Composition.UnitLibrary.csproj` references 19 `Surl.Extended.Protocol.*` libraries; `ExtendedProtocolServers.Registrations` lists their entries. The same sentence gives `ExtendedProtocolServerServices` as "(content store, policy, TLS upgrade)"; the record wraps the whole `ComposedProtocolServices`. `Application/Surl.Composition.UnitLibrary/CLAUDE.md` already describes the servers, so point there rather than repeating the list.
- Same file, the "startup warnings go to the log stream" bullet: goes from the `--allow-anonymous`, `--allow-plaintext-auth` and `--auth` lines straight to the keytab, `--self-signed` and SSH lines. `ComposedServing.WriteStartUpLines` writes more: `AuthenticationComposition.WriteLooseningWarnings` also writes the `--user-ldap` plaintext, `--proxy-auth` and `--s3-plaintext`/`--oci-plaintext`/`--az-plaintext`/`--gs-plaintext` lines; then the `--proxy-allow-origin`, `--fault`, `--control`, share `@everyone`, topic `@everyone`, `--mail-catch-all` and readable-by-others warnings come before the keytab lines; the TLS-SRP group notes follow the `--self-signed` lines; the LDAP account source, group, mailbox, share and message bus notes follow the SSH lines. Take the order from the method itself.
- Same file, the `ServerTlsComposition` bullet: its two lists of upgradable schemes (`smtp`, `imap`, `pop3`, `ftp`, `ldap`) omit schemes that `ServerTlsComposition.UpgradableSchemes` holds (`amqp1`, `xmpp`), and `NeedsTls` also covers `UpgradeRequiredSchemes`, `coaps` with a certificate and upgrades under TLS-SRP.
- `Application/Surl.Console/CommandLineRunner.cs`, `<param name="readStartFile">` (about lines 45-51): lists `--user-file`, `--authorized-keys`, `--keytab` and `--hostkey`. The same seam (`SurlServerCompositionSeams.ReadStartFile`) also reads `--mailbox-file`, `--proxy-user-file`, `--hostcert`, `--control-token-file`, the `bind-password-file` and `ca-file` of `--user-ldap`, `--cert`, `--key`, `--cacert`, `--srp-groups` and `--srp-verifiers`.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The extended-servers bullet no longer says "none yet", and what it says `ExtendedProtocolServers.Compose` builds and is given agrees with `ExtendedProtocolServers.cs` and `ExtendedProtocolServerServices.cs`.
- [ ] The start-up warnings bullet names every writer `ComposedServing.WriteStartUpLines` calls, in the order it calls them, the `--fault` warning included.
- [ ] Every list of schemes in the `ServerTlsComposition` bullet agrees with `ServerTlsComposition.NeedsTls`, `UpgradableSchemes` and `UpgradeRequiredSchemes`.
- [ ] The `readStartFile` doc comment names every file listed in Context, or states that it reads every file the composition reads at start through `ReadStartFile` without giving a partial list.
- [ ] `dotnet build Application/Surl.Console -warnaserror` is clean and the diff of `CommandLineRunner.cs` changes comment lines only.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
