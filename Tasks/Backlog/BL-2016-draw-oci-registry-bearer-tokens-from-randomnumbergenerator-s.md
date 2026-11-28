---
id: BL-2016
title: Draw OCI registry bearer tokens from RandomNumberGenerator so --random-seed cannot predict them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2016 — Draw OCI registry bearer tokens from RandomNumberGenerator so --random-seed cannot predict them

## Goal

An `oci` server's bearer tokens cannot be predicted from `--random-seed`.

## Context

Found while working BL-2005 (2026-11-27), read from source and not run. `OciTokenEndpoint` calls
`tokens.Issue(accountName, ..., context.Identifiers)`, and `OciTokenBook.Issue` fills the token's 32 bytes from that
`IIdentifierRandom`. Under `--random-seed` that is the seeded source, so a peer who knows the seed can work out a
token that grants an account's pushes and deletes.

- ADR-0124 decision 6: tokens and anything that grants access are never seeded; they stay on `RandomNumberGenerator`.
- ADR-0115 decision 4.3: the token's production source is `RandomNumberGenerator`.
- ADR-0163 lets the upload UUID follow the seed because every request still needs the token. It stays as it is.
- The library's `CLAUDE.md`, "Token authentication", says tokens are drawn from `ExchangeContext.Identifiers`.

`Surl.Extended.Protocol.McpServer`'s `McpProtocolServer`, which takes a `RandomNumberGenerator`, shows one way to keep
the source injectable for tests. If the fix needs `Surl.Composition`, add it to `touches` as the board rules allow.

## Acceptance criteria

- [ ] A unit test shows that two `oci` servers whose exchanges carry the same seeded `Identifiers` issue different tokens.
- [ ] Production token bytes come from `RandomNumberGenerator`; tests can still inject a known source without any static mutable state.
- [ ] The upload session UUID still follows `ExchangeContext.Identifiers` (ADR-0163), and its tests pass unchanged.
- [ ] The library's `CLAUDE.md` and the `///` comments of `OciTokenBook` and `OciTokenEndpoint` name the source the code uses.
- [ ] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
