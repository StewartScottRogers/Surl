---
id: BL-1914
title: Make the --allow-anonymous, --allow-plaintext-auth and --auth explanations true of every server that now lists them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1914 — Make the --allow-anonymous, --allow-plaintext-auth and --auth explanations true of every server that now lists them

## Goal

The explanations of `--allow-anonymous`, `--allow-plaintext-auth` and `--auth` say nothing false or incomplete about the servers whose help pages show them.

## Context

- Found by BL-1902, which added these options to the help pages of more servers.
- `AllowAnonymousExplanation` and `AllowPlaintextAuthExplanation` in `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`
  list only some of the servers each option loosens, and `AuthExplanation` does not say which methods the DNS over
  HTTPS, gRPC, Cloud Storage, MCP, SignalR and WebTransport servers judge by it.

## Acceptance criteria

- [x] Each of the three explanations either names every server in the option's categories that it changes, or says in words that the servers named are examples.
- [x] Each statement is checked against the server code it describes.
- [x] The pinned explanation lines in `Surl.Cli.UnitTests` match.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Decisions, and why:

- `--allow-anonymous` and `--allow-plaintext-auth` say "For example" and then "the other servers whose help pages
  list this option": a full list of 30-odd servers would go stale with every new server.
- `--allow-anonymous` also lists the checks and refusals that stay, because "every request and every login" was false.
- `--auth` names every HTTP-family server in its categories and what each takes of the HTTP methods, and says which
  password logins it does not decide.
- No behaviour changed, so `ManualText` and the `AiHelpProse` paragraphs are as before. ADR-0034 decision 5 keeps its
  original paragraphs; it already says the text follows the code.

Statements checked against:

- `Surl.Authentication`: `AuthenticationPolicy`, `HttpAuthenticationSession`, `DigestMd5SaslExchange`,
  `NtlmSaslExchange`, `GssapiSaslExchange`.
- `Surl.Composition`: `ExtendedProtocolServers`, `AuthenticationComposition`, `ServerTlsComposition.RequiresCertificate`,
  `LdapAccountSourceComposition`.
- Writes and refusals under anonymous: `S3Login`, `GcsLoginJudge`, `AzureLoginJudge`, `OciAccessCheck`,
  `OciTokenEndpoint`, `McpHttpRequestAnswerer`, `McpPasswordCheck`, `McpAuthorizeEndpoint`, `GrpcStreamHandler`,
  `WebTransportConnectAuthentication`, `NatsServerConnection`, `XmppServerConnection`, `ContentShare`,
  `TopicAccessPolicy`.
- Mechanism lists: `KafkaSaslHandshakeAnswerer`, `XmppServerConnection.OfferableMechanisms`, `Mqtt5EnhancedAuthentication`.

Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
30299 passed, 3 skipped, 0 failed. No integration tests run: help text only.

Filed: BL-1916 (the `--allow-anonymous` start-up warning and `README.md` overstate in the same way).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. the --allow-anonymous, --allow-plaintext-auth and --auth explanations match the code of every server that lists them
