---
id: BL-1948
title: Correct the Surl.Composition ownership claims and ADR links in the SMTP library
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1938]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1948 — Correct the Surl.Composition ownership claims and ADR links in the SMTP library

## Goal

The SMTP library's comments and `CLAUDE.md` name `Surl.Composition` as the project that composes the server, and the
ADR links in that `CLAUDE.md` resolve.

## Context

Found while working BL-1929 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. BL-1938 corrects the
`isTlsUpgradeAvailable` sentences; this task leaves them as that task writes them.

What the code does (`Surl.Composition`): `ProtocolServerRegistrations.Original` registers `SmtpProtocolServer` for
`smtp`, passing `services.AuthenticationPolicyFor("smtp")` as both policy arguments (one `AuthenticationPolicy` per
scheme, cached by `SchemeAuthenticationPolicies.For`), and registers `smtps` with
`ProtocolServerRegistration.ImplicitTls("smtps", "smtp")`, an `ImplicitTlsSchemeServer` (a `Surl.Composition` type) over
that same server. The library does not reference `Surl.Composition`, so name its types in `<c>`, not `<see cref>`.

In `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary`:

- `CLAUDE.md`, about lines 15-16: `smtps` goes "through `Surl.Console`'s `ImplicitTlsSchemeServer`".
- `CLAUDE.md`, about lines 23-24: "`Surl.Console` passes one `AuthenticationPolicy` as both".
- `SmtpProtocolServer.cs`, the `mailAuthenticationPolicy` param, about line 88: "`Surl.Console` passes the same object".
- `CLAUDE.md`, about lines 4, 6, 8 and 10: the ADR-0053, ADR-0050, ADR-0049 and ADR-0059 links start with
  `../Documentation/`, which resolves under `Protocols/Mail/`. From this folder the path is `../../../Documentation/`.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] `CLAUDE.md` says `Surl.Composition` registers `smtps` as an `ImplicitTlsSchemeServer` over the `smtp` server.
- [ ] `CLAUDE.md` and the `mailAuthenticationPolicy` param say `Surl.Composition` passes the `smtp` scheme's one `AuthenticationPolicy` as both policy arguments.
- [ ] No comment or `CLAUDE.md` line in the library says `Surl.Console` registers or composes the server.
- [ ] Every relative link in the library's `CLAUDE.md` resolves to a file in the repository.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
