---
id: BL-1400
title: Bind an XMPP login accepted unchecked under --allow-anonymous to the JID of the name it gave
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1399]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1400 — Bind an XMPP login accepted unchecked under --allow-anonymous to the JID of the name it gave

## Goal

Under `--allow-anonymous`, an XMPP `PLAIN` or SCRAM login that names an authcid (say `alice`) and is accepted
unchecked binds as `alice@<stream domain>`, not as a new `anon-...` JID, so stanzas addressed to `alice@<domain>`
reach it; only `ANONYMOUS` gets an `anon-` name.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 2 (second half): with `--allow-anonymous`, a client logging in
  with `-u alice:s3cret` is bound as `anon-<hex>@127.0.0.1`.
- ADR-0114 decision 3: the bare JID is `<name>@<stream domain>` for the authcid's account name, a `local@domain`
  account only on the stream's domain, and `anon-` plus 16 hex digits "for `ANONYMOUS`". It never says a named
  login gets an `anon-` JID. ADR-0049 says `--allow-anonymous` accepts credentials unchecked; the name the client
  gave is still the name it logs in under.
- Cause: under `--allow-anonymous` the policy answers `SaslLoginOutcome.AcceptedUnchecked` with no account name
  (`Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs:52`, and e.g. `CramMd5SaslExchange.cs:33`),
  and `XmppSaslLogin.SucceedAsync`
  (`Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/XmppSaslLogin.cs:208-211`) passes that `null` to
  `XmppJids.BareJid` (`XmppJids.cs:36-39`), which turns `null` into an `anon-` name. The authcid is already in the
  client's first message, which `CheckFirstMessage` reads for the authzid: `XmppAuthzid.IsAcceptablePlain`
  (`XmppAuthzid.cs:21`; RFC 4616's `authzid NUL authcid NUL passwd`) and `IsAcceptableScram` (`XmppAuthzid.cs:54`;
  RFC 5802's `n=<user>` after the gs2 header, `=2C` and `=3D` unescaped).
- The work: keep the authcid the first message named; when the outcome is `AcceptedUnchecked` and an authcid was
  named, make the JID from it by the same rules (`local@domain` only on the stream's domain; a name that is not an
  RFC 7622 localpart is `not-authorized`). `ANONYMOUS` (BL-1399) and an unchecked login with an empty authcid keep
  the `anon-` name.
- Stand-in: a cross-client test under `--allow-anonymous` (two clients, `alice` and `bob`, sending to each other)
  works without accounts; no store, event or fault change.

## Acceptance criteria

- [ ] A failing test first: an `XmppSaslLogin` test with a policy answering `AcceptedUnchecked` for `PLAIN`
      `\0alice\0anything` sets `BareJid` to `alice@<domain>`.
- [ ] The same for a SCRAM-SHA-256 client-first `n,,n=alice,r=...` accepted unchecked.
- [ ] Tests: an unchecked login named `alice@other.example` on stream domain `example.com`, or named `a b`, is
      `not-authorized`; `ANONYMOUS` and an unchecked `PLAIN` with an empty authcid still get `anon-<16 hex>`.
- [ ] A checked login (`Accepted`, account name from the policy) binds exactly as before (an existing test stays
      green unchanged).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

- Depends on BL-1399 only because both change `XmppSaslLogin`; take it after.
- Update `XmppJids.BareJid`'s doc comment and the library's `CLAUDE.md` bullet ("`anon-` ... for a login accepted
  unchecked") to the new rule. BL-1409 records the reading in ADR-0114.

## Log

- 2026-11-19: Created.
