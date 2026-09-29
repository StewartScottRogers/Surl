---
id: BL-092
title: Never serve, list or accept uploads into the .surl folder of the served root
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-090]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: FR-024
created: 2026-09-29
completed:
---
# BL-092 — Never serve, list or accept uploads into the .surl folder of the served root

## Goal

`ContentStore` answers every request for the `.surl` folder at the top of the served root,
and anything under it, exactly as a missing entry, leaves it out of every listing and
refuses uploads into it, whatever the exposure options say.

## Context

FR-024; ADR-0031 (BL-090) decision 5 says how `.surl` is recognised (first segment,
ordinal or ignoring case) and how each operation answers. Service state (MQTT retained
messages, the lock file) lives there, so serving it would leak state and let a peer
overwrite it.

- `Surl.Content.UnitLibrary/ContentStore.cs`: dot-files are hidden unless
  `ContentExposureOptions.ServeDotFiles` (`IsDotFileName`, around lines 450-461); the
  `.surl` rule is separate and applies even with `ServeDotFiles` on.
- `--follow-symlinks` (`FollowSymbolicLinks`) follows a link whose final target stays
  inside the served root; a link whose final target lies inside `<root>/.surl` must be
  refused too (check the resolved path, not only the request path).
- Every protocol that serves files goes through `ContentStore` (HTTP, Gopher, DICT, TFTP),
  so one change here covers them all; no protocol project changes.
- Tests use `UnitTestInMemoryContentFileSystem`, no disk.

## Acceptance criteria

- [ ] With `ServeDotFiles` on and every other exposure option on, tests in
      `ContentStoreTests` (a new partial file `ContentStoreTests.ServiceStateFolder.cs`)
      prove: reading `/.surl`, `/.surl/`, `/.surl/lock` and `/.surl/mqtt/x` is answered as
      a missing entry; a listing of `/` omits `.surl`; an upload to `/.surl/x` and to
      `/.surl` is refused as not permitted; a symbolic link `/link` whose target is
      `<root>/.surl/lock` is answered as missing with `FollowSymbolicLinks` on.
- [ ] The same requests with a differently cased name (`/.SURL/lock`) are answered as
      ADR-0031 decision 5 says, each pinned by a test.
- [ ] A dot-file that is not `.surl` (`/.hidden`), and a `.surl` folder below the top
      (`/sub/.surl/x`) if ADR-0031 says only the top one is reserved, are still served
      with `ServeDotFiles` on, as today.
- [ ] `ContentStore`'s XML doc comment states the rule and cites ADR-0031.
- [ ] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests pass,
      `Surl.Content.UnitLibrary` keeps 100% line and branch coverage, and no new test needs
      `TestCategory=Integration`.

## Notes

## Log

- 2026-09-29: Created.
