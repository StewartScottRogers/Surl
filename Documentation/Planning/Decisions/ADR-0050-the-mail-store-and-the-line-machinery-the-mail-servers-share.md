# ADR-0050 — The mail store and the line machinery the mail servers share: `Surl.MailStore` and `Surl.LineProtocol`

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-184.
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table of
  horizontal libraries gains the two rows of decision 1 below. Everything else in ADR-0002
  stands.

## Context

The SMTP, IMAP and POP3 servers (FR-043 to FR-047) share two things: the mail itself - SMTP
receives it, IMAP and POP3 serve it - and the way they read and write CRLF lines. Protocol
servers may not reference each other (ADR-0002 decision 3, root `CLAUDE.md`), so both live in
horizontal libraries, and a horizontal library joins ADR-0002's table through a new ADR. The
plan already names two: BL-189 creates them, BL-190 and BL-191 build the store, BL-192 the line
machinery, and BL-198 to BL-209 build and register the three servers on them.

The questions this ADR answers, and the rules that bound them:

- **Whose mail is it?** Accounts come from `--user` and `--user-file`, and an empty name is a
  Bearer token, not an account a name-and-password method can use (ADR-0032 section 1).
  `--allow-anonymous` accepts every login without checking it (ADR-0032, ADR-0038), and a login
  accepted that way carries no account name (`MailLoginStep.AccountName` is `null` for
  `AcceptedUnchecked`, ADR-0049 section 6).
- **What a peer may learn.** "Nothing a peer receives differs between no such user, wrong
  password and no accounts configured" (ADR-0032 section 8, from ADR-0006 section 3). RFC 5321
  section 3.3 lets an SMTP server answer an unknown recipient `550`, which tells any peer on the
  internet which accounts exist.
- **Bounds.** Every store a peer can fill is bounded (ADR-0006); retained MQTT messages set the
  pattern with constants passed to the constructor (ADR-0014 decision 7, ADR-0031 section 4).
- **Persistence.** Service state lives under `<path>/.surl/<service>/`, is loaded once at start
  after the data-directory lock, refuses to start on a malformed file (`CouldNotReadFile`, 37)
  rather than overwrite it, notes a failed write without ending anything, and stays in memory
  without `--directory` (ADR-0031 decisions 6 to 8). ADR-0031's retained-message file is
  rewritten whole on every change; a mail store may hold 256 MiB, so rewriting it whole on
  every delivery is not an option.
- **Lines.** A command line is bounded by `--max-line` (`ExchangeLimits.MaxLineBytes`) and the
  head timeout, and surl never reads past a limit (ADR-0006 sections 1 and 5). Bytes read past a
  `STARTTLS` or `STLS` line are discarded before the upgrade (ADR-0010 section 1). A message body
  is dot-stuffed on the wire (RFC 5321 section 4.5.2, RFC 1939 section 3) and bounded by
  `--max-filesize` (`ExchangeLimits.MaxUploadBytes`). SASL continuation lines are base64 with
  `*` cancelling (RFC 4954 section 4, RFC 3501 section 6.2.2, RFC 5034 section 4); ADR-0049
  section 6 leaves the framing to the servers.
- **Uploads.** `--allow-uploads` gates "uploads into the served files" (ADR-0006 section 2,
  ADR-0031 decision 2). Mail is service state under `.surl/`, never a served file (ADR-0031
  decision 5).

Nothing in this ADR pins a byte upstream curl sends or expects: every wire reply named below is
a reply code or response code from the RFCs, and its exact text is the SMTP, IMAP and POP3
servers' own ADRs' (BL-186, BL-187, BL-188), measured against pinned upstream curl as ADR-0003
requires. The persisted bytes are Surl's own and are pinned by BL-191's tests.

## Decision

### 1. Two libraries, and their rows in ADR-0002's table

| Horizontal library | May itself reference | Holds |
| --- | --- | --- |
| `Surl.LineProtocol.UnitLibrary` | `Surl.Protocol.Abstractions.UnitLibrary` | decision 8's line machinery |
| `Surl.MailStore.UnitLibrary` | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary` (for `IContentFileSystem`) | decisions 2 to 7's mail store |

- Both are production libraries held to the solution's quality gates, AOT-compatible, and
  touch neither a socket nor the disk: the store reads and writes only through
  `IContentFileSystem`, the line machinery only through `IConnection`.
- `Surl.Protocol.Smtp`, `Surl.Protocol.Imap` and `Surl.Protocol.Pop3` reference both (BL-198,
  BL-201, BL-205). Any other protocol server may reference either, as ADR-0002 allows for every
  row.
- **Two libraries, not one:** a store is not line machinery, a line reader has no business
  referencing `Surl.Content`, and two projects let two dark factory lanes build them at once.
- **FTP** has its own line reader planned (BL-177). Moving FTP onto `Surl.LineProtocol` once it
  exists is a possible follow-up, not a dependency of anything here.

### 2. Owners: whose mailboxes exist

The store keeps a **mailbox set per owner**, an owner being a name (a .NET `string`, compared
ordinally):

- **Every named account** (`--user`, `--user-file`, a non-empty name) is an owner.
- **An empty-name Bearer account is not an owner.** A token has no name, so no recipient can
  name it. A login with `XOAUTH2` or `OAUTHBEARER` acts as the owner named by the user the client
  sent (`user=`, or `OAUTHBEARER`'s `a=`): the token is the operator's key to the mail, as an
  HTTP Bearer token reads every file. BL-194 sets `MailLoginStep.AccountName` to that name on
  `Accepted`. A name that names no owner acts as an **empty view**: no mailboxes, nothing to
  read, every write refused as a missing mailbox.
- **Under `--allow-anonymous` there is exactly one owner reached, the anonymous owner**, whose
  name is the empty string (it can never collide with an account, whose name is never empty).
  Every recipient is delivered to its `INBOX` and every session - logged in with any name, or not
  logged in - acts as it. With `--allow-anonymous` the accounts' own mailboxes are kept (in
  memory, and in the file with `--directory`) but not reached. **Why:** `--allow-anonymous` is
  the testing loosening (ADR-0032); one shared mailbox set means `curl --mail-rcpt anyone@x -T
  m smtp://…` followed by `curl imap://…/INBOX` or `curl pop3://…/1` reads the message back
  with any user or none, and a login `AcceptedUnchecked` carries no account name anyway.
- An owner held in the persisted store that is no longer an account (an account removed between
  runs) is kept, unreached and counted against the bounds, so adding the account back restores
  its mail. Deleting mail is an operator's act, never a side effect of a configuration change.

### 3. Mailboxes and messages

- **`INBOX`** exists for every owner reached: at start, after loading, the store creates the
  `INBOX` of every owner that lacks one (decision 7's index is then written once). `INBOX` is
  matched case-insensitively (`OrdinalIgnoreCase`, RFC 3501 section 5.1) and stored as `INBOX`;
  every other name is matched ordinally.
- **Other mailboxes** are created, deleted and renamed by IMAP. The store keeps names flat, as
  strings: the hierarchy delimiter and modified UTF-7 are the IMAP server's view (BL-187). A
  name is 1 to `MaxMailboxNameBytes` UTF-8 bytes with no control character (U+0000 to U+001F,
  U+007F). `INBOX` cannot be deleted; renaming `INBOX` moves all its messages into a new mailbox
  of the new name and leaves `INBOX` empty with its `UIDVALIDITY` and next UID unchanged (RFC
  3501 section 6.3.5).
- **A mailbox** has a `UIDVALIDITY` (a non-zero `uint`) and a next UID (a `uint`, 1 when
  created). `UIDVALIDITY` is issued store-wide as the larger of the Unix time in seconds from
  the injected `TimeProvider` and one more than the last `UIDVALIDITY` issued, so a mailbox
  deleted and created again under the same name never repeats one (RFC 3501 section 2.3.1.1).
- **A message** has a UID (from the next UID, which then rises by one; never reused, even after
  the message with the highest UID is expunged), flags, an internal date, and its bytes.
  - **Flags** are the five permanent system flags `\Seen`, `\Answered`, `\Flagged`,
    `\Deleted` and `\Draft`. Keywords are not kept, so `PERMANENTFLAGS` lists these five and no
    `\*`; `\Recent` is not kept either (RFC 3501 lets a server report none). How IMAP answers a
    keyword is BL-187's.
  - **The internal date** is the injected `TimeProvider`'s `GetUtcNow()` at delivery, or the
    `date-time` an IMAP `APPEND` gives, kept with its offset.
  - **The bytes** are exactly those the server hands over: SMTP's body after dot-unstuffing,
    IMAP's `APPEND` literal. Whether SMTP adds `Return-Path` and `Received` trace fields (RFC
    5321 section 4.4) is BL-186's; the store adds nothing. A message's bytes never change once
    stored.
  - A message whose UID would pass `uint.MaxValue` is refused as the store being full
    (decision 6).
- **Operations**, each atomic with respect to every other: deliver one message to several
  owners' `INBOX`es (all of them or none), append to a named mailbox, fetch a message's bytes,
  set and clear flags, expunge a mailbox's `\Deleted` messages, copy messages to another
  mailbox (new UIDs there), create, delete and rename a mailbox, and list an owner's mailboxes.
  A refusal a peer can cause (a missing mailbox, a bound reached, the maildrop locked, an
  invalid name) is a typed outcome, not an exception.
- **Concurrent sessions** see each other's changes at once, except POP3 (decision 4). A fetch
  of a message another session expunged meanwhile is a missing message.

### 4. POP3's maildrop and its lock

- A POP3 session's **maildrop is its owner's `INBOX`**.
- RFC 1939 section 4's **exclusive-access lock** is the store's, per owner, in memory, taken at
  a successful login and released when the session ends however it ends (the lock handle is
  disposed). A second POP3 login for an owner whose maildrop is locked gets a typed
  `MaildropLocked` outcome, which the POP3 server answers `-ERR [IN-USE]` (RFC 3206 section 5;
  text BL-188's) - also under `--allow-anonymous`, where every POP3 session shares the one
  owner.
- The lock does not stop SMTP or IMAP. The POP3 session's **view is fixed at login**: the
  messages then in `INBOX`, numbered 1 upward in UID order, with their sizes. A message
  delivered later is not in it; a message IMAP expunges meanwhile is still readable from the
  view (its bytes do not change, decision 3), and removing it at `QUIT` is then a no-op.
  `DELE` marks within the session; only `QUIT` in the TRANSACTION state removes the marked
  messages from the store (RFC 1939 section 6).

### 5. Recipients and deliveries

- **An SMTP `RCPT TO` address** maps to an owner by its local part, read from RFC 5321's
  `Path`: a source route is ignored, a quoted local part is unquoted (backslash escapes
  removed), and the domain (or address literal) is **ignored**, whatever it is. **Why:** surl
  has no configured domain, and a server listening on any address cannot know the name the
  client used, as ADR-0049 section 5 says of `DIGEST-MD5`'s `digest-uri`.
- The local part matches an account ordinally; when none matches that way, it matches the one
  account that equals it case-insensitively (`OrdinalIgnoreCase`), if exactly one does (RFC 5321
  section 2.4 advises against case-sensitive local parts). `postmaster` is a local part like any
  other.
- **An unknown recipient is accepted exactly as a known one**, with the same reply, and its copy
  is discarded at delivery: the server notes `Mail for <address> discarded: no such account`
  in its exchange log (the address escaped by ADR-0006 section 3), and nothing reaches the peer.
  **Why:** RFC 5321's `550` would tell any peer on the internet which accounts exist, which
  ADR-0032 section 8 forbids for logins; the same rule holds for recipients. A syntactically
  invalid address is refused at `RCPT` as RFC 5321 says (text BL-186's), which reveals nothing
  and is how `curl --mail-rcpt-allowfails` is exercised.
- Under `--allow-anonymous` no recipient is unknown: every one is delivered to the anonymous
  owner (decision 2), and a message with several recipients is stored once per recipient, so
  the anonymous `INBOX` holds one copy per `RCPT`, as separate mailboxes would.
- **`--allow-uploads` gates neither SMTP delivery nor IMAP `APPEND`.** Mail is service state,
  not the served files `--allow-uploads` names (ADR-0006 section 2, ADR-0031 decision 5), and an
  SMTP server that refused mail by default would not be one. What protects the store is its
  bounds (decision 6), and for IMAP the login `APPEND` already needs; whether SMTP needs a login
  before `MAIL` is BL-186's.

### 6. Bounds

The store's bounds are constructor parameters with these defaults, as `MqttRetainedMessages`'
are; no command-line option sets them in this work:

| Constant | Default | Counts |
| --- | --- | --- |
| `MailboxStore.DefaultMaxMessages` | 100000 | messages held, every owner and mailbox together |
| `MailboxStore.DefaultMaxTotalMessageBytes` | 268435456 (256 MiB) | bytes of the distinct message files held (decision 7: copies share one) |
| `MailboxStore.DefaultMaxMailboxes` | 10000 | mailboxes held other than the owners' `INBOX`es |
| `MailboxStore.MaxMailboxNameBytes` | 1024 | UTF-8 bytes of one mailbox name (a limit, not a parameter) |

The type is `Surl.MailStore.MailboxStore`, not `MailStore`: a type named `MailStore` in the
namespace `Surl.MailStore` would be shadowed by that namespace in every other `Surl.*`
namespace (C# name lookup finds the namespace `Surl.MailStore` before any `using`-imported
type), so each server would have to write `global::Surl.MailStore.MailStore`.

- 256 MiB matches `InMemoryContentFileSystem.DefaultMaxTotalBytes` (ADR-0031 decision 4): room
  for two messages of ADR-0006's 100 MiB `--max-filesize` default at once. Without
  `--directory` every byte is in memory, so the bound is a memory bound too.
- **One message** is bounded by `--max-filesize` as each server reads it (decision 8), and the
  store refuses one larger than its own `MailboxStore.MaxMessageBytes` (`Surl.Console` sets it from
  `ExchangeLimits.MaxUploadBytes`; 0 means no limit) again as a second line of defence, with
  the typed `MessageTooLarge` outcome and nothing stored.
- **Past a bound** nothing is stored, and the operation (a whole delivery, a whole `COPY`) gets
  the typed `StoreFull` or `TooManyMailboxes` outcome, answered in each protocol's words (texts
  in BL-186's and BL-187's ADRs). `StoreFull` also answers a new mailbox that would need a
  `UIDVALIDITY` past `uint.MaxValue` (the year 2106 as Unix seconds):

  | Outcome | SMTP | IMAP | POP3 |
  | --- | --- | --- | --- |
  | `StoreFull` after `DATA` | `452` (RFC 5321 section 4.2.2, "insufficient system storage"; enhanced code `4.3.1`, RFC 3463) | n/a | n/a |
  | `StoreFull` on `APPEND` or `COPY` | n/a | tagged `NO [OVERQUOTA]` (RFC 5530 section 3) | n/a |
  | `TooManyMailboxes` on `CREATE` or `RENAME` | n/a | tagged `NO [LIMIT]` (RFC 5530 section 3) | n/a |
  | A message past `--max-filesize` | `552`, ADR-0006 section 5 | tagged `NO`, ADR-0006 section 5 | n/a |

  `452` is transient, so a sender may retry once mail has been read and removed.

### 7. Persistence under `<path>/.surl/mail`

ADR-0031 decision 6's pattern, with one change: messages are written one file each, once, and
only the index is rewritten whole.

- **Files:** `<path>/.surl/mail/index`, and `<path>/.surl/mail/messages/<n>`, `<n>` a message
  file number written as 16 lower-case hexadecimal digits. Without `--directory` there are no
  files and every byte is held in memory.
- **A message file** holds a message's bytes exactly, and is written once through
  `<path>/.surl/mail/messages/.pending-<guid>` (the server streams the body into it as it reads)
  then renamed to its number with `IContentFileSystem.MoveFileReplacing`. Every copy of a
  delivered message (several recipients, IMAP `COPY`) refers to the same file, which is deleted
  once no message refers to it. With a data directory, a message's bytes are read from its file
  when a server fetches them, not held in memory.
- **The index** is written after every change that alters the store (a delivery, an append, a
  flag change, an expunge, a copy, a mailbox created, deleted or renamed, an `INBOX` created at
  start), not after a refused or no-op one, through `<path>/.surl/mail/.index-<guid>` and
  `MoveFileReplacing`. Writes are serialised inside the store's lock, so the file always holds a
  state the store held. A change's message file is renamed into place before its index is
  written; an expunged message's file is deleted after the index no longer names it.
- **The index's byte format.** All integers big-endian and unsigned unless marked signed:

  | Part | Bytes |
  | --- | --- |
  | Header | the 18 ASCII bytes `SURL-MAIL-INDEX-1` then LF (0x0A) |
  | Next message file number | 8 bytes |
  | Last `UIDVALIDITY` issued | 4 bytes |
  | Owner count | 4 bytes, then that many owners in ordinal order of name |
  | Owner | name length (2 bytes), name as UTF-8 (empty for the anonymous owner), mailbox count (4 bytes), then that many mailboxes in ordinal order of name |
  | Mailbox | name length (2 bytes), name as UTF-8 (`INBOX` in capitals), `UIDVALIDITY` (4 bytes), next UID (4 bytes), message count (4 bytes), then that many messages in ascending UID order |
  | Message | UID (4 bytes), flags (1 byte: bit 0 `\Seen`, bit 1 `\Answered`, bit 2 `\Flagged`, bit 3 `\Deleted`, bit 4 `\Draft`), internal date as signed Unix seconds (8 bytes, signed) and signed offset in minutes (2 bytes, signed), size in bytes (8 bytes), message file number (8 bytes) |

  Then end of file. An empty store is the header, the two numbers and an owner count of 0.
  Ordinal order makes the bytes a function of the store's contents, so a test can pin them.
  An owner with no mailboxes is not written.
- **Loaded** once at start, after the data-directory lock and before any listener binds (ADR-0031
  decision 7's order); `Surl.Console` does it (BL-207). A missing index is an empty store, and
  any message files beside it are ignored. Leftover `.index-<guid>` and `.pending-<guid>` files,
  and message files the index does not name, are ignored (a later delivery given the same
  number replaces such a file).
- **Malformed at start** - surl refuses to start with `CouldNotReadFile` (37) and stderr
  `surl: (37) Could not read <file path>: <reason>`, `<file path>` the full path of the file at
  fault:
  - the index cannot be read: `<reason>` is the exception message;
  - the index does not parse: `<reason>` is `not a mail store index`, for a wrong header; a
    truncated part; trailing bytes; an owner or mailbox name that is not UTF-8, holds a control
    character, or is repeated (an `INBOX` in any case counting as `INBOX`); an empty mailbox name
    or one past `MaxMailboxNameBytes`; a `UIDVALIDITY` of 0, above the last issued, or repeated
    within an owner; a next UID of 0; a UID of 0, not ascending, or not below its mailbox's next
    UID; a flag bit above bit 4; an offset outside -1439 to 1439 minutes; a message file number
    not below the next one; one file number given two different sizes; or more messages, bytes
    or mailboxes than decision 6's bounds;
  - a message file the index names is missing, cannot be read, or is not the size the index
    gives: `<file path>` is the message file and `<reason>` is `missing or not the size the index
    gives`, or the exception message.
  Starting empty instead would overwrite the index on the first delivery and lose the mail
  silently, which is ADR-0031's reason too.
- **A write that fails while serving:**
  - a message file that cannot be written refuses that delivery or append - nothing is stored,
    since its bytes would exist nowhere - with a typed `StorageFailed` outcome carrying the
    exception message, which the server notes with `IExchangeLog.Note` and answers as a local
    error (SMTP `451`, IMAP tagged `NO`; texts BL-186's and BL-187's);
  - an index that cannot be written leaves the change in memory, and the outcome carries the
    exception message for the server to note; nothing ends, and the next change rewrites the
    whole index;
  - a message file that cannot be deleted is noted the same way and left behind, ignored at the
    next load.
- **Nothing outside the data directory** (ADR-0031 decision 8): the store writes only these
  paths, only through `IContentFileSystem`.

### 8. The line machinery in `Surl.LineProtocol`

Everything reads through `IConnection.ReadAsync` with the exchange's cancellation token and
`ExchangeLimits`, and times with `ExchangeContext.TimeProvider`.

- **A command-line reader** over one connection, holding one buffer:
  - A line ends **only at CRLF**. A bare LF or a bare CR is part of the line, which its parser
    then refuses or keeps as it chooses (RFC 5321 section 2.3.8; the SMTP-smuggling defect
    class). The line is returned without its CRLF.
  - **Bounded by `MaxLineBytes`, CRLF included** (ADR-0006 section 1). The buffer is never
    filled past the limit, so no byte past it is read: when `MaxLineBytes` bytes are buffered and
    no CRLF is among them, the outcome is `LineTooLong`. A limit of 0 means no limit.
  - **The head timeout** (ADR-0006 section 1): for a connection's first line the clock starts
    when the reader is created, which a server does at the start of `ServeAsync` (after the
    engine's TLS handshake for implicit TLS, a difference from "at accept" of the handshake's
    length, accepted); for every later line it starts at the line's first byte. Past it the
    outcome is `HeadTimedOut`. The idle timeout and the maximum duration stay the engine's
    (they cancel the token).
  - A peer that closes gives `Closed`, with no partial line returned. Outcomes are values, not
    exceptions: the server answers each in its own words (ADR-0006 section 5).
  - **Pipelining** is allowed: bytes after a line stay buffered for the next call.
  - **Reading a counted run** of bytes through the same buffer, for IMAP literals (`{n}`),
    bounded by the caller.
  - **Discarding what is buffered**, returning how many bytes were thrown away: a server calls it
    after reading a `STARTTLS` or `STLS` line and before `UpgradeToTlsAsync`, so pipelined
    plaintext is never run as a command (ADR-0010 section 1); the count is for the server's log
    note.
- **A dot-stuffed body reader** (SMTP `DATA`; RFC 5321 section 4.5.2): reads from the same
  buffer until CRLF `.` CRLF, where the body's first line counts as following a CRLF; removes
  the first `.` of a line starting with `.`; keeps a bare LF and a bare CR as they are and never
  ends the body at one (so `LF . LF` is body, not an end). The body's final CRLF (the one before
  `.`) is kept, the terminator's `.` CRLF is not. It streams to a caller's `Stream` (decision 7's
  pending file, or memory) and counts the bytes written: past `MaxUploadBytes` the outcome is
  `BodyTooLarge` and nothing more is read, so the server cannot find the end of the body and
  answers ADR-0006's `552` then closes.
- **A dot-stuffing body writer** (POP3 `RETR` and `TOP`; RFC 1939 section 3): writes a
  message's bytes adding a `.` before every line that starts with `.` (at the start and after
  each CRLF, not after a bare LF, since upstream curl unstuffs only after CRLF), adds a CRLF when
  the bytes do not end with one, then writes `.` CRLF.
- **A reply-line writer**: writes one line of ASCII text followed by CRLF, and throws
  `ArgumentException` for text holding a CR, an LF or a byte outside 0x20 to 0x7E, so no
  peer-supplied byte can inject a reply line.
- **A SASL continuation reader** (ADR-0049 section 6's framing): reads one line with the
  command-line reader's bounds and outcomes, then classifies it: `*` alone is `Cancelled`; an
  empty line is an empty response; otherwise `Convert.TryFromBase64String` decodes it, and text
  that is not base64 (whitespace included) is `NotBase64`. The initial response's `=` belongs
  to the command it rides on, so the servers read it there (ADR-0049 section 6).

### 9. Who builds what

| Work | Task |
| --- | --- |
| The two projects, `Surl.slnx`, the isolation test's rows | BL-189 |
| The store in memory: decisions 2 to 6 | BL-190 |
| The store's persistence: decision 7 | BL-191 |
| The line machinery: decision 8 | BL-192 |
| `XOAUTH2`/`OAUTHBEARER`'s `AccountName` (decision 2) | BL-194 |
| Loading the store at start, the 37 texts, composing one store for the three servers | BL-207 |

## Alternatives considered

- **One `Surl.Mail` library for both.** Rejected: the line reader would reference
  `Surl.Content` for nothing, and one project is one lane.
- **Answer an unknown recipient `550`.** Rejected in decision 5: it enumerates accounts for any
  peer. **`550` only after a login.** Rejected: two behaviours to build and test, and still an
  enumeration by any account holder.
- **Deliver unknown recipients to a catch-all mailbox.** Rejected: no login reaches it, so it
  would fill the store with mail nobody can read.
- **Mailboxes for every name under `--allow-anonymous`.** Rejected in decision 2: a login
  `AcceptedUnchecked` carries no name, so a SASL session could not say whose mailboxes it wants.
- **Gate SMTP delivery and `APPEND` on `--allow-uploads`.** Rejected in decision 5.
- **Rewrite one whole file per change, as MQTT does.** Rejected in decision 7: up to 256 MiB
  rewritten per delivery. **One file per mailbox.** Rejected: mailbox names are arbitrary
  strings and would need their own escaping into file names, and a flag change would still
  rewrite a mailbox's messages.
- **Accept a bare LF as a line end, as many servers once did.** Rejected in decision 8: it is
  the SMTP-smuggling defect, and upstream curl ends every line it sends with CRLF.
- **Read the rest of an oversized body so the session can go on.** Rejected in decision 8:
  ADR-0006 forbids reading past a limit.
- **Keep keywords and `\Recent`.** Rejected in decision 3: curl's `-X STORE` works with system
  flags, and RFC 3501 lets a server offer neither; a later ADR may add them.

## Consequences

- ADR-0002 decision 3's table gains two rows; `ProtocolIsolationTests` learns them in BL-189.
- The product overview's "Layers" and "Project layout" name both libraries as intent until
  BL-189 creates them.
- BL-186, BL-187 and BL-188 write the exact reply texts for decisions 4 to 7's outcomes, and
  measure against pinned upstream curl what it does with each (`452` after `DATA`, `[IN-USE]`,
  `[OVERQUOTA]`).
- With `--allow-anonymous`, POP3 sessions exclude each other, since they share one owner;
  tests that run two POP3 sessions at once need accounts.
- A mail store can grow to 256 MiB in memory without `--directory`, which is ADR-0031's
  in-memory file system's bound too; the two are separate budgets.
