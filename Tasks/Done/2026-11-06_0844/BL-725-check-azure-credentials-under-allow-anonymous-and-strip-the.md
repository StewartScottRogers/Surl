---
id: BL-725
title: Check Azure credentials under --allow-anonymous and strip the path-style account segment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424, BL-717]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-725 — Check Azure credentials under --allow-anonymous and strip the path-style account segment

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

- [x] A named Azure test shows `Authorization: Bearer wrong` under anonymous answered `401 InvalidAuthenticationInfo`
      with the Bearer challenge, and a good token under anonymous served as the account.
- [x] A named Azure test shows `GET /account1/photos/a.txt` with `Host: 127.0.0.1:10000` served from container
      `photos` when `account1` is a named account, and from container `account1` when it is not.
- [x] Every existing `Surl.Authentication.UnitTests` and `Surl.Protocol.Http.UnitTests` test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the changed libraries keep 100% line
      and branch coverage.

## Notes

- Filed by BL-424, which could not change `Surl.Authentication` or `Surl.Protocol.Abstractions`.
- BL-717 (2026-11-05): a request with `HttpAuthenticationRequest.AzureStorage` set is now judged by
  `HttpAuthenticationSession.JudgeAzureStorageAsync`, which checks every credential even under
  `--allow-anonymous`, so a bad bearer token there is already `401 InvalidAuthenticationInfo`; the first
  criterion's named Azure test remains. The scope's `AccountName` is still the `Host`'s first label; the
  path-style segment should replace it there too, so a SAS on an IP-literal host names its account.
- 2026-11-06, what was built. The seam is `IAuthenticationPolicy.NamesAccount(string)`, a default
  interface member that matches nothing, so the 27 other implementations (test fakes across many
  projects outside `touches`) need no change and a policy without named accounts never applies the
  path-style form, as ADR-0109 decision 2 says. `AuthenticationPolicy` answers it with
  `AccountBook.HasNamedAccount` (any non-empty named account, base64 password or not, ordinal).
  Chosen over a field on `AzureStorageScope` or the verdict because the server must know the
  container before it can name the resource path the session judges.
- The Azure server's new `AzurePathStyle.Read` strips the segment before login on both endpoints.
  The session still gets the target as sent, so a Shared Key canonicalized resource carries the
  account twice as the page says; the scope's `AccountName` is the segment and `ResourcePath` the
  path after it. An IPv4 literal must have four dotted parts so a one-label numeric name is not an
  address; a bracketed IPv6 literal is one, a bracketed IPv4 is not.
- No new ADR: ADR-0109 decision 2 already decides the behaviour, and `Documentation/Planning/Decisions`
  is in BL-701's `touches`; the seam is recorded here and in the three projects' CLAUDE.md files.
- `Documentation/Conformance/Extended/AzureStorage.md` added to `touches` (no task in Doing names
  it) to name the tests for rows `NamingAndReferencing-1` and `Surl-Auth-1`.
- Criterion 1: the session side was already BL-717's; the named tests are
  `AzureStorageProtocolServerTests.ServeAsync_WrongBearerTokenUnderAllowAnonymous_Is401InvalidAuthenticationInfoWithTheChallenge`,
  `ServeAsync_GoodBearerTokenUnderAllowAnonymous_IsServedAsTheAccount`, and in `Surl.Authentication.UnitTests`
  `AzureStorageVerifierTests.Bearer_WrongTokenUnderAllowAnonymous_IsForbiddenAsUnknownAccount` and
  `Bearer_GoodTokenUnderAllowAnonymous_IsAcceptedAsTheEmptyName`. Criterion 2: `AzurePathStyleTests`.
- Left as is: `x-ms-rename-source` on a path-style Data Lake rename still names `/<filesystem>/<path>`
  without the account segment, and List Blobs' `ServiceEndpoint` stays `http(s)://<Host>/`.
- Coverage measured with the collector: `AzurePathStyle`, `AzureLoginJudge`, both answerers,
  `AzureStorageProtocolServer`, `AuthenticationPolicy` and `IAuthenticationPolicy` at 100% line and branch.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Azure bearer tokens are checked under --allow-anonymous (named tests) and a path-style /<account>/ segment on an IP-literal or localhost host is removed before the container is read
