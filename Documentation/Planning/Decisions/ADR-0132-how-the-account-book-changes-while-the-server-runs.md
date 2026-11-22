# ADR-0132 — How the account book changes while the server runs

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-802.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 3 (seed, reset, snapshot and
  restore; accounts as a store), ADR-0032 section 8 (constant-time checks and the refusal delay), ADR-0102
  decision 4.3 (per-scheme views), [ADR-0128](ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md).
- **Acted on by:** BL-802; BL-805 exposes it through `ComposedStores`, BL-810 through `Surl.Api`.
- **Amended by:** [Amendment 2 below](#amendment-2-2026-11-22-bl-1601-the-bearer-token-account-is-added-rotated-and-removed-on-every-host) (the empty-name Bearer-token account is added, rotated and removed on every host; the control endpoint spells rotate and remove `/v1/bearer-token`), [Amendment 1 below](#amendment-1-2026-11-20-bl-1482-one-rule-for-adding-a-name-that-already-has-an-account)(#amendment-1-2026-11-20-bl-1482-one-rule-for-adding-a-name-that-already-has-an-account) (adding a name that has an account is refused on every host; a password change is its own operation), [ADR-0170](ADR-0170-how-the-coaps-pre-shared-keys-the-proxy-accounts-and-the-tls-srp-accounts-are-judged-published-and-controlled.md) (the account book is not the only account source: proxy and TLS-SRP accounts are their own stores, and `coaps` reads this book live).

## Context

ADR-0124 makes the account set a store a test controls: add and remove an account, reset to the
configured accounts, snapshot and restore, each published as a `StoreChangedEvent`. Before BL-802
`AccountBook` was filled once at start, and `ForScheme` copied the admitted accounts into a separate book,
so a change to the book would never have reached a server composed with a scheme view. BL-802 may change
only `Surl.Authentication`.

## Decision

1. **One changing set, swapped whole.** `AccountBook` reads an internal `AccountStore` that holds an
   unchanging `AccountSet` (every lookup a login makes) and the configured accounts. A change derives what
   it must outside the lock, then builds a new set and swaps it in under the lock. A login reads the
   current set and so sees either the set before a change or the set after it, never a half-written
   dictionary. Secrets are still compared with `CryptographicOperations.FixedTimeEquals`, an unknown name
   still finds the dummies, and the refusal delay is unchanged.
2. **Every view is live.** `AccountBook.ForScheme` always returns a view over the same store, filtered by
   the scheme when read and filtered again only after the store's set changes. `AuthenticationSettings.ForScheme`
   therefore always gives a view. It no longer returns itself when no account is left out, so an account
   restricted to another scheme that is added later is still left out of that scheme's view. A view's
   `Add`, `Remove`, `Reset`, `TakeSnapshot` and `Restore` act on the book it was made from.
3. **Adding derives as at start.** `Add` derives the password SHA-256, Digest user hashes in both
   spellings, the NT hashes, the Signature Version 4 and challenge-response secrets, the Azure Storage key
   and the SCRAM credentials exactly as the constructor does. Adding a name that has an account throws
   `ArgumentException`, and the existing account is kept. `Remove` of a name with no account returns
   `false` and publishes nothing.
4. **What reset returns to.** Reset returns to exactly the accounts the book was built with (`--user`,
   `--user-file`, `--mailbox` and groups, as `Surl.Composition` gives them), as ADR-0124 says.
5. **Snapshots hold no clear password.** An `AccountSnapshot` holds the accounts as the book keeps
   them: the password hashes and the credentials derived from each password. It never holds `Account.Password`.
   A method that needs the secret itself (Signature Version 4, CRAM-MD5, APOP) keeps the derived bytes it
   already kept, so a restored account logs in by every method. The snapshot is in memory and opaque: its
   only public member is `UserNames`. A snapshot of another book is refused with `ArgumentException`.
   `MaxSnapshotBytes` belongs to the whole-instance snapshot (BL-805). The account set is tiny next to that
   bound and is not counted here.
6. **Events.** `AccountAdded` and `AccountRemoved` name the user, and `Reset` and `Restored` carry an empty
   name. `ByteCount` and `ExchangeId` are null. No event holds a password, a hash or a token. Each event is
   published under the change lock, so the events keep the order of the changes. An add by a test is
   published as `AccountAdded`, not `Seeded`: no protocol adds accounts, so every add is a test's seed, and
   the more specific change says so. The publisher is the new public constructor's `ISurlEventPublisher`,
   and the old constructor publishes nowhere.
7. **No warning for an added account.** A test adds an account through `Surl.Api`, `Surl.Mcp` or the
   control endpoint. Each of those is an explicit, already-warned test surface (ADR-0124 decision 7's
   control-endpoint warning names accounts). `Surl.Authentication` has no log, and a second warning per
   account would only repeat that one.
8. **Open sessions run on.** A removed account's connection that is already logged in keeps its login,
   NTLM's connection login included, until the connection ends, as ADR-0124 says.
9. **Authorized keys do not change with the accounts.** `--authorized-keys` keys are configuration, not
   part of the account set. Adding, removing or resetting accounts leaves them as they are.

## Consequences

- `AccountBook.ForScheme` and `AuthenticationSettings.ForScheme` never return the receiver, and a view of
  a view admits only what both schemes admit.
- Composition needs only to build the book with the instance's publisher (BL-803, BL-805).

## Amendment 1 (2026-11-20, BL-1482): one rule for adding a name that already has an account

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-20, in BL-1482.
- **Why now:** the Surly cookbook bug report of 2026-11-20 (item 5) found the three hosts disagreeing. `Accounts.Add`
  threw and `add_account` answered an error (decision 3), but the control endpoint's `POST /v1/accounts`
  removed then added, so it answered `204` and replaced the password.
- **Upstream curl** has no say: this is a server-side test surface only.

### The rule

1. **Adding a name that already has an account is refused, on every host, for all three account kinds** (the account
   book, the proxy accounts and the TLS-SRP accounts of ADR-0170). The existing account is kept and nothing is
   published. Decision 3 stands; the control endpoint now follows it. Why: an add is a seed, and a seed that
   silently overwrites hides a test's typo (two accounts meant to be different, one name); a password change that
   is meant says so by name.
2. **Changing a password is its own operation, never an add.** It exists on every host and for all three kinds:

   | Host | Account book | Proxy account | TLS-SRP account |
   | --- | --- | --- | --- |
   | `Surl.Api` | `Accounts.SetPassword(user, password)` | `ProxyAccounts.SetPassword(user, password)` | `TlsSrpAccounts.SetPassword(user, password)` |
   | `surl-mcp` | `set_account_password` (`server_id`, `user`, `password`) | `set_proxy_account_password` (same) | `set_tls_srp_account_password` (same) |
   | control endpoint | `PUT /v1/accounts/{user}` | `PUT /v1/proxy-accounts/{user}` | `PUT /v1/tls-srp-accounts/{user}` |

   The endpoint's body is JSON `{"password": "<text>"}`, and the `user` in the path names the account.
3. **What a password change does.** It derives everything decision 3 derives from the new password, keeps the
   account's other members (its scheme restriction, mailbox and groups) and swaps the set whole (decision 1).
   It publishes `AccountRemoved` then `AccountAdded` for the user, exactly as the control endpoint's replace did, so
   ADR-0185 decision 2's retire-and-reach-again of the mail owner is unchanged and no event holds a password
   (decision 6). Open sessions run on (decision 8). A name with no account is refused and nothing is published.
   Reset returns to the configured password (decision 4).

### The answers

| Case | `Surl.Api` | `surl-mcp` | Control endpoint |
| --- | --- | --- | --- |
| add, name already has an account | `ArgumentException` (the message names the user, never a password) | `isError: true`, text `user <name> already has an account` (as today) | `409 conflict`, message `user <name> already has an account` |
| add, new name | returns | `{"user"}` (as today) | `204` |
| set password, account exists | returns | `{"user"}` - never the password | `204` |
| set password, no account | `ArgumentException` (the message names the user) | `isError: true`, text `user <name> has no account` | `404 not_found` |
| set password, empty password | as `Add` refuses one today | as `add_account` refuses one today | `400 bad_request` with the reader's text |

The proxy and TLS-SRP kinds answer the same, with `proxy account` and `TLS-SRP account` in the texts where the
existing tools already say so.

### Documents this amendment changes (edited by BL-1490, which adds them to its touches)

- **ADR-0124 decision 7's table**: `POST /v1/accounts` answers `204` or `409 conflict`; the table gains
  `PUT /v1/accounts/{user}` (JSON `password`; `204`, or `404 not_found`) and the same two rows for
  `/v1/proxy-accounts` and `/v1/tls-srp-accounts`. Decision 8's tool table gains the three `set_*_password` tools
  (`server_id`, `user`, `password`; result `{"user"}`). The `409 conflict` error code's list gains "an account
  added twice".
- **ADR-0185 decision 2**: the sentence "`ComposedControlTarget`'s replace-by-remove-then-add (a password change)"
  now reads "a password change (`SetPassword`, which publishes `AccountRemoved` then `AccountAdded`)". The retire
  and reach-again behaviour is unchanged.
- **`IControlTarget`'s member documents** ("or replaces the password of one with the same name") change with the
  alignment work below.

### Consequences

- The control endpoint's `POST` on an existing name changes from `204` to `409`. The endpoint is a test surface
  with one known caller (the Surly cookbook), which wanted the refusal. The configuration surface only grows:
  nothing else changes, and every default keeps today's behaviour.
- A password-rotation test, which the cookbook's batch covers, uses `SetPassword` on every host.
- The alignment work is filed as tasks (see BL-1482's Log), each depending on BL-1482: `Surl.Composition` with
  `Surl.Control` (the target and the endpoint), `Surl.Authentication` and `Surl.Api`, and `Surl.Mcp.UnitLibrary`.

## Amendment 2 (2026-11-22, BL-1601): the Bearer-token account is added, rotated and removed on every host

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-22, in BL-1601.
- **Why now:** the Surly cookbook bug report of 2026-11-22 (item 2, a question) found that with `--user :tok123` a test
  cannot expire or replace the token mid-session: `POST /v1/accounts` with `"user": ""` is `400`, `DELETE /v1/accounts/`
  is `404`, and `add_account` with `"user": ""` fails schema validation. The report was made with the Curl port as
  client, so it is a symptom report and no evidence. `AccountBook` and `Surl.Api` already take the empty name
  (ADR-0032 section 1: "an empty name is a Bearer token"); only the host layers refuse it.
- **Upstream curl** has no say: this is a server-side test surface (`--oauth2-bearer` is the client's side only).

### Options weighed

- **(a)** accept `user: ""` in `POST /v1/accounts` and the three tools, and accept the empty last segment of
  `PUT|DELETE /v1/accounts/`. Smallest change, but a path built from an unset variable (`/v1/accounts/{user}` with
  `user` empty) would then silently rotate or delete the token account: the very accident the `400` and `404` catch
  today. Rejected.
- **(b)** the same for the body and the tools, with dedicated `PUT|DELETE /v1/bearer-token` routes in place of the
  empty segment. **Chosen.** Every operation is spelled on purpose: an empty `user` in a JSON body or tool argument is
  written deliberately, a dedicated route is typed deliberately, and `/v1/accounts/` stays `404`. It only grows the
  surface (configuration as data) and every default keeps today's behaviour.
- **(c)** keep the refusal; a test rotates a token only through `Surl.Api`. Rejected: non-.NET suites and agents
  would have no way to do what the stand-in exists to let them do.

### The rule

1. **`user: ""` names the Bearer-token account** and is accepted by the account book's add, rotate and remove on
   every host. It is the same account `--user :tok` makes, so `Surl.Api` and `Reset` behave as decisions 3 and 4
   say: a token given at start is removed by remove and returns on reset; a token added later is removed by reset.
2. **Adding the token account when none exists makes Bearer logins succeed**, which widens access. It needs no new
   warning: it is a test's explicit act on an already-warned surface (decision 7; ADR-0124 decision 7).
3. **Proxy accounts and TLS-SRP accounts have no token form.** An empty `user` is refused for both kinds on every
   host, as today: `ArgumentException` on `Surl.Api`, a schema failure (`minLength` 1, unchanged) on `surl-mcp`,
   `400 give user and password, neither empty` on the control endpoint. `ProxyAccounts` and `TlsSrpAccounts` have
   no `/v1/bearer-token` counterpart. An empty password stays refused for every kind.
4. **Events.** `AccountAdded` and `AccountRemoved` carry an empty `name`, as for any add and remove (decision 6), a
   rotation publishes `AccountRemoved` then `AccountAdded` (amendment 1.3), and nothing names or holds the token.

### The answers (account book, empty-name account)

| Operation | `Surl.Api` | `surl-mcp` | Control endpoint |
| --- | --- | --- | --- |
| add | `Accounts.Add("", token)` returns; a second add throws `ArgumentException` | `add_account` `{"server_id", "user": "", "password"}` answers `{"user": ""}`; a second add is `isError: true`, `the Bearer-token account already exists` | `POST /v1/accounts` `{"user":"","password":"<token>"}` is `204`, or `409 conflict` `the Bearer-token account already exists` |
| rotate | `Accounts.SetPassword("", token)` returns; no such account throws `ArgumentException` | `set_account_password` with `"user": ""` answers `{"user": ""}`; none is `isError: true`, `the Bearer-token account does not exist` | `PUT /v1/bearer-token` `{"password":"<token>"}` is `204`, or `404 not_found` |
| remove | `Accounts.Remove("")` returns `true`, or `false` when none | `remove_account` with `"user": ""` answers `{"user": ""}`; none is `isError: true`, `server <id> has no Bearer-token account` | `DELETE /v1/bearer-token` is `204`, or `404 not_found` |
| published | `AccountAdded`, `AccountRemoved` then `AccountAdded`, `AccountRemoved`, each with an empty `name` | the same | the same |

The `password` keeps `minLength` 1 on every tool; an empty one is `400 bad_request` on the control endpoint. No
answer, error text or log line holds the token. `PUT /v1/accounts/` and `DELETE /v1/accounts/` (an empty segment)
stay `404`, like `/v1/files/` (ADR-0136 decision 8).

### Documents this amendment changes (edited by BL-1601)

ADR-0136 decisions 7 and 8, ADR-0161's `remove_account` row, and ADR-0124 decision 7's route table and decision 8's
tool table, each with a pointer here. The code follows in BL-1602 (control endpoint) and BL-1603 (`surl-mcp`);
`Surl.Api` needs no change (BL-1604 proves it).
