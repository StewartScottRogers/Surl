---
id: BL-034
title: Serve the content store over Gopher in Surl.Protocol.Gopher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-009, BL-029, BL-044]
touches: [Surl.Protocol.Gopher.UnitLibrary, Surl.Protocol.Gopher.UnitTests, Documentation/Planning/Decisions/ADR-0012-how-the-gopher-server-answers-a-selector.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
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

- [x] Recordings exist for a file selector, the root menu, and a missing selector, each
      fed Surl's intended response, and each shows the pinned build's exit code and
      stdout for that response.
- [x] Fast tests replay each recording through the in-memory connection against an
      in-memory content store, and assert Surl's reply bytes equal the accepted ones.
- [x] A selector containing `..` or its percent-encoded forms is refused through the
      content store, pinned by a test.
- [x] `ProtocolIsolationTests` pass with the `Surl.Content.UnitLibrary` reference.
- [x] `dotnet build Surl.Protocol.Gopher.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `Integration` test in `Surl.Protocol.Gopher.UnitTests`,
      and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Gopher.UnitLibrary`.

## Notes

Wiring `gopher` into `surl` and the live conformance run are BL-040.

Plan and outcome (2026-09-28, dark factory lane 1):
- Measured first with `Record-CurlExchange.ps1 -Raw` against the pinned build: curl sends
  the URL path without its item-type character, URL-decoded, then CRLF; the root is the
  empty selector; `%09` becomes TAB plus search words; without `--path-as-is` it removes
  dot segments (percent-encoded ones too), so a `..` reaches Surl only through
  `--path-as-is` or `%252e%252e`. curl writes every reply byte to stdout until the close.
- Decisions recorded in ADR-0012 (Decided by Claude under Stewart's delegation): selector
  read as a percent-encoded path and mapped by `ContentStore`; text after a TAB ignored
  (Gopher+ `+` still works); files sent byte for byte with no `.` line; menus with item
  types by extension (`1`, `0`, `g`, `I`, `h`, else `9`), host = listen URL host or, for a
  wildcard, the connection's local address; one fixed type-`3` error menu for refused,
  missing and vanished selectors; limits close with no bytes.
- Exposure (listings off, dot-files hidden) is left to BL-047 in `ContentStore`, not
  duplicated here; until it lands the server lists every directory.
- `ConnectionWriteStream` is copied from the HTTP library: protocol servers may not
  reference each other.
- `touches` gained ADR-0012 and the ADR index `README.md`: the decision record lives
  there, and no task in Doing names either file.
- Fixtures: `file-selector`, `root-menu`, `missing-selector`, plus `path-as-is-dot-segment`
  and `encoded-dot-segment`; all exit 0, and `stdout.bin` equals the reply sent, which the
  tests assert byte for byte.
- Review (code-reviewer): no defects; its note that a link-local IPv6 zone leaked into the
  menu host was fixed. Gates: 51 Gopher tests green, whole fast suite green,
  `Measure-CodeQuality.ps1` 100% line and branch, worst CRAP 10, no failing member.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. surl's Gopher server answers file, menu and missing selectors from the content store as pinned upstream curl 8.21.0 accepts; follow-up BL-066 for gophers
