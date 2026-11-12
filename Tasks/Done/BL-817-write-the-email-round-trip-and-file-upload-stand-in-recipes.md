---
id: BL-817
title: Write the email round-trip and file-upload stand-in recipes as executable tests against a Surl.Api server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-639, BL-638, BL-810]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-817 — Write the email round-trip and file-upload stand-in recipes as executable tests against a Surl.Api server

## Goal

Two recipe test classes show, as short readable tests a user can copy, how an integration test uses Surl as a
stand-in: start one `Surl.Api` server with several protocols, let a real client send over one protocol, wait for
the store change instead of polling, read it back over another protocol, and reset between tests.

## Context

- Recipes are executable documentation: each test is short, Arrange/Act/Assert with one comment line per step
  saying what a user would do and why; BL-821's wiki page links each test by path and name. They run real loopback
  sockets and pinned upstream curl 8.21.0 (the client a reader would recognise), so they are
  `[TestCategory("Integration")]` in `Conformance/Surl.Conformance.UnitTests` like every class there; a missing pin
  is Inconclusive (`PinnedUpstreamCurl`). Never the Curl port (ADR-0003).
- Built on: BL-618 (the project's reference to `Application/Surl.Api` and the pattern of curl against a
  `SurlServer`), BL-639 and BL-638 (the cross-protocol proofs and helpers `StoredMail.cs`, `AccountsFile.cs`,
  `PinnedUpstreamCurl.cs` - reuse, do not duplicate their matrices), BL-807 (`WaitForEventAsync`), BL-810
  (seeding and `ResetAsync`).
- `StandInMailRoundTripRecipeTests`: one server on `smtp`, `imap` and `pop3` at `127.0.0.1:0` with a mail domain
  and two mailboxes (through `AdditionalArguments` or BL-668's typed properties if done); curl sends over SMTP; the
  test awaits the mail store's delivery event for `bob@...`; curl reads it over IMAP and over POP3 and the bytes
  match; a second test seeds a message with `Mail.DeliverAsync` and curl reads it over POP3; `ResetAsync` between
  tests.
- `StandInFileUploadRecipeTests`: one server on `ftp`, `sftp` and `http` with `AllowUploads`, an account and
  `ThrowawayHostKey`; curl uploads over FTP and then over SFTP (`-k`/`--insecure` for the throwaway host key;
  Inconclusive when the pinned build lacks SFTP, as `UpstreamCurlBuildFeatures` reports); the test awaits the
  content store's write event and asserts the exchange's listen URL is the FTP (then SFTP) one; curl fetches over
  HTTP and the bytes match.
- Stand-in: the cross-protocol scenarios the principles name, using the shared mail and content stores.

## Acceptance criteria

- [x] Both classes exist with the tests named in Context, each `[TestCategory("Integration")]`, passing on Windows
      with the pinned reference build present, and platform-neutral (no drive-letter path, no Windows-only text).
- [x] No test uses `Thread.Sleep`, `Task.Delay` or a polling loop: every wait is a `Surl.Api` wait with a timeout.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Both classes use Surl.Api options directly; 5 tests pass against the pinned curl 8.21.0 (mail round trip, seed+POP3, reset, FTP upload, SFTP upload). No waits except WaitForEventAsync.

## Log

- 2026-11-05: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Mail round-trip and file-upload recipes pass against pinned curl
