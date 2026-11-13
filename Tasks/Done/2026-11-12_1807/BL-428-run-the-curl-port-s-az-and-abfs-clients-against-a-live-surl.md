---
id: BL-428
title: Run the Curl port's az and abfs clients against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1023, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-428 — Run the Curl port's az and abfs clients against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl az://` and `surl abfss://`: a blob read,
write and listing, a Gen2 file create-append-flush and read, and a refused unauthenticated request. Each
assertion comes from the Azure REST API, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3. The port's client is only a live client. Assertions are justified
  by the REST API pages, and a port defect is reported in Notes, never worked around.
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. The port's command lines: its
  `curl --extended-help az` and its cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/`.
- Every test is `[TestCategory("Integration")]` and inconclusive when the pinned client is absent.

## Acceptance criteria

- [x] The test class exists with the exchanges above. Each passes on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/AzureStorage.md`: every row is covered by a named test or marked `n/a`
      with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- 2026-11-12 (lane 1): Added `CurlPortClientTransfersBlobsAndFilesWithSurlOverAzureTests` (7 tests: az get, put, list; abfss get, create-append-flush put, list; unauthenticated az get refused). All pass on Windows with the pinned port client. The port's Azure client only sends `Authorization: Bearer` or a SAS and always connects to port 443 over TLS (host from the URL), so each test starts surl on `127.0.0.1:443` with `--self-signed` and `--user :<token>`, passes `-k`, and sets `AZURE_STORAGE_BEARER_TOKEN`. Only one listener can own 443, so the class is `[DoNotParallelize]`; it is inconclusive if 443 cannot be bound. The abfs (plain HTTP) scheme is not reachable by the port's client, so abfss stands for the Data Lake endpoint. List needs `--list-directories`. No port defect found.
- `AzureStorage.md`: every row now names unit tests (and the live tests where they prove it). `Surl-Limits-4` is n/a (the connection host enforces timeouts for every server). `Surl-Tls-1` to `Surl-Tls-3` are n/a here: the executable's TLS and plaintext wiring belongs to BL-427, which is Deferred.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-427 replaced by BL-1023 (BL-427 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. The Curl port's az and abfss clients read, write and list against a live surl (7 Integration tests); AzureStorage.md rows all name tests or are n/a
