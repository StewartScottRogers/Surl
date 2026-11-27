---
id: BL-1946
title: Correct the Surl.Composition ownership claims and ADR links in the POP3 library
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1929]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1946 — Correct the Surl.Composition ownership claims and ADR links in the POP3 library

## Goal

The POP3 library's comments and `CLAUDE.md` name `Surl.Composition` as the project that composes the server, and the
ADR links in that `CLAUDE.md` resolve.

## Context

Found while working BL-1929 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. BL-1929 corrects the
`isTlsUpgradeAvailable` sentences; this task leaves them as that task writes them.

What the code does (`Surl.Composition`): `ProtocolServerRegistrations.Original` registers `Pop3ProtocolServer` for
`pop3`, passing `services.AuthenticationPolicyFor("pop3")` as both policy arguments (one `AuthenticationPolicy` per
scheme, cached by `SchemeAuthenticationPolicies.For`), and registers `pop3s` with
`ProtocolServerRegistration.ImplicitTls("pop3s", "pop3")`, an `ImplicitTlsSchemeServer` (a `Surl.Composition` type) over
that same server. The library does not reference `Surl.Composition`, so name its types in `<c>`, not `<see cref>`.

In `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary`:

- `CLAUDE.md`, about lines 14-15: `pop3s` goes "through `Surl.Console`'s `ImplicitTlsSchemeServer`".
- `CLAUDE.md`, about lines 22-23: "`Surl.Console` passes one `AuthenticationPolicy` as both".
- `Pop3ProtocolServer.cs`, remarks, about line 30: "`Surl.Console` registers it for `pop3s`".
- `Pop3ProtocolServer.cs`, the `mailAuthenticationPolicy` param, about line 85: "`Surl.Console` passes the same object".
- `CLAUDE.md`, about lines 5, 7 and 9: the ADR-0056, ADR-0050 and ADR-0049 links start with `../Documentation/`, which
  resolves under `Protocols/Mail/`. From this folder the path is `../../../Documentation/`.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] `CLAUDE.md` and the `Pop3ProtocolServer` remarks say `Surl.Composition` registers `pop3s` as an `ImplicitTlsSchemeServer` over the `pop3` server.
- [ ] `CLAUDE.md` and the `mailAuthenticationPolicy` param say `Surl.Composition` passes the `pop3` scheme's one `AuthenticationPolicy` as both policy arguments.
- [ ] No comment or `CLAUDE.md` line in the library says `Surl.Console` registers or composes the server.
- [ ] Every relative link in the library's `CLAUDE.md` resolves to a file in the repository.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
