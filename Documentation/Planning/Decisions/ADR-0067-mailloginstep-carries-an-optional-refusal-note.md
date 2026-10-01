# ADR-0067 — `MailLoginStep` carries an optional `RefusalNote` for why a mail login was refused

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-260 (FR-046); recorded by BL-276.
- **Amends:** [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) section 6 (the
  contract's `MailLoginStep` record and its note rule).

## Context

[ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
decision 4 has a refused Kerberos ticket leave its reason in the verbose log as one line,
`Kerberos: <reason>` (for example `Kerberos: ticket expired`), and never a key byte, a password or
a decrypted field. HTTP `Negotiate` writes that line itself. For SASL `GSSAPI` on SMTP, IMAP and
POP3 the mechanism lives in `Surl.Authentication` behind `ISaslExchange`, and the servers write
only what the `MailLoginStep` it returns carries (ADR-0049 section 6): the `CheckedLogin`, whose
`Note` is `Login refused: <method> <user>` (ADR-0038). Nothing on the step could carry the
reason, so BL-260 had to widen the contract.

BL-260 made the change; it could not record it then because `Documentation/Planning/Decisions`
was named by BL-262, in `Doing` at the time, and two lanes would have allocated the same ADR
number. This ADR records it.

## Decision

1. **The member.** `MailLoginStep` (`Surl.Protocol.Abstractions.UnitLibrary/MailLoginStep.cs`)
   has an optional fifth member, `string? RefusalNote = null`: why the credentials were refused,
   as one log line, or `null` when the mechanism names no reason. It never holds a key byte, a
   password or a decrypted field. Being optional with a `null` default, every existing
   construction of the record, in production and in tests, compiles and means what it meant.
2. **Who sets it.** `SaslMechanismExchange.RefuseAsync` (`Surl.Authentication.UnitLibrary`)
   takes it as an optional last argument, `string? refusalNote = null`, and puts it on the
   `RefusedCredentials` step beside the `CheckedLogin`, whose method is the mechanism's registered name. Today only
   `GssapiSaslExchange.AcceptTicketAsync` passes one: when the `--keytab` acceptor refuses the
   ticket, it passes `"Kerberos: " + KerberosAcceptResult.RefusalReason`. Its other refusals (a
   non-empty answer to the AP-REP, an unreadable security-layer choice, a principal with no
   account or a mismatched authorization identity) name no reason, and no other mechanism sets it.
3. **Where the servers write it.** The SMTP, IMAP and POP3 sessions
   (`SmtpSession`, `ImapSession.Authentication`'s `NoteCheckedLogin`, `Pop3Session`) write
   `RefusalNote` to the exchange log with `Note` whenever it is not `null`, immediately after
   `CheckedLogin.Note` and before they answer, so the log reads
   `Login refused: GSSAPI` and then `Kerberos: <reason>`.
4. **Which level shows it.** Nothing new: `LevelledExchangeLogFactory` lets no note through below
   the verbose level (ADR-0033, section 3), so the reason appears at `--verbose` only.
   `LevelledExchangeLogFactoryTests.KerberosRefusalNote_IsWrittenAtTheVerboseLevelOnly`
   (`Surl.Output.UnitTests`) pins it, and `SmtpAuthTests`, `ImapAuthenticateTests` and
   `Pop3AuthTests` pin the two notes in order.

ADR-0049 section 6's `MailLoginStep` therefore reads:

```csharp
public sealed record MailLoginStep(
    MailLoginOutcome Outcome,
    ReadOnlyMemory<byte> Challenge,         // the continuation's bytes before base64; empty unless Challenge
    string? AccountName,                    // the account logged in when Accepted; otherwise null
    CheckedLogin? CheckedLogin,             // the login note to write; null when nothing was checked
    string? RefusalNote = null);            // why the credentials were refused, written after the note; null for no reason
```

## Alternatives considered

- **A reason field on `CheckedLogin`.** Rejected. `CheckedLogin` is shared: HTTP, MQTT and the
  password logins all build it, and its one-line `Note`, `Login refused: <method> <user>`
  (ADR-0038), is pinned by their tests. Carrying the reason there would either change that line
  for every protocol or add a member every other caller must ignore, and it would mix what was
  checked with why the check failed. A separate member on the mail step touches only the mail
  path and leaves ADR-0038's note as it is.
- **Have `GssapiSaslExchange` write the line itself.** Rejected: the mechanism has no exchange
  log, and giving it one would break ADR-0049 section 6's rule that the server writes every note
  the step carries, in order, before it answers.

## Consequences

- A mail login refused by the Kerberos acceptor explains itself at `--verbose` with the same
  `Kerberos: <reason>` line HTTP `Negotiate` writes.
- Any later mechanism with a reason worth logging passes it to `RefuseAsync`; no server changes.
- `MailLoginStep`'s positional shape grew by one optional member; callers that deconstruct it
  positionally would need the fifth element, and none do.
