# ADR-0165 — How TLS-SRP and SOCKS5 GSS-API logins publish LoginJudgedEvent and honour refuse-login

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1214.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1 (`LoginJudgedEvent`) and 4
  (`refuse-login`, and "no fault alters the handshake itself"), [ADR-0128](ADR-0128-how-surl-authentication-publishes-judged-logins-and-applies-refuse-login.md)
  (the policy's publishing and refusal), [ADR-0122](ADR-0122-surl-serves-tls-srp-with-a-hand-built-tls-1-2-server.md) (TLS-SRP) and ADR-0121
  amendment 2 (SOCKS5 GSS-API, BL-765).
- **Amends:** ADR-0124 decisions 1 and 4.
- **Acted on by:** the tasks listed under Consequences.

## Context

ADR-0124 says every judged login is published and a `refuse-login` rule makes a login fail as a wrong password
does. Two logins are judged outside `AuthenticationPolicy` and do neither:

- **TLS-SRP.** `Surl.TlsSrp`'s `TlsSrpServer` finds the account in `TlsSrpAccounts`, and the client's `Finished`
  fails its MAC (`bad_record_mac` on the wire) for a wrong password or an unknown user. It only throws a
  `TlsAlertException` whose message the verbose log shows. `Surl.TlsSrp` references only the SRP primitives, and
  `Surl.Networking` runs it inside the listener, before any `ExchangeContext` exists.
- **SOCKS5 GSS-API.** `Socks5GssApiNegotiation` calls `KerberosAcceptor.Accept` directly. `IAuthenticationPolicy`
  has no Kerberos member.

## Decision

### TLS-SRP

1. **Published.** Yes. A TLS-SRP login is *checked* once the client's `Finished` has been read, so each handshake that
   reaches that point publishes one `LoginJudgedEvent`: `Mechanism` `tls-srp`, `Account` the `srp` user name as the
   client sent it (lossy UTF-8 only for the event; the account lookup still uses the exact bytes), `Scheme` and
   `ExchangeId` those of the exchange the connection belongs to, `IsAccepted` true when the `Finished` verified.
   An unknown user and a wrong password are both published as refused: the event stream is the test's side channel
   and the wire already hides the difference, so the stream may show it only through the same `Account`. No
   password, verifier, salt or key is in the event. A handshake that fails before the `Finished` (no suite, no
   `srp` extension, a malformed message, a peer alert) was never judged and publishes nothing, as an unchecked
   login does in ADR-0128 decision 2.
2. **Who publishes.** Not `Surl.TlsSrp` and not `Surl.Networking`, which hold no event publisher or fault plan. The
   exchange's engine does. Three small contracts carry it:
   - `Surl.TlsSrp` defines `ITlsSrpLoginJudge` (`ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken)` and
     `void Judged(string userName, bool isAccepted)`) and `TlsSrpServer` takes it as an optional last constructor parameter
     (default: none, behaviour unchanged). It stays free of every Surl project.
   - `Surl.Protocol.Abstractions` defines `ITlsLoginJudge` with the same two members, and `IConnection` gains a default
     member `UseTlsLoginJudge(ITlsLoginJudge)` that does nothing. `StreamConnection` keeps the judge and
     `ServerTlsHandshake.AuthenticateAsync` hands it to `TlsSrpServer` through an adapter. Every other `IConnection` keeps the default.
   - The engine (`Surl.Core`) hands each connection an `ITlsLoginJudge` made from the exchange's `ExchangeContext`
     before the handshake (implicit TLS) or the upgrade, which publishes the events and asks the fault plan.
3. **Refuse-login applies, and is the one fault applied inside a handshake.** ADR-0124 decision 4's "no fault alters
   the handshake itself" is amended: `refuse-login` acts at `FaultStage.Login` inside the TLS-SRP handshake because a
   refused login *is* a handshake that fails, and it fails exactly as a wrong password fails, so no new
   byte exists. `delay`, `drop` and `error` are still never applied to a handshake.
4. **When it is asked.** Only once the `Finished` has verified (credentials matched), with
   `FaultPoint(FaultStage.Login) { ExchangeId, ListenUrl, Scheme, Account = <user name> }`, as ADR-0128 decision 4 says, so
   a wrong login never spends a rule's `times`. A rule of any kind but `RefuseLogin` is not applied. A matching
   `refuse-login` rule makes `TlsSrpServer` fail as the `Finished` MAC fails: the same plaintext fatal alert
   `bad_record_mac` (20), no `ChangeCipherSpec` and no server `Finished` sent, and a `TlsAlertException` whose message
   is `The TLS-SRP login failed: login refused for '<name>'.` for the verbose log.
5. **Order of events.** A fault-refused login publishes `FaultAppliedEvent(rule.Id, RefuseLogin, Login, ExchangeId, <account>)` and then
   `LoginJudgedEvent` with `IsAccepted` false and `IsRefusedByFault` true. A wrong password publishes
   `LoginJudgedEvent` refused with `IsRefusedByFault` false. The fault never makes a refused login succeed.
6. **Delay.** `TlsSrpServer` never reads a clock (ADR-0122) and a wrong password has no refusal delay today, so
   a refused login has none either. If a delay is ever added, it applies to both alike.

### SOCKS5 GSS-API

7. **Published.** Yes. A token that `KerberosAcceptor.Accept` accepts is an accepted login; a token it refuses is a
   refused one with an empty `Account` (none could be read), as ADR-0128 decision 2 allows. `Mechanism` is `gssapi`,
   `Account` is the client principal (`KerberosSecurityContext.ClientPrincipal`), `Scheme` and `ExchangeId` are the
   exchange's (`socks5` or `socks5h`; the listen URL's own scheme).
8. **How.** The SOCKS server already reaches the policy only through `IAuthenticationPolicy`, so the contract gains a
   default member `ValueTask<bool> JudgeKerberosLoginAsync(KerberosLogin login, CancellationToken)` and a record
   `KerberosLogin(string? ClientPrincipal, bool IsTokenAccepted, string Scheme, LoginExchange? Exchange)`, both in
   `Surl.Protocol.Abstractions`. The default returns `IsTokenAccepted` and publishes nothing, so every other policy and
   test double is unchanged. `AuthenticationPolicy` overrides it: it publishes, and when the token was accepted it asks the fault plan as
   decision 4 does, with `Account` the principal. `Surl.Protocol.Abstractions` references no Kerberos type, which is
   why the record carries strings.
9. **Refuse-login applies.** When the verdict is `false` the server answers as it already answers a refused token: `01 FF`
   and the connection closed, with the log note `SOCKS5 GSS-API login refused: <reason>`. It is called after `Accept`
   succeeds and before the AP-REP is sent, so a refused login never sees the server's mutual-authentication token.
   `SocksProtocolServer` calls it only when it has an `IAuthenticationPolicy`; with none, nothing is published or refused, as
   before. Delay is the policy's `RefusalDelay`, on the injected `TimeProvider`.

### Both

10. **Upstream curl.** No byte Surl sends changes. A refused TLS-SRP login is the `bad_record_mac` that
    pinned upstream curl 8.21.0 received for a wrong password (ADR-0122's recorded fixtures,
    `Record-CurlExchange.ps1 -TlsSrp`), and a refused SOCKS5 GSS-API login is the `01 FF` the server already sends for a
    refused token. No new measurement is needed; the follow-up tasks' tests re-assert those fixtures.
11. **Secrets.** No event, note or fault record carries a password, verifier, key or token.

## Consequences

A test can see who logged in over TLS-SRP and SOCKS5 GSS-API and make chosen accounts fail, as it can on every other login.
Follow-up tasks, in order (contracts first):

1. BL-1221: `Surl.Protocol.Abstractions`: `ITlsLoginJudge`, `IConnection.UseTlsLoginJudge`, `KerberosLogin`, `IAuthenticationPolicy.JudgeKerberosLoginAsync`.
2. BL-1222: `Surl.TlsSrp`: `ITlsSrpLoginJudge` and its calls in `TlsSrpServer`.
3. BL-1223: `Surl.Networking`: `StreamConnection` and `ServerTlsHandshake` carry the judge to `TlsSrpServer`.
4. BL-1224: `Surl.Core`: the engine's `ITlsLoginJudge` (events and fault plan).
5. BL-1225: `Surl.Authentication`: `AuthenticationPolicy.JudgeKerberosLoginAsync`.
6. BL-1226: `Surl.Protocol.Socks`: `Socks5GssApiNegotiation` calls it.
