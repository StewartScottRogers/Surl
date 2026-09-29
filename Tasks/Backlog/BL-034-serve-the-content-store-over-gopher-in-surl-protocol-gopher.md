---
id: BL-034
title: Serve the content store over Gopher in Surl.Protocol.Gopher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-009, BL-029, BL-044]
touches: [Surl.Protocol.Gopher.UnitLibrary, Surl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-034 — Serve the content store over Gopher in Surl.Protocol.Gopher

## Goal

`Surl.Protocol.Gopher.UnitLibrary` contains a Gopher protocol server for the `gopher`
scheme. It answers the selectors the pinned upstream curl 8.21.0 build sends with files
from the content store, and a menu for a directory, proven by byte scripts recorded from
that build.

## Context

- Gopher is RFC 1436. What upstream curl sends for `gopher://host:port/<type><selector>`
  and for a search (`%09` query) is measured with `Record-CurlExchange.ps1 -Raw` (BL-029)
  against the pinned build, never assumed.
- `gophers` is the same server over TLS (ADR-0002, "Consequences"). Its wiring comes
  after TLS lands, so this task declares only `gopher`, and the `/feature` plan notes
  the follow-up for `gophers`.
- Files come from the content store (BL-008, BL-009), and directory entries from its
  listing (BL-044). Add a `ProjectReference` to
  `Surl.Content.UnitLibrary`, which ADR-0002 allows. A refused or missing selector gets
  the error the plan decides (RFC 1436 item type `3`), stated in the XML doc.
- Directory menus: this task generates an RFC 1436 menu for a directory in the content
  store, from BL-044's listing. The plan decides the item type for each entry and the
  host and port fields (the listen URL's bound address from the exchange context), and
  states them in the XML doc.
- The server implements the protocol-server interface from the listener-seam ADR
  (BL-000, BL-005), and is tested through BL-005's in-memory connection.
- Fixtures: as in BL-017, commit recordings under
  `Surl.Protocol.Gopher.UnitTests/Fixtures/<case>/` as `EmbeddedResource`, with a
  `README.md` giving command lines and build SHA-256.

## Acceptance criteria

- [ ] Recordings exist for a file selector, the root menu, and a missing selector, each
      fed Surl's intended response, and each shows the pinned build's exit code and
      stdout for that response.
- [ ] Fast tests replay each recording through the in-memory connection against an
      in-memory content store, and assert Surl's reply bytes equal the accepted ones.
- [ ] A selector containing `..` or its percent-encoded forms is refused through the
      content store, pinned by a test.
- [ ] `ProtocolIsolationTests` pass with the `Surl.Content.UnitLibrary` reference.
- [ ] `dotnet build Surl.Protocol.Gopher.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `Integration` test in `Surl.Protocol.Gopher.UnitTests`,
      and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Gopher.UnitLibrary`.

## Notes

Wiring `gopher` into `surl` and the live conformance run are BL-040.

## Log

- 2026-09-28: Created.
