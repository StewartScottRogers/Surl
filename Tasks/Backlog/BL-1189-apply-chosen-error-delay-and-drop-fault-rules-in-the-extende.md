---
id: BL-1189
title: Apply chosen-error, delay and drop fault rules in the extended OCI registry server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1189 — Apply chosen-error, delay and drop fault rules in the extended OCI registry server

## Goal

An `error`, `delay;stage=command` or `drop;stage=command` fault rule naming `oci` (or a listener of one) makes the OCI registry answer the matching request with the chosen error, hold it, or cut it off - instead of `--fault` refusing the `error` with "scheme oci has no error shape" and the server ignoring delay and drop.

## Context

- Stand-in audit 2026-11-14: `Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary` never asks `ExchangeContext.Faults`, and `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs` has no `oci` row. BL-837's audit left it to its serve task (BL-1028), which did not add it; ADR-0124 decision 9 says every server wires `error` and adds its row.
- Decided by Claude under Stewart's delegation in this task, as BL-983 did for the object stores, and recorded as ADR-0124 decision 4's row: the reply is an HTTP status 400 to 599 and an error code from the OCI Distribution Specification v1.1.0's error-code table (`BLOB_UNKNOWN`, `DENIED`, `TOOMANYREQUESTS`, ...), written in the specification's `{"errors":[{"code","message"}]}` body; `command` is the method and `path` the request path. The task may refine this and says so in the row.
- Use `HttpCommandFaults.FindAndApplyAsync` (`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs`) for delay and drop and to get the `error` back, as the HTTP server does. ADR-0115 and ADR-0130 are the registry's decision records; judged by the OCI specifications (ADR-0083). The token endpoint is judged by the authentication policy; decide and state whether faults apply there too.

## Acceptance criteria

- [ ] Named tests: a request matching an `error` rule gets exactly the chosen status and an OCI error body with the chosen code; a non-matching request is served; one `FaultAppliedEvent`.
- [ ] Named tests: `delay;stage=command` holds the matching request for `ms` on a fake `TimeProvider` then answers it; `drop;stage=command` aborts with no response; one `FaultAppliedEvent` each.
- [ ] Named `Surl.Cli` tests: one accepted and one refused `reply` for `oci`.
- [ ] ADR-0124 decision 4's extended-server table has the `oci` row.
- [ ] With no rule, every existing OCI registry test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
