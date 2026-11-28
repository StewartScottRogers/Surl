---
id: BL-2025
title: Draw OCI registry bearer tokens from RandomNumberGenerator so --random-seed cannot predict them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2025 — Draw OCI registry bearer tokens from RandomNumberGenerator so --random-seed cannot predict them

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

- [x] A unit test shows that two `oci` servers whose exchanges carry the same seeded `Identifiers` issue different tokens.
- [x] Production token bytes come from `RandomNumberGenerator`; tests can still inject a known source without any static mutable state.
- [x] The upload session UUID still follows `ExchangeContext.Identifiers` (ADR-0163), and its tests pass unchanged.
- [x] The library's `CLAUDE.md` and the `///` comments of `OciTokenBook` and `OciTokenEndpoint` name the source the code uses.
- [x] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

- Plan: `OciTokenBook` takes a `RandomNumberGenerator`; the server's internal constructor takes an optional one
  and the public constructors get `RandomNumberGenerator.Create()`, so `Surl.Composition` does not change.
- No new ADR: ADR-0115 decision 4.3 and ADR-0124 decision 6 already decide the source; the code now follows them.
- Test first: `AnswerAsync_TwoServersWithTheSameSeededIdentifiers_IssueDifferentTokens` failed on the old code (both
  servers issued the same token).
- `OciSessionUuidTests` is unchanged. Two tests that use the public constructors (`OciRegistryProtocolServerTests`,
  `OciRepositoryTests`) now read the token from the `/token` answer, since they cannot know it beforehand; their
  assertions are unchanged.
- The new test's seeded fake repeats the private one in `OciSessionUuidTests`, left separate so that file stays unchanged.
- BL-2024 (the `context` parameter comment): this task rewrote that comment to name the clock behind `issued_at` and the
  login's exchange identity. The token bytes no longer come from the exchange, which BL-2024's context still says.
- Gates: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 30423 passed;
  `Measure-CodeQuality.ps1` for the library 0 failing members; code review found nothing to fix.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. oci bearer tokens now come from RandomNumberGenerator, so --random-seed cannot predict them
