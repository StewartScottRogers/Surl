# ADR-0170 — How the coaps pre-shared keys, the proxy accounts and the TLS-SRP accounts are judged, published and controlled

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in BL-1244.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1 (`LoginJudgedEvent`), 3 (accounts as a
  store) and 4 (`refuse-login`), [ADR-0132](ADR-0132-how-the-account-book-changes-while-the-server-runs.md) (the account
  book changes while the server runs), [ADR-0128](ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md),
  [ADR-0165](ADR-0165-how-tls-srp-and-socks5-gss-api-logins-publish-login-judged-events-and-honour-refuse-login.md) (the
  engine-side login judge for TLS-SRP), [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md)
  decision 5 (proxy accounts), ADR-0122 (TLS-SRP), ADR-0083 (extended servers) and ADR-0087 decision 10 (`coaps`).
- **Amends:** ADR-0124 decisions 1, 3 and 4; ADR-0132 (the account book is not the only account source).
- **Acted on by:** the tasks listed under Consequences.

## Context

Three account sources live outside the instance's `AccountBook`, and so outside everything ADR-0124 gives a test:

1. `coaps` pre-shared keys: `DeferredCoapsProtocolServer` copies `PreSharedKeyAccounts` once, on the first flow, into
   a closure. No add, remove, reset or restore reaches it, no `LoginJudgedEvent` is published for a PSK handshake,
   `refuse-login` cannot refuse one, and the `coaps` command-stage fault point has no `Account`.
2. Proxy accounts (`-U`, `--proxy-user-file`): `ComposeProxyPolicy` builds its own policy over its own book that
   `ComposedStores` does not hold. Their logins are already published and faultable through the policy.
3. TLS-SRP accounts (`--tlsuser`, `--srp-verifiers`): `TlsSrpAccounts` is read once at start. ADR-0165 publishes and
   faults their logins.

## Decision

### Which sources are under store control

1. **`coaps` pre-shared keys: in, and they are the instance's own accounts.** A PSK account is a `--user` or
   `--user-file` account admitted for scheme `coaps` (`Account.MayLogInOn`), so it is already in the `AccountBook`.
   No new store: `AddAccount`, `RemoveAccount`, reset, snapshot and restore of `StoreKind.Accounts` now also decide
   who may log in over `coaps`, because the key lookup reads the live book (decision 5). Nothing is added to a host
   surface. Secure by default is kept: an account a test adds is admitted to `coaps` only when its schemes admit it,
   as for every other scheme.
2. **Proxy accounts: in, as their own store, `StoreKind.ProxyAccounts`.** A proxy credential is a different
   credential space from a server account (ADR-0088 decision 5: `-U` never admits a `--user` account to a proxy
   and the reverse), so folding them into `Accounts` would let a test's `alice` on SMTP also pass the proxy. The
   proxy policy's `AccountBook` is held by `ComposedStores` beside the first, and takes part in reset, snapshot and
   restore (`StoreOrder` gains it after `Accounts`) and in `AddProxyAccount` and `RemoveProxyAccount`. With no
   proxy account configured the proxy stays anonymous and the store is absent (not listed), so a test adding a proxy
   account to such an instance is refused, as it is for a store whose servers are not running; it never turns
   anonymous proxying into authenticated proxying behind the test's back.
3. **TLS-SRP accounts: in, as their own store, `StoreKind.TlsSrpAccounts`.** Same reason (a verifier is not a
   password). `TlsSrpAccounts` gains thread-safe `Add`, `Remove`, `Reset`, `TakeSnapshot` and `Restore`, published as
   `StoreChangedEvent`s like every store. `AddTlsSrpAccount(userName, password)` derives the verifier with the
   configured group and a salt drawn from the instance's seedable random, so a seeded test gets the same verifier
   each run (ADR-0124 "Deterministic"). A snapshot holds user names, salts, group indexes and verifiers, never a
   password. As with proxy accounts, the store exists only when TLS-SRP is composed.
4. **Host surfaces.** Each store gets the shape the account store has, with its own names and nothing repurposed
   (ADR-0124 "Configuration as data"):
   - `Surl.Api`: `ProxyAccounts` and `TlsSrpAccounts` members beside `Accounts`, each with `Add`, `Remove`.
   - `Surl.Mcp`: tools `add_proxy_account`, `remove_proxy_account`, `add_tls_srp_account`, `remove_tls_srp_account`.
   - Control endpoint: `POST` and `DELETE` on `/v1/proxy-accounts` and `/v1/tls-srp-accounts`.
   - Reset, snapshot and restore need no new route or tool: the existing `reset`, `snapshot` and `restore` handle
     each `StoreKind` the instance has. The `store` filter of the event stream accepts the two new names.

### How a `coaps` pre-shared-key login is judged

5. **Where the key comes from.** `DeferredCoapsProtocolServer` no longer copies the accounts. `AccountBook` gains
   `TryFindPreSharedKey(string userName, string scheme)` returning the password bytes of the account when it exists
   and admits the scheme, and the lookup calls it for every handshake. A lookup of an unknown identity or an
   account not admitted to `coaps` returns none, exactly as an unknown identity does today.
6. **What is published, and by whom.** `Surl.Dtls` defines `IDtlsLoginJudge`
   (`ValueTask<bool> ShouldRefuseAsync(string identity, CancellationToken)`, `void Judged(string identity, bool isAccepted)`),
   an optional `DtlsServerOptions` member (default none, behaviour unchanged), as ADR-0165 did for TLS-SRP.
   `SecureCoapProtocolServer`, which holds the `ExchangeContext`, supplies it: `Judged` publishes
   `LoginJudgedEvent` with `Mechanism` `psk`, `Account` the PSK identity as UTF-8 text (lossy only for the event),
   `Scheme` `coaps`, and `ExchangeId` the exchange's. No key is in the event.
7. **When.** The login is *checked* once the client's `Finished` verified (an unknown identity is keyed with a
   random key by `Surl.Dtls` today, so it fails at the same place, and a wrong key fails there too). A handshake that
   fails before the `Finished` was never judged and publishes nothing. An unknown identity and a wrong key are both
   published as refused, as in ADR-0165 decision 1.
8. **`refuse-login`.** Asked only after the `Finished` verified, with
   `FaultPoint(FaultStage.Login) { ExchangeId, ListenUrl, Scheme = "coaps", Account = <identity> }` and only a
   `RefuseLogin` rule applies, so a wrong login never spends a rule's `times`. A matching rule makes the handshake
   fail as a wrong key fails: a fatal `decrypt_error` alert, no `ChangeCipherSpec`, no server `Finished`, never
   `unknown_psk_identity` (ADR-0087 decision 10 as amended, `Cryptography/Surl.Dtls.UnitLibrary/CLAUDE.md`). Order of events: `FaultAppliedEvent`,
   then `LoginJudgedEvent` with `IsRefusedByFault` true. `delay`, `drop` and `error` are never applied to the handshake.
9. **The command-stage point gains `Account`.** `CoapRequestFaults.ApplyAsync` sets `Account` to
   `DtlsServerSession.ClientPreSharedKeyIdentity` for a request that arrived over a PSK session, and leaves it empty for
   plain `coap`, a certificate session or a PSK-less one. A rule scoped by `account` then matches `coaps` requests.

### Specifications and measurements

10. **`coaps`.** An extended scheme (ADR-0083), judged by RFC 6347 and RFC 4279. Wire behaviour pinned: a refused or
    wrong-key login ends in `decrypt_error` as RFC 6347 section 4.2.1 and RFC 5246 section 7.2.2 allow, and is
    byte-for-byte what an unknown identity already gets, so no byte changes. No upstream curl measurement applies.
11. **Proxy and TLS-SRP.** No byte Surl sends changes: adding or removing an account changes who is admitted, not how
    admission or refusal is framed. A refused proxy login is the `407` (or SOCKS refusal) that ADR-0088 pinned from pinned upstream curl 8.21.0, and a refused
    TLS-SRP login is the `bad_record_mac` of ADR-0122 and ADR-0165. The follow-up tests re-assert those fixtures; no new
    measurement is needed.
12. **Secrets.** No event, snapshot note or fault record carries a password, key or verifier except the TLS-SRP snapshot's
    verifiers, which are the stored form and not reversible to the password; the snapshot is held in memory as the account
    snapshot is (ADR-0132).

**Amendment 1 (2026-11-25, BL-1688; decided by Claude under Stewart's delegation). The PSK suites are offered while
no account exists.** Amends ADR-0087 decisions 2 and 3 for how `surl` composes `coaps`.

- `DeferredCoapsProtocolServer` builds the server once, at the first flow, and always gives it the live lookup of
  decision 5, whether or not any account admits `coaps` then. So an account added afterwards logs in by PSK, and a
  first flow with no certificate and no account (every account removed before it) no longer fails the build, which
  had failed every later flow too.
- **A PSK-only client while no account admits `coaps`** is answered as any unknown identity is (decision 7, ADR-0087
  decision 10): `ServerHello`, `ServerHelloDone`, then `decrypt_error` once its `Finished` cannot be verified, published
  as a refused `psk` login. Before, it got `handshake_failure` at its hello. Nobody is admitted, and the answer no
  longer tells a peer whether any account exists.
- A client offering certificate and PSK suites still gets a certificate suite when a certificate is served, because
  certificate suites come first in the server's order. With no certificate and no account, a client offering no PSK
  suite still gets `handshake_failure`.
- The start is unchanged: a `coaps` listen URL with no certificate and no account is refused `(58)`.
- Rejected: asking "does an account exist now" at each hello so that no PSK suite is live without one. It needs a new
  `Surl.Dtls` option and keeps a difference a peer can read, for no gain in who is admitted.

## Consequences

A test can seed, remove, reset, snapshot and restore all three account sources, sees every `coaps` PSK login on the event
stream, and can make chosen PSK identities fail. Follow-up tasks, contracts before servers, hosts last:

1. BL-1274: `Surl.Authentication`: `AccountBook.TryFindPreSharedKey`.
2. BL-1275: `Surl.Dtls`: `IDtlsLoginJudge` and its calls in the server handshake.
3. BL-1276: `Surl.Protocol.Abstractions`: `StoreKind.ProxyAccounts` and `StoreKind.TlsSrpAccounts`.
4. BL-1277: `Surl.TlsSrp`: mutable `TlsSrpAccounts` with reset, snapshot and restore.
5. BL-1278: `Surl.Extended.Protocol.Coap`: the judge and `Account` on the command point.
6. BL-1279: `Surl.Composition`: the live PSK lookup, the held proxy book and TLS-SRP accounts, `ComposedStores` members.
7. BL-1280: `Surl.Api`; BL-1281: `Surl.Mcp.UnitLibrary`; BL-1282: `Surl.Control.UnitLibrary`.
