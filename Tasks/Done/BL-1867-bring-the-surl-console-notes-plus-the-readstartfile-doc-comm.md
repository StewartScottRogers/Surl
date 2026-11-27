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
completed: 2026-11-27
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

- [x] The extended-servers bullet no longer says "none yet", and what it says `ExtendedProtocolServers.Compose` builds and is given agrees with `ExtendedProtocolServers.cs` and `ExtendedProtocolServerServices.cs`.
- [x] The start-up warnings bullet names every writer `ComposedServing.WriteStartUpLines` calls, in the order it calls them, the `--fault` warning included.
- [x] Every list of schemes in the `ServerTlsComposition` bullet agrees with `ServerTlsComposition.NeedsTls`, `UpgradableSchemes` and `UpgradeRequiredSchemes`.
- [x] The `readStartFile` doc comment names every file listed in Context, or states that it reads every file the composition reads at start through `ReadStartFile` without giving a partial list.
- [x] `dotnet build Application/Surl.Console -warnaserror` is clean and the diff of `CommandLineRunner.cs` changes comment lines only.

## Notes

- `readStartFile` comment: took the criterion's second form (every file read through the seam, no list), so a new start file does not make it stale.
- The start-up warnings bullet numbers the 17 calls of `ComposedServing.WriteStartUpLines`; the extended-servers bullet points to the composition `CLAUDE.md` and repeats no server list.
- Verify 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests green. No integration tests: comment and document text only.
- Drift outside `touches` found on the way is filed as follow-up tasks, not fixed here.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Console notes and the readStartFile doc comment now describe the extended servers, start-up warnings, server TLS and start files as composed; follow-ups BL-1873 to BL-1876
