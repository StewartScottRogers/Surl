---
id: BL-721
title: Check Azure credentials under --allow-anonymous and strip the path-style account segment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424, BL-717]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-721 — Check Azure credentials under --allow-anonymous and strip the path-style account segment

## Goal

Under `--allow-anonymous` the Azure Storage server still checks a bearer token a request carries, and refuses a
bad one `401 InvalidAuthenticationInfo` rather than serving it as anonymous; and with an IP-literal or
`localhost` `Host`, a first path segment naming a named account is removed before the container is read.

## Context

- ADR-0109 decision 3.1 ("A request that carries credentials is checked even then; a bad one is never served as
  anonymous") and decision 2 (the path-style form). Matrix rows `AzureStorage:Surl-Auth-1` and
  `AzureStorage:NamingAndReferencing-1` in `Documentation/Conformance/Extended/AzureStorage.md`.
- BL-424 built the server on `IHttpAuthenticationSession`. `Surl.Authentication`'s `HttpAuthenticationSession`
  answers every request `Proceed` with no account under `--allow-anonymous` without running the bearer
  verifier, so the Azure server cannot tell a bad token from a good one there; today it serves both as
  anonymous reads.
- The server references neither `Surl.Authentication` nor an account (ADR-0084 decision 4), so the account
  names the path-style form needs must reach it through a `Surl.Protocol.Abstractions` contract. BL-717 adds
  the Shared Key and SAS checks on the same seam; this task follows it.
- The HTTP server's behaviour under `--allow-anonymous` must not change (its fixtures are upstream curl's).

## Acceptance criteria

- [ ] A named Azure test shows `Authorization: Bearer wrong` under anonymous answered `401 InvalidAuthenticationInfo`
      with the Bearer challenge, and a good token under anonymous served as the account.
- [ ] A named Azure test shows `GET /account1/photos/a.txt` with `Host: 127.0.0.1:10000` served from container
      `photos` when `account1` is a named account, and from container `account1` when it is not.
- [ ] Every existing `Surl.Authentication.UnitTests` and `Surl.Protocol.Http.UnitTests` test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the changed libraries keep 100% line
      and branch coverage.

## Notes

- Filed by BL-424, which could not change `Surl.Authentication` or `Surl.Protocol.Abstractions`.

## Log

- 2026-11-05: Created.
