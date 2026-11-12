---
id: BL-1052
title: Write the TLS-SRP verbose notes: a group outside RFC 5054 and which login failed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1052 — Write the TLS-SRP verbose notes: a group outside RFC 5054 and which login failed

## Goal

With `-v`, surl notes once at start each TLS-SRP group from `--srp-groups` that is not one of RFC 5054 Appendix A's, and after each failed TLS-SRP handshake whether the user was unknown or the password wrong, as ADR-0122 decision 4 asks.

## Context

- ADR-0122 decision 4 ("noted once in the verbose log"; "The verbose log says which it was; the
  wire never does") and Amendment 2 ("Not yet").
- `SrpGroup.IsRfc5054Group` (Surl.Cryptography.Srp) says whether a group is Appendix A's.
  `TlsSrpAccountsComposition` (Composition) reads the files; the start writes its `-v` notes through
  the `WriteStartLines` pattern the other compositions use.
- A failed handshake surfaces in `ServerTlsHandshake` (Networking) as `TlsHandshakeException`;
  `TlsSrpAccounts.Find` returns `IsSimulated` for an unknown user, so the handshake can carry that
  into the exception message the engine already logs.

## Acceptance criteria

- [ ] `Surl.Composition.UnitTests`: a groups file with a group outside Appendix A writes one `-v` note naming its index, and an Appendix A group writes none.
- [ ] `Surl.Networking.UnitTests`: replaying pinned upstream curl's `CurlTlsSrpBaduserBadpass` and `CurlTlsSrpJsmithBadpass` recordings gives handshake failures whose messages say "unknown user" and "wrong password" respectively, while the bytes sent stay identical.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and each touched library keeps 100% line and branch coverage.

## Notes

- Filed by BL-567 on 2026-11-11.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
