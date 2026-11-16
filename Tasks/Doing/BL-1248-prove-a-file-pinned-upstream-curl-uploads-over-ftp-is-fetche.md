---
id: BL-1248
title: Prove a file pinned upstream curl uploads over FTP is fetched over SMB from the same surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md, Documentation/Wiki/Surl-As-A-Stand-In.md]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1248 — Prove a file pinned upstream curl uploads over FTP is fetched over SMB from the same surl

## Goal

An `Integration` test shows pinned upstream curl uploading a file over `ftp://` to one `surl` and fetching exactly those bytes back over `smb://` from it, so the product overview's claim that a file uploaded over FTP is fetched over SMB is proved, not only written.

## Context

- Found by the second stand-in audit (2026-11-16). `Documentation/Product/Product-Overview.md` ("One store per job, per `surl` run") says "a file uploaded over FTP is fetched over HTTP, SFTP, SMB or `s3`". ADR-0100 decision 5's table and `Conformance/Surl.Conformance.UnitTests/UpstreamCurlSharesFilesAcrossSurlProtocolsTests.cs` prove `ftp`->`http`, `sftp`->`ftp`, `http`->`scp` and an overwrite; `CurlPortClientReadsFilesSharedAcrossSurlProtocolsTests.cs` proves `ftp`->`s3`. No test fetches over `smb` what another protocol wrote.
- Start from both files: `UpstreamCurlSharesFilesAcrossSurlProtocolsTests` (one `surl` with several schemes, `--allow-uploads`, `--user tester:secret`, an in-memory and a `--directory` row, `IsolatedCurlHome`) and `Conformance/Surl.Conformance.UnitTests/UpstreamCurlTransfersFilesWithSurlOverSmbTests.cs` (`--auth ntlmv1 -u <account>`, `PinnedUpstreamCurl.RunForProtocolAsync(TestContext, "smb", ...)`, which picks the pinned build that lists `smb`: ADR-0030's static-curl build on Windows, because the reference 8.21.0 build has no `smb`; inconclusive where none is installed).
- SMB serves each top-level folder of the served root as a share (ADR-0073), so the file goes to a folder: upload to `ftp://127.0.0.1:<port>/docs/shared.bin` with `--ftp-create-dirs` (Surl's FTP server answers `MKD`), fetch `smb://127.0.0.1:<port>/docs/shared.bin`. Use bytes that are not plain text (a zero byte, `0xFF`, CR LF), as the existing class does.
- Both ends are original protocols, so pinned upstream curl is the only client (ADR-0003). Surl's answer bytes are already pinned by each server's own tests; this test checks only curl's exit code and output.

## Acceptance criteria

- [ ] `UpstreamCurlSharesFilesAcrossSurlProtocolsTests` (or a new class beside it, named in Notes) has `UploadOverFtp_FetchOverSmb_FetchesTheUploadedBytes`, with an in-memory and a `--directory` row, `[TestCategory("Integration")]`.
- [ ] Each row: curl's FTP upload exits 0, and curl's SMB fetch exits 0 with standard output equal to the uploaded bytes exactly; inconclusive, naming the pin, where no pinned build lists `smb`.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UploadOverFtp_FetchOverSmb"` passes on Windows with the pinned static-curl build installed.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean, and the fast run (`TestCategory!=Integration`) is unchanged.
- [ ] The `UpstreamCurlSharesFilesAcrossSurlProtocolsTests` row of ADR-0100 section 5's table ("Cross-protocol tests") lists the `ftp` then `smb` pair, and `Documentation/Wiki/Surl-As-A-Stand-In.md`, section "Composing protocols over the shared stores", says a file uploaded over FTP is fetched over SMB too, naming the test.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
