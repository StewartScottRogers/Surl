---
id: BL-1947
title: Correct the Surl.Composition ownership claims and ADR links in the IMAP library
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1928]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1947 — Correct the Surl.Composition ownership claims and ADR links in the IMAP library

## Goal

The IMAP library's comments and `CLAUDE.md` name `Surl.Composition` as the project that composes the server, and the
ADR links in that `CLAUDE.md` resolve.

## Context

Found while working BL-1929 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. BL-1928 corrects the
`isTlsUpgradeAvailable` sentences; this task leaves them as that task writes them.

What the code does (`Surl.Composition`): `ProtocolServerRegistrations.Original` registers `ImapProtocolServer` for
`imap`, passing `services.AuthenticationPolicyFor("imap")` as both policy arguments (one `AuthenticationPolicy` per
scheme, cached by `SchemeAuthenticationPolicies.For`), and registers `imaps` with
`ProtocolServerRegistration.ImplicitTls("imaps", "imap")`, an `ImplicitTlsSchemeServer` (a `Surl.Composition` type) over
that same server. The library does not reference `Surl.Composition`, so name its types in `<c>`, not `<see cref>`.

In `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary`:

- `CLAUDE.md`, about lines 17-18: `imaps` goes "through `Surl.Console`'s `ImplicitTlsSchemeServer`".
- `CLAUDE.md`, about lines 25-26: "`Surl.Console` passes one `AuthenticationPolicy` as both".
- `ImapProtocolServer.cs`, remarks, about lines 40-41: `imaps` is implicit TLS "which `Surl.Console` wraps round this
  server".
- `ImapProtocolServer.cs`, the `mailAuthenticationPolicy` param, about line 80: "`Surl.Console` passes the same object".
- `CLAUDE.md`, about lines 5, 8, 10 and 12: the ADR-0055, ADR-0050, ADR-0049 and ADR-0059 links start with
  `../Documentation/`, which resolves under `Protocols/Mail/`. From this folder the path is `../../../Documentation/`.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] `CLAUDE.md` and the `ImapProtocolServer` remarks say `Surl.Composition` registers `imaps` as an `ImplicitTlsSchemeServer` over the `imap` server.
- [x] `CLAUDE.md` and the `mailAuthenticationPolicy` param say `Surl.Composition` passes the `imap` scheme's one `AuthenticationPolicy` as both policy arguments.
- [x] No comment or `CLAUDE.md` line in the library says `Surl.Console` registers, wraps or composes the server.
- [x] Every relative link in the library's `CLAUDE.md` resolves to a file in the repository.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- BL-1943 (commit `68bd81d49`) had already made the `Surl.Composition` wording fixes in `CLAUDE.md` and
  `ImapProtocolServer.cs`. Checked against `ProtocolServerRegistrations.Original`; no `Surl.Console` mention remains
  in the library.
- This task changed only the four ADR links (ADR-0055, 0050, 0049, 0059) to `../../../Documentation/`; all four
  resolve. No `.cs` file changed.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, 0 errors; fast tests 30368 passed, 0 failed, 3 skipped.
  `dotnet format` not run: no `.cs` or project file changed.
- Filed BL-1952 for two other statements in the same `CLAUDE.md` (`SPECIAL-USE`, log redaction), outside this task.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The IMAP library's four ADR links resolve and it names Surl.Composition as the composer (wording from BL-1943).
