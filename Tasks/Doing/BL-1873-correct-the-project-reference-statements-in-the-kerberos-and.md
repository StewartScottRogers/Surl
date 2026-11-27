---
id: BL-1873
title: Correct the project reference statements in the Kerberos and Tls13 library CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Cryptography/Surl.Kerberos.UnitLibrary, Cryptography/Surl.Tls13.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1873 — Correct the project reference statements in the Kerberos and Tls13 library CLAUDE.md files

## Goal

The `CLAUDE.md` files of `Surl.Kerberos.UnitLibrary` and `Surl.Tls13.UnitLibrary` state project references as the
project files have them.

## Context

Found by BL-1836 and checked against the project files on 2026-11-27. Prose only. No store, event or fault changes.

- `Cryptography/Surl.Kerberos.UnitLibrary/CLAUDE.md`, first paragraph, says `Surl.Authentication.UnitLibrary`
  "will reference" the library "(BL-240)". `Surl.Authentication.UnitLibrary.csproj` already references it. So do
  the project files of `Surl.Kerberos.TestKdc.UnitLibrary` (which the paragraph mentions),
  `Surl.Protocol.Socks.UnitLibrary`, `Surl.Composition.UnitLibrary` and `Surl.Console` (and
  `Surl.Kerberos.UnitTests`). The later sentence "It references nothing but the shared framework" is true.
- `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md`, the paragraph starting "It references", names Curve25519,
  Curve448, MlKem and Sm2. `Surl.Tls13.UnitLibrary.csproj` also references
  `Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`, for the record layer
  (ADR-0157 decision 2), as the same file's `TLS_CHACHA20_POLY1305_SHA256` bullet already says. ADR-0002
  decision 3's table (corrected by BL-1836) lists all six.

## Acceptance criteria

- [ ] The Kerberos `CLAUDE.md` says in the present tense that `Surl.Authentication.UnitLibrary` references the
      library, and no "will reference" remains in the file.
- [ ] Any list in that file that claims to give every project referencing the library matches the `ProjectReference`
      entries of the production projects.
- [ ] The Tls13 `CLAUDE.md`'s "It references" paragraph names exactly the six projects of the
      `ProjectReference` entries in `Surl.Tls13.UnitLibrary.csproj` and cites ADR-0157 beside ADR-0082 and ADR-0147.
- [ ] The only files changed outside `Tasks/` are those two `CLAUDE.md` files.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
