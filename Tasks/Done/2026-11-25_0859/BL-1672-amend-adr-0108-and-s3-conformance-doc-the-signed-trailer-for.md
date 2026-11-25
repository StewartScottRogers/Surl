---
id: BL-1672
title: Amend ADR-0108 and S3 conformance doc: the signed trailer form STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER is served (BL-1665)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation, Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1672 — Amend ADR-0108 and S3 conformance doc: the signed trailer form STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER is served (BL-1665)

## Goal

ADR-0108 and `Documentation/Conformance/Extended/S3.md` say the S3 server serves the signed trailer form
`STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER`, as BL-1665 built it.

## Context

BL-1665 added `S3PayloadForm.SignedChunksWithTrailer`: every chunk signature, the `x-amz-trailer-signature`
(string to sign `AWS4-HMAC-SHA256-TRAILER`, chained from the final chunk's signature) and the trailing checksum are
checked, through `IHttpChunkSignatureChain.VerifyTrailer`. ADR-0108 decision 4.5 and Amendment 9 decision 5 still list
the signed `-TRAILER` form as unserved (`501`); only the ECDSA forms remain so.
Decided by Claude under Stewart's delegation.

## Acceptance criteria

- [x] ADR-0108 gains an amendment serving the signed trailer form, and the conformance doc lists it as served.
- [x] `Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md` names the trailer chain.

## Notes

Added the Authentication CLAUDE.md to touches (criterion 2 edits it; no Doing task touches it).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. ADR-0108 amendment 10, S3 conformance doc and Authentication CLAUDE.md state the signed trailer form is served
