# ADR-0038 — Checked logins carry the login note, and the server writes it

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-125; recorded in BL-129.
- **Amends:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  section 6's contract, which gains `CheckedLogin`, a fourth `HttpAuthenticationVerdict`
  parameter and `PasswordLoginVerdict.AcceptedUnchecked`. Everything else in ADR-0032 stands.

## Context

ADR-0032 section 8's last bullet asks for a verbose-log note on every login:
`Login accepted: <method> <user>` or `Login refused: <method> <user>`, `<user>` as sent and
escaped by ADR-0007 section 8, `bearer token` for Bearer, and never a password, a token or an
`Authorization` value. Section 6's contract as BL-109 built it could not carry that note:

- `HttpAuthenticationVerdict(Outcome, WwwAuthenticateValues, AccountName)` names the account
  logged in, not the method checked or the user as sent, and says nothing on a refusal.
- `PasswordLoginVerdict` had `Accepted` both for credentials checked against an account and
  for `--allow-anonymous`, which checks nothing.
- `IAuthenticationPolicy` has no `IExchangeLog`, so `Surl.Authentication` cannot write a note
  itself.

Two shapes were open: the verdicts carry what the note needs and each protocol server writes
it through its own exchange log, or the contract hands the exchange log to the policy. ADR-0002
forbids a protocol server to reference `Surl.Authentication`, so whichever is chosen has to be
provable from each protocol server's own tests with a policy double from Abstractions.

The notes go to surl's own stderr log, not to the wire, so no byte upstream curl sends or
receives changes, and there is nothing of upstream curl's to measure (ADR-0003).

## Decision

### 1. `CheckedLogin` in Abstractions

`Surl.Protocol.Abstractions.UnitLibrary/CheckedLogin.cs`, namespace `Surl.Protocol.Abstractions`:

```csharp
public sealed record CheckedLogin(string Method, string? User, bool IsAccepted)
{
    public const string BearerTokenUser = "bearer token";

    public string Note =>
        $"Login {(IsAccepted ? "accepted" : "refused")}: {Method}{(User is null ? string.Empty : " " + User)}";
}
```

- `Method` is the method checked, `User` the user name as sent (or `BearerTokenUser`), and
  `IsAccepted` whether the credentials matched an account.
- `Note` builds ADR-0032 section 8's text in one place, so every protocol server writes the
  same words. It is not escaped here: the server passes it to `IExchangeLog.Note`, and the
  verbose log (`Surl.Output.UnitLibrary/VerboseExchangeLog.cs`) escapes every note by ADR-0007
  section 8.
- The record has no field for a password, a token or an `Authorization` value, so no note built
  from it can hold one.

**Why:** a record of the three facts the note needs is the smallest thing both HTTP and the
password-login servers can share, and it keeps the note's wording out of every server.

### 2. `HttpAuthenticationVerdict` gains an optional fourth parameter; the server writes the note

```csharp
public sealed record HttpAuthenticationVerdict(
    HttpAuthenticationOutcome Outcome,
    IReadOnlyList<string> WwwAuthenticateValues,
    string? AccountName,
    CheckedLogin? CheckedLogin = null);
```

`Surl.Protocol.Http.UnitLibrary/HttpRequestResponder.cs` writes `verdict.CheckedLogin.Note`
to the exchange log whenever it is not `null`, right after `JudgeAsync` returns and before the
response. The policy never receives an `IExchangeLog`.

**Why:** every other note is written by the server that owns the exchange, through the
`IExchangeLog` on its `ExchangeContext`; the login note is one more. With the note on the
verdict, `Surl.Protocol.Http.UnitTests` proves the exact text with its own
`IHttpAuthenticationSession` double and a `RecordingExchangeLog`, with no reference to
`Surl.Authentication` (ADR-0002). Handing the log to the policy would have put a second writer
on each exchange's log and made the note provable only through `Surl.Authentication`. The
parameter is optional, so every existing construction of the verdict (`Forbidden`, the plain
`Challenge`, `AnonymousAuthenticationPolicy`'s `Proceed`) compiles unchanged and carries no
note.

### 3. `PasswordLoginVerdict.AcceptedUnchecked`, declared last

```csharp
public enum PasswordLoginVerdict
{
    Accepted,
    RefusedCredentials,
    RefusedAnonymous,
    RefusedPlaintext,
    AcceptedUnchecked,
}
```

- `AcceptedUnchecked` is the `--allow-anonymous` answer: `Surl.Authentication`'s
  `AuthenticationPolicy` returns it before looking at the credentials, and
  `AnonymousAuthenticationPolicy` returns it for every login. `Accepted` now means only that
  the credentials were checked and match an account.
- It is declared last, so no existing value is renumbered.
- A password-login server answers `AcceptedUnchecked` as it answers `Accepted` (MQTT: `CONNACK`
  0) and writes no note for it. MQTT's verdict switch maps any value it does not know to
  `CONNACK` 5, so a verdict added later fails closed.
- A password-login server writes a note only for `Accepted` and `RefusedCredentials`, with
  `PasswordLogin.Scheme`, the listen URL's scheme, as the method and `PasswordLogin.UserName`
  as the user: `Login accepted: mqtt tester` (`mqtts` on an `mqtts://` listen URL). The note
  is written by `Surl.Protocol.Mqtt.UnitLibrary/MqttPacketResponder.cs`, before the `CONNACK`.

**Why:** with a plain `Accepted` for `--allow-anonymous`, MQTT would have noted
`Login accepted` for logins nobody checked, while HTTP under `--allow-anonymous` notes nothing.
A separate value keeps the two protocols telling the same story and keeps `Accepted` true to
its name.

### 4. The method name in a note

- **HTTP:** the `Authorization` scheme as it is spelled on the wire,
  `AuthenticationMethods.AuthorizationSchemeOf`: `Basic`, `Digest`, `Bearer`, `NTLM`,
  `Negotiate` or `AWS4-HMAC-SHA256`. That is the word a reader finds on the bytes-received
  line above the note.
- **A password login:** the listen URL's scheme, which `PasswordLogin.Scheme` already carries
  "for the log" (ADR-0032 section 6).

### 5. Which logins write no note

Only a login whose credentials were checked writes one. None is written for:

- **no credentials** (an HTTP request with no `Authorization`, or one of a method outside the
  accepted set, answered as missing);
- **a plain-text secret refused unchecked** (HTTP's `403` for Basic or Bearer in clear; MQTT's
  `RefusedPlaintext`);
- **a login with no user name** (MQTT's `RefusedAnonymous`);
- **a handshake's continuation step** (a `Continue` that carries `WWW-Authenticate` values,
  such as Digest's `stale=true` challenges), which has checked nothing yet;
- **`--allow-anonymous`** (HTTP's `Proceed` with no verdict note; `AcceptedUnchecked`).

A continuation step with no value to send is answered as a refusal (a `401` must carry a
challenge, RFC 9110 section 11.6.1), and like every refusal it is delayed and noted
`Login refused`.

**Why:** a note says what credentials were worth. Where none were checked the note would
either claim a check that did not happen or say nothing the `CONNACK` or status line does not
already say.

### 6. A user that cannot be read is left out

When the credentials carry no readable user the note has no `<user>`: `Login refused: Basic`.
That is Basic credentials that are not base64, hold no `:`, or whose user-id is not UTF-8
(ADR-0035), and a Digest `Authorization` that cannot be parsed. Otherwise the user is the one
sent: Basic's user-id, Digest's `username` parameter as sent (ADR-0036), and
`CheckedLogin.BearerTokenUser` for Bearer, whose token is never written. Inside
`Surl.Authentication` each method reports it as `HttpCredentialCheck.UserAsSent`, and
`HttpAuthenticationSession` builds the `CheckedLogin` from it.

**Why:** a user that could not be decoded has no faithful text, and writing the raw bytes
could put part of a password in the log (a Basic value with no `:` is user and password run
together).

## Alternatives considered

- **Give the policy an `IExchangeLog`.** Rejected (decision 2): a second writer on the
  exchange's log, and the note provable only through `Surl.Authentication`.
- **A method and user on `HttpAuthenticationVerdict` itself, no `CheckedLogin`.** Rejected: the
  password-login servers need the same three facts and the same wording; one record serves both.
- **Keep `Accepted` for `--allow-anonymous` and let MQTT look at the settings.** Rejected: a
  protocol server cannot see `Surl.Authentication`'s settings (ADR-0002), and the verdict
  would still say "accepted" for a check that did not happen.
- **`AcceptedUnchecked` inserted beside `Accepted`.** Rejected: it would renumber
  `RefusedCredentials` and the values after it.

## Consequences

- ADR-0032 section 6's contract block shows the three additions and points here.
- `Surl.Protocol.Http.UnitTests` and `Surl.Protocol.Mqtt.UnitTests` prove the notes with their
  own policy doubles; neither references `Surl.Authentication`.
- Each HTTP method still to be built (NTLM, Negotiate, AWS Signature Version 4) reports its
  user as sent through `HttpCredentialCheck.UserAsSent`; one that sets none gets a note with no
  `<user>`.
- Each password-login server still to be built (FTP, IMAP, POP3, SMTP, LDAP) notes `Accepted`
  and `RefusedCredentials` as MQTT does, and treats `AcceptedUnchecked` as accepted.
