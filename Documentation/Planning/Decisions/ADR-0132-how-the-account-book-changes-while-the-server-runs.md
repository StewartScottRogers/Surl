# ADR-0132 — How the account book changes while the server runs

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-802.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 3 (seed, reset, snapshot and
  restore; accounts as a store), ADR-0032 section 8 (constant-time checks and the refusal delay), ADR-0102
  decision 4.3 (per-scheme views), [ADR-0128](ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md).
- **Acted on by:** BL-802; BL-805 exposes it through `ComposedStores`, BL-810 through `Surl.Api`.

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
