---
id: BL-1232
title: Reset, snapshot and restore the OCI registry's uploads and tokens and judge tokens against the account book
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216, BL-1228]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1232 — Reset, snapshot and restore the OCI registry's uploads and tokens and judge tokens against the account book

## Goal

A reset or restore aborts the OCI registry's open upload sessions and revokes its tokens, and a removed account's token stops working at once.

## Context

ADR-0167 decisions 2 to 5. `OciUploadSessions`, `OciTokenBook`, `OciTokenEndpoint`. Register `StoreKind.Uploads` named `oci` (refusing on a disk-backed content store) and `StoreKind.Sessions` for the token book from Surl.Composition. `OciTokenBook.Find` result must also be checked against the account book on every use: a token whose account no longer exists is refused with the ordinary `401` challenge. Use the account lookup the server already uses to judge a login.

## Acceptance criteria

- [x] After reset, PATCH or PUT to an earlier upload location answers `404 BLOB_UPLOAD_UNKNOWN` and an earlier bearer token is answered `401` with the challenge.
- [x] A token issued to an account that is then removed is refused on its next use; an anonymous token is not.
- [x] Snapshot reports 0 bytes; restore clears as reset does; one `StoreChangedEvent` per registration.
- [x] A disk-backed content store makes the uploads registration refuse.
- [x] 100% line and branch coverage for the changed code.

## Notes

- Decision: the token registration is named `oci` on `StoreKind.Sessions`, joined with MQTT 5 sessions by `UploadStoreRegistrations.CombineOf`. A session taken out by an in-flight request is not aborted (returns afterwards); accepted as a narrow race.
- Decision: `NamesAccount` on the policy is the account lookup (ADR-0167 decision 4).

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Reset, snapshot, restore and account-book token checks done
