# ADR-0132 — How the account book changes while the server runs

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-802.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 3 (seed, reset, snapshot and
  restore; accounts as a store), ADR-0032 section 8 (constant-time checks and the refusal delay), ADR-0102
  decision 4.3 (per-scheme views), [ADR-0128](ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md).
- **Acted on by:** BL-802; BL-805 exposes it through `ComposedStores`, BL-810 through `Surl.Api`.
- **Amended by:** [Amendment 5 below](#amendment-5-2026-11-27-bl-1854-removing-the-last-account-does-not-open-the-server) (a scheme that has had an account to log in to keeps asking for a login when the last one is removed, so no read opens; a reset or restore returns it with the accounts; the warning says so), [Amendment 4 below](#amendment-4-2026-11-27-bl-1839-removing-the-last-proxy-account-does-not-open-the-proxy) (removing the last proxy account leaves the proxy asking every client for a login, a `GET` or `HEAD` included, and the warning says so), [Amendment 3 below](#amendment-3-2026-11-27-bl-1840-what-a-snapshot-holds-of-each-password) (decision 5: a snapshot of a named account holds the password's bytes among its derived credentials, so "no clear password" is withdrawn), [Amendment 2 below](#amendment-2-2026-11-22-bl-1601-the-bearer-token-account-is-added-rotated-and-removed-on-every-host) (the empty-name Bearer-token account is added, rotated and removed on every host; the control endpoint spells rotate and remove `/v1/bearer-token`), [Amendment 1 below](#amendment-1-2026-11-20-bl-1482-one-rule-for-adding-a-name-that-already-has-an-account) (adding a name that has an account is refused on every host; a password change is its own operation), [ADR-0170](ADR-0170-how-the-coaps-pre-shared-keys-the-proxy-accounts-and-the-tls-srp-accounts-are-judged-published-and-controlled.md) (the account book is not the only account source: proxy and TLS-SRP accounts are their own stores, and `coaps` reads this book live).

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
   bound and is not counted here. *Corrected by
   [Amendment 3 below](#amendment-3-2026-11-27-bl-1840-what-a-snapshot-holds-of-each-password): the
   heading is wrong, since some derived credentials are the password's bytes. Only the
   `Account.Password` string is left out, and more methods than the three named here read those bytes or other
   derived credentials.*
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
   account's scheme restriction and swaps the set whole (decision 1). It publishes `AccountRemoved` then
   `AccountAdded` for the user, as the control endpoint's replace did, and no event holds a password (decision 6).
   It is no removal to the mail store: `ComposedStores.SetAccountPassword` changes only the account book, so the
   account's mail owner and shared folders stay as they are
   ([ADR-0185](ADR-0185-the-mail-store-reaches-an-account-added-while-the-server-runs-and-retires-one-removed.md)
   decision 2). Open sessions run on (decision 8). A name with no account is refused and nothing is published.
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
- **ADR-0185 decision 2**: its sentence naming "`ComposedControlTarget`'s replace-by-remove-then-add" as the
  password change is replaced. The decision says a password change retires nothing and leaves the mail owner as it
  is.
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

## Amendment 3 (2026-11-27, BL-1840): what a snapshot holds of each password

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-27, in BL-1840.
- **Why now:** decision 5 is headed "Snapshots hold no clear password" and ADR-0124 decision 3 said "password
  hashes only, never a clear password". Both are false of `AccountStore`: it keeps each named account's password
  as its UTF-8 bytes, and a snapshot holds them.
- **Upstream curl** has no say: no byte Surl sends or expects changes.

### What is held

This is true of every `AccountBook`, the proxy accounts' book (ADR-0170 decision 2) included, and of every
`AccountSnapshot` taken of one. The TLS-SRP accounts are another store (ADR-0170 decision 3), not covered here.

Each account is held as its user name, the schemes it may log in on, and the SHA-256 of its password's UTF-8
bytes. **The empty-name Bearer-token account is held with that SHA-256 only**: nothing else is derived from the
token. A named account also holds every other row below, the Azure Storage key only when its password gives one,
each derived once when the account is configured, added or given a new password, whatever `--auth` accepts and
whatever servers run.

| Held | Whose | Gives the password back? | Read by |
| --- | --- | --- | --- |
| SHA-256 of the password's UTF-8 bytes | every account | No. One unsalted hash, so it can be guessed offline | the logins that send the password: HTTP Basic, SASL `PLAIN` and `LOGIN`, SSH `password` and `keyboard-interactive`, and the other password logins a server hands to the policy; for the empty name, the logins that send the token: HTTP and Azure Storage Bearer, SASL `OAUTHBEARER` and `XOAUTH2`, and a server's token login |
| The password's UTF-8 bytes: one array, held as `AwsSigV4Account.Secret` and `ChallengeResponseAccount.Password` | named | **Yes. It is the password** | `AwsSigV4Account.Secret`: AWS Signature Version 4 (header, presigned URL and S3 signed chunks), Cloud Storage's V4 HMAC, and the `coaps` pre-shared key (`AccountBook.TryFindPreSharedKey`). `ChallengeResponseAccount.Password`: SASL `CRAM-MD5` and `DIGEST-MD5`, and POP3 `APOP` |
| The bytes the password decodes to as base64 (`AzureStorageAccount.Key`) | named, when the password reads as base64 of at least one byte, meant as a key or not | **Yes.** Encoding them again gives the password, but for how its base64 was spelt | Azure Storage Shared Key, Shared Key Lite and shared access signatures |
| Four unsalted NT hashes (`NtlmPasswordHashes`, ADR-0075) | named | No, but each is a password equivalent for NTLM and can be guessed offline | NTLM wherever it is carried (HTTP NTLM and Negotiate, SASL `NTLM` and `GSS-SPNEGO`), and SMB's NTLMv1, which reads one of the four |
| Digest user hashes, `H(name:surl:password)` | named | No, but each is a password equivalent for Digest in the realm `surl` | HTTP Digest authentication |
| SCRAM salt, iteration count, `StoredKey` and `ServerKey` under SHA-1, SHA-256 and SHA-512 (ADR-0125) | named | No. Salted and iterated | SASL `SCRAM-SHA-1`, `SCRAM-SHA-256` and `SCRAM-SHA-512` |

The logins that need only a name (SASL `EXTERNAL`, Kerberos through `GSSAPI` or Negotiate) read no row of the
table beyond asking whether the account exists.

### The decision

1. **The code stands and the promise is corrected.** A snapshot of a named account holds the password's SHA-256
   and its derived credentials, some of which are the password's bytes. Treat a snapshot, and the running book,
   as holding every named account's password. The one thing neither keeps is the `Account.Password` string.
2. **Why the bytes are kept.** Their readers cannot work from a hash: Signature Version 4 derives each signing
   key from the secret itself (ADR-0043 decision 3), `CRAM-MD5`, `DIGEST-MD5` and `APOP` compute over the password
   (ADR-0049 section 5), a pre-shared key is the secret (ADR-0170 decision 5), and Azure Storage signs with the
   decoded key (ADR-0109 decisions 3.2 and 3.3). Leaving them out of the book would remove those logins; leaving
   them out of a snapshot would make a restored account fail them, against decision 5's "logs in by every method".
3. **A snapshot shares the book's records.** It holds the same unchanging per-account records the book held when it
   was taken, not copies of their bytes; no record is changed in place, so it behaves as the copy ADR-0124
   decision 3 describes. The book keeps the configured accounts' records for as long as it lives, so that reset can
   return to them, and a snapshot keeps the records of an account removed or given a new password since for as long
   as the snapshot is referenced. Nothing overwrites the bytes with zeros.
4. **A snapshot is in memory and opaque.** `AccountSnapshot`'s one public member of its own is `UserNames`.
   Nothing serialises a snapshot or writes it to disk. `ComposedSnapshot` shows which stores it holds and a byte
   count, to which accounts add 0, and `SurlSnapshot` shows the byte count. The control endpoint and `surl-mcp`
   keep snapshots and answer only an ID (`p1`, `p2`, ...). Restore is what reads one back.
5. **No event or log line is made from what is held.** A change to the accounts publishes a `StoreChangedEvent`
   naming at most a user name (decision 6), and a login note names the method and the user (ADR-0032 section 8).
   What a client sends is a separate matter: `ExchangeBytesEvent` carries the raw bytes of an exchange, a password
   sent in clear among them (ADR-0182 decision 4), while the log withholds it.
6. **The running book is less closed than a snapshot, and that stays.** `AccountBook.TryFindPreSharedKey` is public
   and returns a copy of the password bytes of a named account that may log in on the scheme the caller names, so
   in-process code holding the book can read them, a test's own protocol server among it
   (`SurlProtocolServices.AuthenticationPolicy.Settings.Accounts`). In the production code only the `coaps` key
   lookup calls it, and neither the control endpoint nor `surl-mcp` has a request or tool that returns it. Such
   code runs inside the process that holds the bytes, so withholding the method would protect nothing from it.

This promises nothing about the rest of the process: whoever gave the book an `Account` may still hold its password
(`SurlServerOptions.Accounts`, for one).

### Options weighed

- **Keep the code and say what it holds.** Chosen: every login method stays, and a reader knows how far to trust a
  snapshot.
- **Drop the bytes from a snapshot or from the book.** Rejected: decision 2 above.
- **Encrypt the bytes in memory.** Rejected: the key would sit in the same process, so a reader of its memory gets
  both, at the cost of code on every login.

### Documents this amendment changes (edited by BL-1840)

ADR-0124 decision 3, ADR-0036 (context, decision 7 and its "Keeping the clear password for Digest" alternative),
ADR-0158's directory snapshot, ADR-0170 decision 12 and the index rows of ADR-0124 and ADR-0132, each with a
pointer here. The XML documents, the library's `CLAUDE.md` and `Surl.Api`'s README are BL-1841's to correct.

## Amendment 4 (2026-11-27, BL-1839): removing the last proxy account does not open the proxy

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-27, in BL-1839.
- **Why now:** `Surl.Composition`, `Surl.Api` and `surl-mcp` say a proxy with its last account removed "now needs no
  login" (found by BL-1816). That is true of part of the proxy only, and the part it is true of is a defect.
- **Status of the rule:** it is the rule the code is to follow. BL-1850 to BL-1853 build it; until they are done the
  code does what the next section says.

### What the source does (read on 2026-11-27, not run)

- A proxy given `-U` or `--proxy-user-file` is composed with an `AuthenticationPolicy` over the proxy accounts
  (`AuthenticationComposition.ComposeProxyPolicy`). Removing the last account leaves that policy over an empty book.
  An empty `--proxy-user-file` at start composes the same thing
  ([ADR-0165](ADR-0165-how-tls-srp-and-socks5-gss-api-logins-publish-login-judged-events-and-honour-refuse-login.md)
  amendment 1).
- SOCKS stays closed: a client naming no account is refused, and every login is checked against the empty book.
- The HTTP proxy opens for reads. `HttpAuthenticationSession.JudgeWithoutCredentials` challenges a request with no
  credentials only when there is an account to log in to or the request is a write, and the proxy calls every method
  but `GET` and `HEAD` a write. So with the book empty a `GET` or `HEAD` with no `Proxy-Authorization` is forwarded,
  to an `http://` or `ftp://` origin, while a `CONNECT` is answered `407`.
- So the warning is false of SOCKS, `CONNECT` and writes. It is true of the one case secure by default forbids:
  removing accounts, which a test does to take access away, gives read access to everybody. That also breaks
  [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md) decision 5: a proxy with
  accounts answers every request and every `CONNECT` without valid credentials with the `407`.

### The decision

1. **A proxy composed with proxy accounts keeps asking for a login when none is left.** Removing the last proxy
   account, or starting with an empty `--proxy-user-file`, never lets in a client that had to name an account.
2. **What each request gets** after the last proxy account is removed, without `--allow-anonymous`. It holds for the
   plain proxy and the HTTPS proxy, over HTTP/1.x and HTTP/2. "No `Proxy-Authorization`" includes one of a method the
   proxy does not accept, the Basic row apart:

   | Request | Answer | Against today |
   | --- | --- | --- |
   | HTTP proxy `GET` or `HEAD` (`http://` or `ftp://`), no `Proxy-Authorization` | `407 Proxy Authentication Required` with the offered challenges; no origin is connected | **changes:** forwarded today |
   | HTTP proxy `CONNECT`, no `Proxy-Authorization` | `407` with the offered challenges; no tunnel | same |
   | any other HTTP proxy method, no `Proxy-Authorization` | `407` with the offered challenges | same |
   | any HTTP proxy request with a `Proxy-Authorization` of an accepted method | never let in: `407`, after the refusal delay once the credentials are checked (an NTLM or Negotiate handshake's earlier legs are `407` as always) | same |
   | Basic over an unencrypted connection without `--allow-plaintext-auth` | `403`, unchecked | same |
   | a request that would get a `407` when no accepted method can be offered (`--proxy-auth basic`, unencrypted) | `403` | same, but for the `GET` and `HEAD` above |
   | SOCKS4 or SOCKS4a `CONNECT` | `5B` (rejected) | same |
   | SOCKS5 method `00` (no authentication) | never selected; a greeting offering it and no method selected below gets `05 FF` | same |
   | SOCKS5 method `01` (GSS-API), listener with `--keytab` | `05 01`, chosen before `02`; then every token `01 FF` (ADR-0165 amendment 1) | same |
   | SOCKS5 method `01`, listener without `--keytab` | never selected | same |
   | SOCKS5 method `02` (user name and password), when `01` is not selected | `05 02`, then every login `01 01` (after the refusal delay once checked; unchecked over an unencrypted connection without `--allow-plaintext-auth`) | same |
   | any other SOCKS5 method | never selected (`05 FF` when nothing else is) | same |

3. **`--allow-anonymous` is unchanged.** With it the proxy let everybody in before the removal and still does:
   requests are forwarded and tunnelled unchecked, SOCKS4 is granted, SOCKS5 accepts method `00` and any `02` login,
   and a ticket the keytab's keys accept needs no account.
4. **Open connections run on** (decision 8): a tunnel or SOCKS relay already set up stays up, and an HTTP connection
   NTLM or Negotiate logged in keeps that login, so its later requests are not challenged.
5. **Getting in again.** An added account, a reset or a restore counts from the next login on, and admits only the
   accounts it holds. A running proxy composed with accounts has no way to become anonymous, the mirror of ADR-0170
   decision 2. A test that wants an anonymous proxy starts one with no proxy account, which keeps today's behaviour
   (`NoAccountProxyAuthenticationPolicy`).
6. **The warning.** Written when it is written today: on a removal that leaves no proxy account.

   | Host | Text |
   | --- | --- |
   | control endpoint (the line goes to the log; the `DELETE` still answers `204`) | `surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through` |
   | `Surl.Api` (`SurlServerOptions.Log`; `Remove` still returns `true`) | the same line |
   | `surl-mcp` (`remove_proxy_account`: the `warning` member and the second text block) | `server <id> has no proxy account left; without --allow-anonymous its proxy now lets no new client through` |

   One text is true with and without `--allow-anonymous`, so no host has to learn whether it was given, and the
   result shapes do not change. In the `remove_proxy_account` description, the sentence about the last account
   becomes: "Removing the last proxy account does not open the proxy: without --allow-anonymous it then lets no new
   client through; the result then carries a warning, and add_proxy_account lets that account in." The documents of
   `HasAccounts` and `Remove` say the same in place of "needs no login".
7. **The seam.** `AuthenticationSettings` gains an init property `RequiresLoginWithoutAccounts`, `false` by default.
   When it is `true`, a request with no credentials is challenged even with no account to log in to and even when it
   is not a write; `--allow-anonymous` and a connection's NTLM or Negotiate login still come first.
   `ComposeProxyPolicy` sets it on the policy it builds over proxy accounts. `IsWrite` keeps its meaning, and
   servers that are not proxies keep today's behaviour.
8. **Upstream curl.** No new byte: the `407` and the SOCKS refusals are the ones already sent. ADR-0088 amendment 3
   measured pinned upstream curl 8.21.0 against the proxy's `407`: a forwarded `GET` without credentials ends with
   exit 0 and the `407`'s body printed, a `CONNECT` with exit 7, `CONNECT tunnel failed, response 407`.

### Options weighed

- **Keep the proxy closed and challenge reads too.** Chosen: secure by default, one rule for every request and both
  proxy protocols, and ADR-0088 decision 5 holds again.
- **Keep today's behaviour and correct only the warning.** Rejected: the proxy would fetch any allowed origin for
  anybody, unasked, because a test revoked access.
- **Open the whole proxy, `CONNECT` and SOCKS included, as the warning says.** Rejected for the same reason, more
  widely.
- **Refuse to remove the last proxy account.** Rejected: a test needs the state in which every account is revoked,
  and ADR-0132 already lets a test reach it.
- **Have the proxy call every method a write.** Rejected: `IsWrite` would then say something false.

### How a test uses it

Start a proxy with `-U`, make an exchange, remove the account through `Surl.Api`, `surl-mcp` or the control endpoint,
and the next exchange is refused, as a revoked account is; the log carries the warning. Adding the account again, or a
reset, lets it back in.

### Consequences

1. BL-1850: `Surl.Authentication`: `AuthenticationSettings.RequiresLoginWithoutAccounts` and
   `HttpAuthenticationSession`.
2. BL-1851: `Surl.Composition`: `ComposeProxyPolicy` sets it, and `ComposedControlTarget` writes the new line.
3. BL-1852: `Surl.Api`: the line, the XML documents, the README and the library's `CLAUDE.md`.
4. BL-1853: `Surl.Mcp.UnitLibrary`: the warning, the tool description and the library's documents.
5. BL-1854 decides the same question for the server accounts, whose warning ("the server now needs no login") this
   amendment does not change. [Amendment 5 below](#amendment-5-2026-11-27-bl-1854-removing-the-last-account-does-not-open-the-server)
   is that decision.

## Amendment 5 (2026-11-27, BL-1854): removing the last account does not open the server

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-27, in BL-1854.
- **Why now:** the control endpoint, `Surl.Api` and `surl-mcp` say a server with its last account removed "now needs
  no login" (found by BL-1839). That is true of some reads only, and letting those reads through is a defect.
- **Status of the rule:** it is the rule the code is to follow. BL-1863 to BL-1859 build it. Until then the code does
  what the next section and each "today" entry below say.
- **Supersedes:** the sentence of amendment 4 decision 7 that servers which are not proxies keep today's behaviour,
  and the "Removal" and "Alternative rejected" bullets of
  [ADR-0097](ADR-0097-how-the-coap-server-answers-coap-and-coaps.md) amendment 3.

### What the source does (read on 2026-11-27, not run)

- The policy stays an `AuthenticationPolicy` over the now empty account book. Every login checked against the book is
  refused; with `--user-ldap` the directory may still accept a password login.
- `HttpAuthenticationSession.JudgeWithoutCredentials` challenges a request with no credentials only when it is a write
  or `AuthenticationSettings.HasAccountToLogInTo` finds an account in the server's scheme view, or a directory serving
  the scheme. `CoapAccessRules` asks the same about `coap` before a read. So the reads an account protected are served
  to anybody over `http`, `https`, `ws`, `wss`, `rtsp`, `gs`, DNS over HTTPS, `coap` and `coaps`, all but those in a
  share with `--share-access` rows, which stays closed to anonymous callers (ADR-0102).
- It is asked of a scheme view, so removing the last account allowed on a scheme opens that scheme's reads while
  accounts restricted to other schemes remain, and no warning is written: the warnings look at the whole book.
- On the removal that empties the book, the control endpoint and `Surl.Api` write
  `surl: warning: no account left; the server now needs no login`, or, with `--user-ldap`,
  `surl: warning: no local account left; logins are now checked against the directory only`, which is not so on a
  scheme the directory does not serve. `surl-mcp` writes `server <id> has no account left and now needs no login`,
  with `--user-ldap` too.
- ADR-0097 amendment 3 says CoAP opens "as ADR-0132 decides for the other servers". ADR-0132 had decided no such
  thing before this amendment.

### The decision

1. **A scheme that has had an account to log in to keeps asking for a login when none is left.** It has had one when
   an account allowed on it was configured, or was added while the server ran. This is judged per scheme, whether or
   not accounts for other schemes remain. Neither a removal nor a password change takes it away.
2. **Reset and restore return it with the accounts.** A reset returns to what the configured accounts give; a restore
   returns to what held when the snapshot was taken. So a scheme with an account at start never serves an anonymous
   read for want of an account again. A scheme with none at start gets its anonymous reads back by a reset, or by
   restoring a snapshot taken before the first account was added, with no warning, since that is the state the test
   asked for. What a start gives is unchanged: with no account, an empty `--user-file` included, reads need no login
   (ADR-0032 section 4). A proxy is amendment 4's: an empty `--proxy-user-file` keeps it closed.
3. **Requests with no credentials**, without `--allow-anonymous`, on a scheme that has had an account and that no
   `--user-ldap` directory serves, after its last account is removed. "`401` or `403`" is `401` with the offered
   challenges, or `403` when no accepted method can be offered on the connection.

   | Request with no credentials | Today | After BL-1863 |
   | --- | --- | --- |
   | HTTP `GET` or `HEAD` (`http`, `https`; HTTP/1.x, HTTP/2, h2c and HTTP/3) | served as an anonymous read | `401` or `403` |
   | HTTP write: every other method, known or not, a `PUT` under `--allow-uploads` included ([ADR-0180](ADR-0180-an-http-server-judges-the-login-before-the-method.md)) | `401` or `403` | same |
   | WebSocket upgrade (`ws`, `wss`) | accepted | `401` or `403` |
   | RTSP request but `ANNOUNCE`, `RECORD` and a `SETUP` to record | served | `401` or `403` |
   | RTSP `ANNOUNCE`, `RECORD` or a `SETUP` to record | `401` or `403` | same |
   | `gs` `GET` or `HEAD` | served as an anonymous read | `401` `AuthenticationRequired` |
   | `gs` write | `401` `AuthenticationRequired` | same |
   | DNS over HTTPS query | answered | `401` or `403` |
   | `mcp` or `mcps` `GET` of the MCP endpoint, which serves no `GET` | `405` | `401` |
   | `coap` or `coaps` `GET`, `/.well-known/core` included, from an unauthenticated peer | served | `4.01` |
   | `coap` or `coaps` write from an unauthenticated peer | `4.01` | same |

   A connection NTLM or Negotiate logged in keeps being served as that account (decision 8). A `coaps` peer with a
   certificate verified against `--cacert` stays authenticated, reads and writes, since no account makes it so.
4. **Every other login and request**, without `--allow-anonymous` or a directory, after the last account is removed.
   BL-1863 changes none of these answers but the one marked.

   | Login or request | Answer after the removal |
   | --- | --- |
   | No login where one is needed: FTP, SSH `none`, SMTP `MAIL`, an IMAP mailbox command, a POP3 maildrop command, SMB, an LDAP search but the root DSE's, MQTT, AMQP, XMPP, STOMP, NATS, Kafka, Redis, Socket.IO, a gRPC call, SignalR, WebTransport `CONNECT`, `oci` `/v2/`, an MCP `POST` or `DELETE`, S3, Azure Storage | refused, as before the removal |
   | Password: FTP (curl's anonymous login included), IMAP `LOGIN`, POP3 `USER`/`PASS`, MQTT, LDAP simple bind, Redis `AUTH`, STOMP, NATS, Socket.IO, `oci` `/token`, the MCP authorization endpoints, HTTP Basic, SASL `PLAIN` and `LOGIN`, SSH `password` and `keyboard-interactive` | refused as a wrong password is. A clear password the connection may not carry is refused unchecked, as before |
   | Token: HTTP, Azure Storage and `gs` Bearer, SASL `OAUTHBEARER` and `XOAUTH2`, a server's token login | refused |
   | Challenge and response: HTTP Digest, NTLM and Negotiate carrying NTLM; SASL `CRAM-MD5`, `DIGEST-MD5`, `NTLM`, `GSS-SPNEGO` and `SCRAM`; `APOP`; SMB NTLMv1 | the exchange may run; the answer is refused |
   | An identity that must name an account: a Kerberos ticket (Negotiate, SASL `GSSAPI` and `GSS-SPNEGO`), SASL `EXTERNAL` | refused: no account has the name |
   | Signature: AWS Signature Version 4 (header, presigned URL, S3 chunks), Azure Storage Shared Key and shared access signature, `gs` V4 HMAC | refused: no such account |
   | `coaps` pre-shared key | the handshake fails: unknown identity |
   | An HTTP read carrying credentials of a method `--auth` does not accept, which counts as none | **changes with BL-1863:** served today, `401` or `403` after |
   | A token issued before: an `oci` token, an MCP access or refresh token | refused: its account is gone |
   | SSH `publickey` with a key `--authorized-keys` gives the user name | **still accepted** (decision 9): it logs in under that name, and a share granted to the name still admits it. A key is configuration, not an account |
   | A session already logged in, a DTLS session included | runs on (decision 8) |
   | TFTP, Gopher, DICT, TELNET, DNS but over HTTPS, `mcp+stdio:` | no login before or after; a share with `--share-access` rows stays closed to them (ADR-0102) |

   The proxy accounts (amendment 4) and the TLS-SRP accounts are other stores, which this removal does not touch.
5. **`--allow-anonymous` is unchanged.** Before and after the removal every request without credentials proceeds and
   logins are accepted unchecked. What is checked even under it refuses the removed account: an S3 signature, Azure
   Storage credentials, a `gs` V4 HMAC signature, a `coaps` pre-shared key, and an LDAP bind that sets up a security
   layer.
6. **`--user-ldap` is unchanged on the schemes the directory serves:** reads already need a login there, and a
   password login (HTTP Basic, SASL `PLAIN` and `LOGIN`, SSH password and the servers' password logins) naming a user
   the book does not hold is checked against the directory (ADR-0149 decision 6). The removed name is now such a name,
   so where the directory holds it, the directory's password logs in. That is kept: the operator named the directory.
   Every other login is answered as decision 4's table says. A scheme the directory does not serve behaves as
   decisions 3 and 4 say.
7. **How the instance then differs from one started with no account.** Its reads stay closed on the schemes that had
   an account (decision 1). A `coaps` listener started on pre-shared keys alone, with no certificate (which a start
   given neither `--user` nor `--user-file` refuses), completes no handshake. The mail store has retired the mail
   owner it reached under a removed account's name, keeping its mail
   ([ADR-0185](ADR-0185-the-mail-store-reaches-an-account-added-while-the-server-runs-and-retires-one-removed.md)
   decision 2). It retires nothing for a `--mailbox` account, whose address stays an owner and a recipient, nor under
   `--allow-anonymous`, nor for the Bearer token's empty name, nor for an account added while the server ran that may
   log in on no mail scheme (ADR-0185 decision 6). What a view of a removed name holds is ADR-0185 decision 2 and its
   Amendment 1. Upload permission comes from `--allow-uploads` alone and never from whether accounts exist.
8. **The warning.** Written on the condition that writes one today, a removal that leaves the book with no account,
   and never by a reset, a restore or a password change. A scheme losing its last account while others remain writes
   none, since nothing opens.

   | Host | Text (after BL-1865 to BL-1859) |
   | --- | --- |
   | control endpoint (the line goes to the log; the `DELETE` still answers `204`) | `surl: warning: no local account left; the server still asks for a login wherever it did` |
   | `Surl.Api` (`SurlServerOptions.Log`; `Remove` still returns `true`) | the same line |
   | `surl-mcp` (`remove_account`: the `warning` member and the second text block) | `server <id> has no local account left; it still asks for a login wherever it did` |

   One text is true with and without `--user-ldap` and `--allow-anonymous`, so it replaces the directory line too, and
   `surl-mcp` need not learn whether a directory was given. In the `remove_account` description, the sentence about
   the last account becomes: "Removing the last account does not open the server: it still asks for a login wherever
   it did; the result then carries a warning, and add_account lets that account in." The documents of `HasAccounts`
   say that `false` means no account is held, not that the server needs no login.
9. **The seam.** The account book remembers which schemes the accounts it has held may log in on, keeping no
   credential of a removed account for it, and an `AccountSnapshot` records it. `HasAccountToLogInTo` is renamed
   `IsLoginRequiredToRead`: `true` when the book or view has held such an account, or the directory serves the scheme.
   `HttpAuthenticationSession` and the CoAP rules ask it, and `CoapAccessRules.HasAccounts` takes the same name
   (BL-1864). `AccountBook.HasAccounts` keeps its meaning, an account is held now. `RequiresLoginWithoutAccounts`
   (amendment 4) is still what keeps a proxy given an empty `--proxy-user-file` closed.
10. **Upstream curl.** No new byte: the `401`, `403` and `4.01` are the ones a server with accounts already sends.

### Options weighed

- **Keep a scheme closed once it has had an account.** Chosen: removing an account, which a test does to take access
  away, never gives access to anybody, and one rule covers every server.
- **Keep today's behaviour and correct only the warning.** Rejected: content would be served to anybody, unasked,
  because a test revoked access.
- **Close only the schemes that had an account at start** (amendment 4's switch, set at composition). Rejected:
  removing an account a test added would still open reads to everybody it had just kept out.
- **Never return to anonymous reads, not even by reset.** Rejected: a reset returns to the configured state (ADR-0124
  decision 3). With decision 2 this also answers ADR-0097 amendment 3's two objections: every server changes
  together, and a reset gives the starting behaviour back.
- **Refuse to remove the last account.** Rejected: a test needs the state in which every account is revoked.

### How a test uses it

Start with an account, make an exchange, remove the account through `Surl.Api`, `surl-mcp` or the control endpoint,
and the next exchange is refused; the log carries the warning. Adding the account again, or a reset, lets it back in.
A test that starts with no account, adds one and removes it stays refused until it resets. A server with `--directory`
refuses a whole-instance reset, the only reset the three hosts offer, so there the scheme stays closed until a
restart. A test that wants anonymous reads throughout adds no account, or gives `--allow-anonymous`.

### Consequences

1. BL-1863: `Surl.Authentication`: the book's memory, the snapshot's, `IsLoginRequiredToRead` and its one call in
   `Surl.Composition`.
2. BL-1864: `Surl.Extended.Protocol.Coap`: the rename of `CoapAccessRules.HasAccounts`.
3. BL-1865: `Surl.Composition`: the control endpoint's line and the documents of `ComposedStores.HasAccounts`.
4. BL-1866: `Surl.Api`: the line, the XML documents, the README and the library's `CLAUDE.md`.
5. BL-1859: `Surl.Mcp.UnitLibrary`: the warning, the tool description and the library's documents.
6. BL-1860: the `--aihelp` and manual text. BL-1861 and BL-1862: pointers from the decisions that open reads with "no
   account configured", and the product documents, wiki, root README and DNS comments that say so.
