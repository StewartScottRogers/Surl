# ADR-0128 — How Surl.Authentication publishes judged logins and applies refuse-login

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-791.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1 (`LoginJudgedEvent`) and 4
  (`refuse-login`), ADR-0032 sections 4, 5 and 8 (the refusal delay and the login note).
- **Acted on by:** BL-791; BL-871 carries the exchange and listen URL on the login requests.

## Context

ADR-0124 has `AuthenticationPolicy` publish each judged login and refuse a login a `refuse-login` rule
matches "as a wrong password is, after the same delay", scoped "by account and listener or scheme as the
login request carries them". The login requests in `Surl.Protocol.Abstractions` carry less than that:
`PasswordLogin`, `ApopLogin` and `SaslExchangeStart` carry their scheme, but `StartHttpConnection`,
`SshPasswordLogin`, `SshPublicKeyLogin` and `SmbNtlmV1Login` carry none, and no request carries the
exchange ID or the listen URL. BL-791 may change only `Surl.Authentication`.

## Decision

1. **Constructor.** `AuthenticationPolicy` takes an optional `ISurlEventPublisher` and `IFaultPlan`,
   defaulting to `DiscardingSurlEventPublisher.Instance` and `EmptyFaultPlan.Instance`, so a policy given
   neither behaves exactly as before. `Surl.Composition` passes the instance's (BL-803).
2. **Which logins are published.** Every login whose credentials were checked: exactly the logins that
   carry a `CheckedLogin` note (accepted or refused), plus `CheckPasswordLoginAsync`'s `Accepted` and
   `RefusedCredentials`, whose `Mechanism` is `password`. Unchecked answers (`--allow-anonymous`, a
   plain-text secret refused unchecked, no user name, a mechanism not offered, a handshake's continuation
   step) are not published, as they are not noted. `Account` is the user name as the note names it
   (`bearer token` for a bearer token), empty when none could be read; `Mechanism` is the note's method.
3. **Scheme and exchange.** `Scheme` is the request's scheme where it carries one. HTTP, SSH and SMB
   logins are published and matched under their family: `AuthenticationPolicy.HttpLoginScheme` (`http`,
   for every scheme the HTTP family serves), `SshLoginScheme` (`ssh`) and `SmbLoginScheme` (`smb`).
   `ExchangeId` is null and the `FaultPoint` has no listen URL, so a `listener=` rule never matches a login
   yet. BL-871 adds both to the requests and replaces the family names with the real scheme.
4. **When the fault plan is asked.** Only once credentials have matched, with
   `FaultPoint(FaultStage.Login) { Scheme, Account = <the account matched> }`. A login the credentials
   already refuse never asks, so it never spends a rule's `times`. A returned rule of any kind but
   `RefuseLogin` is not applied at a login.
5. **How the refusal looks.** Exactly as the same method refuses a wrong credential: the same verdict,
   note (now `Login refused`) and `RefusalDelay` on the injected `TimeProvider`. HTTP Basic, Digest,
   Bearer, NTLM and Negotiate refuse with a challenge and no `Refusal`; Signature Version 4 with
   `BadSignature`; Azure Storage with `UnknownAccount` for a bearer token and `BadSignature` otherwise; an
   NTLM or Negotiate connection stays logged out. SASL mechanisms refuse at the step that accepted: SCRAM
   with `e=invalid-proof`, `XOAUTH2` and `OAUTHBEARER` with their error challenge, the rest as
   `RefusedCredentials`; `DIGEST-MD5` and SCRAM on the mail servers are refused before their `rspauth` or
   `v=` challenge is sent. The fault never makes a refused login succeed.
6. **Events.** A fault-refused login publishes `FaultAppliedEvent(rule.Id, RefuseLogin, Login, null,
   <account>)` and then `LoginJudgedEvent` with `IsAccepted` false and `IsRefusedByFault` true. No event
   carries a password, hash, token or response.

## Consequences

- A test can assert who logged in, by what method, and make a chosen account's logins fail, on every
  protocol, with no protocol server changed.
- Until BL-871, a test cannot tell `sftp` from `scp`, or `https` from `http`, in a login event, and cannot
  scope `refuse-login` by listener.

## Amendment 1 (BL-871): the exchange and the real scheme

Decided by Claude under Stewart's delegation.

1. **The contract.** `LoginExchange(ExchangeId, ListenUrl)` in `Surl.Protocol.Abstractions` carries what
   decision 3 lacked. It is an optional last parameter (`Exchange`, default `null`) of `PasswordLogin`,
   `ApopLogin`, `SshPasswordLogin`, `SshPublicKeyLogin`, `SmbNtlmV1Login` and `SaslExchangeStart`, so every
   existing caller compiles. `LoginExchange.Of(ExchangeContext)` builds it. `SshNoneLogin` is never published and
   gets none. Exchange IDs are per connection, so HTTP carries the exchange on the session:
   `IAuthenticationPolicy.StartHttpConnection(tlsSession, exchange)`, a default member that ignores the
   exchange, instead of a field on each `HttpAuthenticationRequest`.
2. **What the policy publishes.** With an exchange, `LoginJudgedEvent.ExchangeId` is its ID and
   `LoginJudgedEvent.Scheme` is its listen URL's scheme (`sftp`, `https`), which wins over the request's or
   the family's scheme. The `FaultPoint` gets `ExchangeId`, `ListenUrl` (the reported form) and `Scheme`, so a
   `refuse-login` rule scoped by `listener=` matches a login, and `FaultAppliedEvent.ExchangeId` is set.
   Without an exchange the behaviour is decision 3's: null ID, family scheme, no listen URL.
3. **The servers.** This amendment lands the contract and the policy. Each server family passes
   `LoginExchange.Of(context)` on its login requests in its own task, so until a server does, its logins are
   published as decision 3 says.
