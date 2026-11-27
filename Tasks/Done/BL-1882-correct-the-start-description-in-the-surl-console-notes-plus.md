---
id: BL-1882
title: Correct the start description in the Surl.Console notes plus the TLS settings parameter comment
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1867, BL-1880]
touches: [Application/Surl.Console/CLAUDE.md, Application/Surl.Console/CommandLineRunner.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1882 — Correct the start description in the Surl.Console notes plus the TLS settings parameter comment

## Goal

The `CommandLineRunner` bullet in `Application/Surl.Console/CLAUDE.md` and the `createListenerFactory` doc comment describe the start as `Surl.Composition` runs it.

## Context

Found while working BL-1867 (2026-11-27), read from source. BL-1867 edits both files (other bullets and the `readStartFile` comment), so this task follows it; BL-1880 edits the `ServerTlsComposition` bullet. Documentation and comment text only; no behaviour change. Line numbers are as of that date.

- `Application/Surl.Console/CommandLineRunner.cs`, `<param name="createListenerFactory">` (about lines 19-23): says the TLS settings are `null` when no listen URL is TLS from the first byte. They are `null` only when no listen URL needs TLS (`ServerTlsComposition.NeedsTls`).
- `Application/Surl.Console/CLAUDE.md`, the long bullet starting "`CommandLineRunner` parses the command line" (about lines 31-132):
  - its description of the files read at start leaves out each `--mailbox-file` (`MailboxComposition.Compose`, read first), the `--user-ldap` `bind-password-file` and `ca-file` (`LdapAccountSourceComposition`), `--proxy-user-file` (`AuthenticationComposition.ComposeProxyPolicy`) and `--control-token-file` (`ControlComposition.ReadToken`);
  - it places the content store before the listen-URL refusals and authentication. `SurlServerComposition.ComposeAvailableAsync` (in `SurlServerComposition.Start.cs`) builds only the content file system there, and `ComposeContentStore` after authentication, the shares and the message bus configuration;
  - its list of servers built by `ComposeProtocolServers` leaves out `SocksProtocolServer` (`socks4`, `socks4a`, `socks5`, `socks5h`; `ProtocolServerRegistrations.Original`).
- The start step table in the same file (the "Option refusals, data directory probe, content store, ..." row) has the same order.

Take every order from the code. Where `Application/Surl.Composition.UnitLibrary/CLAUDE.md` already says something, a pointer is better than a second copy.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The `createListenerFactory` doc comment says the settings are `null` only when no listen URL needs TLS.
- [x] The bullet names every file `ComposeAuthentication` reads, in the order it reads them, or points to where that is stated without giving a partial list.
- [x] The bullet and the start step table place the content file system before the listen-URL refusals and the content store after authentication, as `ComposeAvailableAsync` does.
- [x] The bullet's server list agrees with `ProtocolServerRegistrations.Original`, `SocksProtocolServer` included, or points to that list without a partial one.
- [x] `dotnet build Application/Surl.Console -warnaserror` is clean and the diff of `CommandLineRunner.cs` changes comment lines only.

## Notes

- Made in the session rather than delegated to `align-and-document`: two files, text already traced to the code.
- The bullet names all ten start files in `ComposeAuthentication`'s order and lists `SocksProtocolServer`.
- Also corrected in the same bullet, because each was false against the code: the `--dns-transport` option
  refusal was missing; the listen-URL certificate refusal covers more than TLS-first URLs; the lock is also
  taken on each writable `--share`; the TLS upgrade is offered under TLS-SRP too
  (`ServerTlsComposition.IsTlsUpgradeAvailable`). The `takeDataDirectoryLock` doc comment in
  `CommandLineRunner.cs` had the same `--share` omission.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0,
  fast tests exit 0 with no failures. No integration-level code changed.
- Follow-up: BL-1917 (the same two points in `Surl.Composition` comments). BL-1881 already covers its
  `CreateListenerFactory` comment and the start's class summary.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.Console notes and CommandLineRunner comments describe the start order, start files, server list and TLS settings as Surl.Composition runs them
