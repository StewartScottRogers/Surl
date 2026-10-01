# ADR-0069 — One name for the TLS-upgrade flag: `isTlsUpgradeAvailable`

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-274.
- **Amends:** [ADR-0053](ADR-0053-how-the-smtp-server-answers-upstream-curl.md), which names the
  SMTP server's flag `isStartTlsAvailable`; the flag's meaning is unchanged, only its name.

## Context

Four servers can upgrade a plaintext connection to TLS with their protocol's own command:
`STARTTLS` (SMTP, IMAP), `STLS` (POP3) and `AUTH TLS` (FTP). Whether they offer it is one
setting with one source: `Surl.Console`'s `CommandLineRunner.ComposeProtocolServers` computes
`isTlsUpgradeAvailable` from `ServerTlsComposition.IsCertificateConfigured` and passes it to all
four constructors. The glossary's term for the concept is "TLS upgrade".

After BL-264, FTP takes it as `isTlsUpgradeAvailable` (`FtpProtocolServer`,
`FtpCommandResponder`), while the mail servers take it under their command's name:
`isStartTlsAvailable` in `SmtpProtocolServer`, `SmtpSession`, `ImapProtocolServer` and
`ImapSession`, and `isStlsAvailable` in `Pop3ProtocolServer` and `Pop3Session`, with their test
helpers (`SmtpTestExchange.StartTlsServer`, `ImapTestExchange.Server`, `Pop3TestExchange.Server`
and `RunAsync`) following suit. The question BL-214 raised is whether that mixed state is the
decision or a step towards one name.

## Decision

1. **One name everywhere.** The constructor parameter, the private field behind it and every test
   helper's parameter for this setting is `isTlsUpgradeAvailable`, in `Surl.Console`,
   `Surl.Protocol.Ftp`, `Surl.Protocol.Smtp`, `Surl.Protocol.Imap` and `Surl.Protocol.Pop3`. The
   mixed state after BL-264 is a step, not the decision.
2. **What stays per-command.** Names that describe the exchange rather than the setting keep the
   command's name: test classes and methods such as `ImapStartTlsTests` and `Pop3StlsTests`, the
   wire text, and the glossary's "STARTTLS discard" term. Each session's existing `CanUpgrade`
   property is already command-neutral and stays.
3. **The renames.** BL-280 renames `isStartTlsAvailable` and `isStlsAvailable` in the SMTP, IMAP and POP3 libraries,
   their `.UnitTests` twins and their `CLAUDE.md` files, and rewrites the glossary's "TLS upgrade"
   row to name the one name. `Surl.Console` and FTP need no change: the console already uses
   `isTlsUpgradeAvailable` and passes it positionally, and BL-264 renamed FTP's. Renames only;
   nothing on the wire changes.

## Why

- Root `CLAUDE.md`, "Say what it does, do what it says": one concept has one name, the one in the
  glossary. The setting is one concept - one `bool`, from one source, with one meaning (a
  certificate is configured, so the server may offer its upgrade command) - and the glossary calls
  it a TLS upgrade.
- A reader following the value from `ComposeProtocolServers` into a server today sees it change
  name at the constructor boundary three different ways, and has to learn that `isStlsAvailable`
  and `isTlsUpgradeAvailable` are the same thing. One name makes a solution-wide search for the
  setting find every use.

## Alternative rejected

**Per-command names** (`isStartTlsAvailable`, `isStlsAvailable`, and FTP back to
`isAuthTlsAvailable`). Each would read as the command it gates, which helps inside one server.
Rejected because the command is already fixed by the class it sits in - `SmtpSession` can only
mean `STARTTLS` - so the per-command name adds nothing there, while across the solution it splits
one setting into three names, which the naming rule forbids.

## Consequences

- Until BL-280 is done the glossary's "TLS upgrade" row, which lists the names as they are, stays
  true; BL-280 updates it in the same diff as the renames.
- A future server with a TLS upgrade command (for example LDAP's StartTLS extended operation)
  takes the flag as `isTlsUpgradeAvailable` too.
