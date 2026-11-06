---
id: BL-438
title: Require token authentication on the OCI registry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-437]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Documentation/Planning/Decisions/ADR-0115-how-the-oci-registry-server-answers-oci.md, Documentation/Conformance/Extended/OciRegistry.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-438 — Require token authentication on the OCI registry

## Goal

By default the OCI registry challenges with `WWW-Authenticate: Bearer realm=…,service=…,scope=…` and serves its
own token endpoint, which checks the accounts file's credentials. It then accepts only tokens whose scope covers
the repository and action. Anonymous pulls are allowed only under `--allow-anonymous`.

## Context

- Decision: BL-435's ADR, decision 4.
- Docker Token Authentication Specification: the challenge parameters, the token request
  (`GET <realm>?service=…&scope=repository:<name>:pull,push`) with Basic credentials, the
  `{"token":…,"access_token":…,"expires_in":…,"issued_at":…}` response, and `401 UNAUTHORIZED` /
  `403 DENIED` (distribution spec error codes).
- Credentials are checked through the `Surl.Protocol.Abstractions` contracts. Tokens come from a random source
  injected for tests, and expire on an injected `TimeProvider`.

## Acceptance criteria

- [x] Named tests show:
      - an unauthenticated pull challenged with the three parameters
        (`OciAccessCheckTests.CheckAsync_PullWithoutAToken_Returns401WithTheRealmServiceAndScope`);
      - a token issued for good credentials and refused for bad ones
        (`OciTokenEndpointTests.AnswerAsync_GoodCredentials_IssuesATokenWithItsLifetimeAndIssueTime`,
        `OciTokenEndpointTests.AnswerAsync_BadOrUnreadableCredentials_Returns401WithTheBasicChallenge`);
      - a pull accepted with a pull-scoped token (`OciAccessCheckTests.CheckAsync_PullWithAPullScopedToken_IsServed`);
      - a push with a pull-only token gets `DENIED`
        (`OciAccessCheckTests.CheckAsync_PushWithAPullOnlyToken_Returns403DeniedWithInsufficientScope`);
      - an expired token is refused (`OciAccessCheckTests.CheckAsync_ExpiredToken_Returns401InvalidToken`);
      - an anonymous pull is allowed under `--allow-anonymous` and an anonymous push still refused
        (`OciAccessCheckTests.CheckAsync_AnonymousPullUnderAllowAnonymous_IsServedAsTheAnonymousCaller`,
        `OciAccessCheckTests.CheckAsync_AnonymousPushUnderAllowAnonymous_Returns401WithTheChallenge`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Plan: `GET /token` (`OciTokenEndpoint`) checks `Basic` with `IAuthenticationPolicy.CheckPasswordLoginAsync`
  and issues opaque tokens kept in `OciTokenBook` (in `OciServerState`, 32 bytes from
  `ExchangeContext.Identifiers`, 300 s on the `TimeProvider`, 10000 live). `OciAccessCheck` checks
  `Authorization: Bearer` on every `/v2/` request and builds the `WWW-Authenticate` challenge (`OciChallenge`).
  `OciRegistryProtocolServer` now takes the `IAuthenticationPolicy`; nothing composes it yet (BL-439 will).
- Decisions (ADR-0115 Amendment 1, decided under Stewart's delegation): endpoint, method and name are judged
  before the token, and a write while uploads are off is `403 DENIED` before it; the grant is not pre-checked
  against share access, which the content store applies per request; unknown actions and scopes past 16 are
  dropped, not refused; under `--allow-anonymous` the policy accepts every login unchecked, so every token is then
  anonymous and pull-only; an unknown token waits no refusal delay; the realm is `https` only on a TLS connection.
- Tests: the harness fetches a token as `tester` for each `/v2/` request without `Authorization`
  (`SendsTokens`, on by default), so the 260 earlier tests keep exercising the real flow; the new tests turn it
  off. `UnitTestAuthenticationPolicy` mirrors `Surl.Authentication`'s password judgement; `CountingIdentifierRandom`
  makes tokens predictable.
- Touches widened to ADR-0115 (the amendment) and `Documentation/Conformance/Extended/OciRegistry.md` (its
  BL-438 rows now name their tests, and OCI:Surl-Errors-2 and OCI:Token-Scope-1 say what the code does); no task in
  Doing names either.
- Follow-ups filed: BL-890 (Bearer tokens over plain text without `--allow-plaintext-auth`, after BL-439) and
  BL-891 (bind an upload session to the account that opened it).
- Measured: 347 tests in `Surl.Extended.Protocol.OciRegistry.UnitTests` (87 new), library line and branch coverage
  100%; full fast suite green.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The OCI registry requires Bearer tokens from its own GET /token (Basic checked by the authentication policy), scoped per repository and action, expiring after 300 s; anonymous pulls only under --allow-anonymous
