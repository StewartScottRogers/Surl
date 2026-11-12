---
id: BL-861
title: Amend ADR-0108 with how BL-421 serves s3 from surl
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-421, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-861 — Amend ADR-0108 with how BL-421 serves s3 from surl

## Goal

ADR-0108 has an amendment that records, as built, how BL-421 composed the S3 server into `surl`, and the S3
library's `CLAUDE.md` says it is served from `surl`.

## Context

BL-421 could not edit `Documentation/Planning/Decisions` because BL-822, then in Doing, names that folder in its
`touches`. Its Notes record what to write. In short, decided by Claude under Stewart's delegation in BL-421:

1. `s3` joins `TlsSchemes`'s implicit-TLS set and `Surl.Networking`'s ALPN offers `http/1.1` alone for it, so the
   engine handshakes before the S3 server sees the connection and the `(58)` refusal without `--cert` or
   `--self-signed` applies unchanged; the server is registered directly, with no `ImplicitTlsSchemeServer`.
2. The one `ObjectMetadataIndex` of the served root is service state: `ServiceState.ObjectMetadataIndex`, built by
   `ComposeObjectMetadataIndex` (persisted in `<path>/.surl/objects/index` with `--directory`, in memory without),
   loaded after the LDAP directory by `LoadObjectMetadataIndexAsync`, an unreadable or malformed index ending the
   start with `(37) Could not read <file>: <reason>`. `ExtendedProtocolServerServices` carries it and the clock.
3. `--s3-plaintext` (decision 5) is left to BL-860, because the engine decides TLS by scheme alone.
4. The `s3` help category ("Amazon S3 protocol (extended)") is listed by `--directory`, `--allow-uploads`,
   `--list-directories`, `--head-timeout`, `--max-request-head`, `--max-filesize`, `-u`, `--user-file` and
   `--allow-anonymous`; the manual has an `S3 OPTIONS` section.

## Acceptance criteria

- [x] ADR-0108 has an "Amendment 2" stating points 1 to 4 above, marked "Decided by Claude under Stewart's
      delegation", naming BL-421 and BL-860.
- [x] `Surl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md` says `surl` serves `s3` through `Surl.Composition`'s
      `ExtendedProtocolServers.Compose`.

## Notes

- 2026-11-11 (lane 6): The docs work is done in commit a89dbe1f (lane 4 branch factory/BL-861-lane-4-20261111-060243; it numbers the amendment "Amendment 4" because amendments 2 and 3 already exist). Lane 4's integration failed only because `dotnet build` cannot resolve the SDK global.json pins (10.0.401 not installed), not because of this change. Lane 1 cherry-picked it and renumbered the amendment to "Amendment 6" because 4 and 5 now exist; docs only, no code touched.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-861-lane-4-20261111-060243; start with git cherry-pick --no-commit factory/BL-861-lane-4-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so dotnet build fails; the docs change is ready in a89dbe1f.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0108 Amendment 6 records how surl serves s3; S3 CLAUDE.md says so
