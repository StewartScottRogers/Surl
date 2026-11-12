# ADR-0102 — What the mail, file, message and group options mean to the shared stores

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-642. Stewart approved layer 2 on 2026-11-04 ("Layer 2 - a configuration command line for
  real servers"): `surl` is configured as a real server, with mail domains, mailboxes, shares,
  topics and groups. That approval is his and is not reopened here; this ADR decides what each
  option means.
- **Builds on:** [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (BL-622: the shared stores and their three layer-2 seams, `IContentShareLookup`,
  `ITopicNameLookup` and the account extensions) and
  [ADR-0101](ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md)
  (BL-641: the options' names, item grammar, records, refusals and bounds). Also ADR-0003 (upstream
  curl is the only oracle), ADR-0006 (nothing tells a peer what exists; every store is bounded),
  ADR-0014 (MQTT 3.1.1), ADR-0015 (the exposure options and `ContentUploadResult.NotPermitted`),
  ADR-0032 (accounts; section 8: a refused login is indistinguishable from no such user), ADR-0033
  (log levels), ADR-0050 (the mail store), ADR-0053 (SMTP), ADR-0055 (IMAP), ADR-0073 (SMB),
  ADR-0083 (the extended servers' oracle), ADR-0099 (`Surl.Composition`).
- **Amends:** [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md)
  decision 2 (owners: a `--mailbox` address is an owner; the reserved shared-folder owner) and
  decision 5 (recipients: with a mail domain configured, addresses, aliases, catch-alls and
  relaying are resolved as decision 1 here says). With no `--mail-domain`, both stand unchanged.
  ADR-0101 decision 4's records: a `--mailbox` whose `Protocols` is null logs in on the mail
  schemes only (decision 4.3).
- **Acted on by:** BL-646 to BL-648, BL-653 to BL-666 and BL-675 to BL-677 (Consequences).

## Context

ADR-0101 fixed how the layer-2 options are written and left their meaning here. The stores exist
(ADR-0100): one `MailboxStore`, one `ContentStore`, one `MessageBus`, one `AccountBook`. Each has a
seam layer 2 fills. The questions are what every option does to its store, which replies a refused
client gets, and how a `surl` given none of these options stays exactly as it is.

Constraints: upstream curl 8.21.0 is the only oracle for every byte an original server sends
(ADR-0003); an extended server is judged by its specification (ADR-0083); nothing a peer receives
may tell it which accounts or paths exist (ADR-0006 section 3, ADR-0032 section 8, ADR-0050
decision 5); base class library only; every store bounded; secure by default, loosened only by an
option that says so (Stewart's standing rule).

### What upstream curl 8.21.0 does (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0
  (x86_64-w64-mingw32), SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (`UpstreamCurlBuilds.json`, role `reference`).
- **Tool:** `Record-CurlExchange.ps1`, 2026-11-04, on `127.0.0.1` ports 18701 to 18723, with
  `-Smtp`, `-Imap` and `-Raw`; no extension was needed. `mail.txt` is `From: a@x`, `Subject: hi`,
  an empty line and `hello`, every line ending CRLF (33 bytes). `S` below is
  `-sS --mail-from a@x -T mail.txt`, `U` is `-sS -u u:p`. Lines the recorder's defaults answer
  (greeting, `EHLO`, `CAPABILITY`, `AUTHENTICATE`) are left out.

| # | curl arguments | Recorder override | What curl sent (excerpt) | Exit | stderr / stdout |
| --- | --- | --- | --- | --- | --- |
| R1 | `S --mail-rcpt bob@elsewhere.example smtp://…:18701` | `RCPT=550 5.7.1 Relaying denied` | `MAIL FROM:<a@x> SIZE=33`, `RCPT TO:<bob@elsewhere.example>`, `QUIT` | 55 | `curl: (55) RCPT failed: 550` |
| R2 | `S --mail-rcpt ann@example.com --mail-rcpt bob@elsewhere.example smtp://…:18702` | `RCPT=250 2.1.5 Recipient OK`, then `550 5.7.1 Relaying denied` | both `RCPT`s, `QUIT`: no `DATA` | 55 | `curl: (55) RCPT failed: 550` |
| R3 | as R2 with `--mail-rcpt-allowfails` (port 18703) | as R2 | both `RCPT`s, `DATA`, the body, `.`, `QUIT` | 0 | |
| R4 | `S --mail-rcpt-allowfails --mail-rcpt bob@elsewhere.example smtp://…:18704` | `RCPT=550 5.7.1 Relaying denied` | `RCPT`, `QUIT` | 55 | `curl: (55) RCPT failed: 550 (last error)` |
| R5 | `S --mail-rcpt ann@example.com smtp://…:18705` | `DATADONE=452 4.2.2 Mailbox full` | the whole body and `.`, then `QUIT` | 8 | `curl: (8) Weird server reply` |
| R6 | `U -T mail.txt imap://…:18711/INBOX` | `APPEND=NO [OVERQUOTA] Mailbox is over its quota` | `APPEND INBOX (\Seen) {33}`, the literal, `LOGOUT` | 25 | `curl: (25) Upload failed (at start/before it took off)` (ADR-0055 row 28 measured the same exit when the `NO` comes before the `+`) |
| R7 | `U imap://…:18712/` | `LIST` answers `* LIST (\HasNoChildren) "/" INBOX`, `* LIST (\Noselect \HasChildren) "/" Shared`, `* LIST (\HasNoChildren) "/" Shared/team` | `LIST "" *` | 0 | stdout: the three `* LIST` lines exactly as sent, CRLF each |
| R8 | `U -X NAMESPACE imap://…:18713/` | `* NAMESPACE (("" "/")) NIL (("Shared/" "/"))` | `NAMESPACE` | 0 | stdout: that line as sent, CRLF |
| R9 | `U 'imap://…:18714/Shared/team;UID=1'` | none | `SELECT Shared/team`, `UID FETCH 1 BODY[]` | 0 | stdout: the literal |
| R10 | `U -X 'DELETE Shared/team' imap://…:18715/` | `DELETE=NO [NOPERM] A shared folder cannot be deleted` | `DELETE Shared/team`, `LOGOUT` | 21 | `curl: (21) Quote command returned error` |
| R11 | as R9 (port 18716) | `SELECT=NO [NONEXISTENT] Mailbox does not exist` | `SELECT Shared/team`, `LOGOUT` | 67 | `curl: (67) Select failed` |
| R12 | `-sS mqtt://…:18721/a/b` (`-Raw`) | `CONNACK` `20 02 00 00`; then `SUBACK` `90 03 00 01 80` and `DISCONNECT` `E0 00` | `CONNECT`, `SUBSCRIBE` `82 08 00 01 00 03 61 2F 62 00`, then closes | 8 | `curl: (8) Weird server reply`, empty stdout |
| R13 | as R12 without the `DISCONNECT` (port 18722) | `SUBACK` `90 03 00 01 80` only | as R12 | 8 | as R12 |
| R14 | `-sS -d hi mqtt://…:18723/a/b` (`-Raw`) | `CONNACK` only | `CONNECT`, `PUBLISH` `30 07 00 03 61 2F 62 68 69`, `DISCONNECT` `E0 00`, closes without waiting | 0 | |

So curl ends a send at the first refused `RCPT` unless `--mail-rcpt-allowfails` is given, and then
fails only when every recipient was refused (R1 to R4, as ADR-0053 rows 6 to 8 found for `5.1.1`);
any `4xx` after the body is exit 8 (R5, as ADR-0053 row 13); curl prints `LIST` and `NAMESPACE`
data as sent and selects a `/`-named mailbox by its name (R7 to R9); any tagged `NO` to a `-X`
command is exit 21 (R10); a `SUBACK` refusal ends a subscribe with exit 8 (R12, R13); and a QoS 0
publish never waits for anything (R14), so no reply to it can reach curl.

## Decision

### 0. A `surl` given no layer-2 option behaves as before

Every meaning below applies only when its option is given. With no `--mail-domain`, the mail store
resolves recipients exactly as ADR-0050 decision 5 says (owners are account names, the domain is
ignored, an unknown recipient is accepted and discarded); with no `--mailbox`, `--mail-alias`,
`--mail-catch-all` or `--shared-folder`, no owner, alias, quota or shared folder exists; with no
`--share`, `--share-access` or `--bucket`, the content store serves its one root as ADR-0031 and
ADR-0015 say; with no `--topic`, `--queue`, `--topic-map` or `--topic-access`, the bus maps names
as ADR-0100 decision 3.2 says and every logged-in account may publish and subscribe; with no
`--group`, no group exists; and every `--user` and `--user-file` account logs in on every scheme as
ADR-0032 says. The proof is that these test classes pass unchanged when the layer-2 tasks land:
`UpstreamCurlSendsMailToSurlOverSmtpTests`, `UpstreamCurlReadsMailFromSurlOverImapTests`,
`UpstreamCurlReadsMailFromSurlOverPop3Tests`, `UpstreamCurlFetchesFromSurlOverHttp11Tests`,
`UpstreamCurlFetchesFromSurlOverFtpTests`, `UpstreamCurlUploadsToSurlOverFtpTests`,
`UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `UpstreamCurlTransfersFilesWithSurlOverScpTests`,
`UpstreamCurlTransfersFilesWithSurlOverSmbTests`, `UpstreamCurlFetchesFromSurlOverTftpTests`,
`UpstreamCurlFetchesFromSurlOverGopherTests`, `UpstreamCurlTalksToSurlOverMqttTests` and
`UpstreamCurlLogsInToSurlOverMqttTests` in `Conformance/Surl.Conformance.UnitTests`, and every
existing test in `Surl.MailStore.UnitTests` (`MailboxStoreOwnerTests`, `MailRecipientPathTests`,
`MailStoreIndexTests` among them), `Surl.Content.UnitTests` (`ContentStoreTests`) and
`Surl.Authentication.UnitTests` (`AccountBookTests`, `MailAuthenticationPolicyTests`).

### 1. Mail

#### 1.1 Domains

- **Matching.** A `--mail-domain` is converted once, at composition, to its ASCII form with the
  BCL's `System.Globalization.IdnMapping.GetAscii` (`UseStd3AsciiRules` on, so IDNA 2008 / UTS 46
  as .NET applies it), then lower-cased; one trailing dot is dropped. A name `GetAscii` refuses is
  refused at start with ADR-0101 decision 5 row 10, text `option --mail-domain: not a domain name`.
  A recipient's domain is converted the same way (it may arrive as U-labels under `SMTPUTF8`, ADR-0053
  row 34) and compared ordinally with the configured ones. **Subdomains are not matched**:
  `mail.example.com` is in `example.com`'s configuration only if it is itself a `--mail-domain`.
  Why: an implicit wildcard would accept mail for names the operator never meant to serve.
- **Outside every domain is relaying.** An address literal (`[127.0.0.1]`), a domain `GetAscii`
  refuses, a domain in no `--mail-domain`, and a bare local part with no domain all name no
  configured domain. `surl` never sends mail onward, so each is refused at `RCPT` with
  `550 5.7.1 Relaying denied` (RFC 5321 section 3.6.2, which permits refusing to relay; enhanced code
  RFC 3463 `5.7.1`, delivery not authorized). Measured: R1, R2 (exit 55), R3 (with
  `--mail-rcpt-allowfails` the message goes to the other recipients, exit 0), R4 (exit 55). This
  reveals nothing about accounts: the answer depends only on the domain, which the operator's own
  configuration publishes in DNS anyway. Logging in first does not change it: there is nowhere to
  relay to.
- **Under `--allow-anonymous`** ADR-0050 decision 2 stands: every recipient, in a configured domain
  or not, is delivered to the anonymous owner's `INBOX` and nothing is refused as relaying, because
  `--allow-anonymous` is the testing loosening. `surl -v` notes it (decision 5).

#### 1.2 Owners and logins

- **`--mailbox <address>:<password>`** creates one account and one owner, both named by the
  **normalised address**: the local part as written, `@`, the domain in decision 1.1's ASCII
  lower-case form. The account joins `--user`'s and `--user-file`'s in the one `AccountBook`
  (ADR-0100 decision 4.1); a client logs in with the normalised address as its user name
  (`curl -u ann@example.com:secret imap://…`), compared ordinally as every account name is (ADR-0032
  section 1). Its address must name a configured domain and must not equal a `--user` name
  (ADR-0101 decision 5's conflicts). An address holding `:` (possible only percent-encoded, ADR-0101
  decision 3) is refused with row 10's text `the address holds a colon`, so no mailbox can collide
  with the reserved owner of decision 1.6.
- **A `--user` account** stays an owner named by its account name, as ADR-0050 decision 2 says, and
  once a domain is configured it receives mail at every configured domain by its name as local part
  (step 3 of decision 1.3). An operator adding `--mail-domain` to an existing setup therefore
  loses no mail and renames nothing.
- **Persisted owners under the old naming.** The index format (ADR-0050 decision 7) does not
  change: owner names were already strings. An owner `ann` written by an older `surl` is still the
  `--user ann` account's owner. When the operator replaces `--user ann` with `--mailbox
  ann@example.com`, the old owner `ann` is kept, unreached and counted against the bounds, exactly
  as ADR-0050 decision 2 keeps an owner whose account was removed; `surl` never moves or deletes
  mail because the configuration changed. `surl -v` notes each owner held but unreached at start.
- **`XOAUTH2` and `OAUTHBEARER`** (ADR-0050 decision 2) act as the owner named by the client's
  `user=` only when that name is an account; they can never reach the reserved shared-folder owner
  (decision 1.6), which is not an account.

#### 1.3 Resolving a recipient

With at least one `--mail-domain`, a `RCPT TO` address in a configured domain is resolved in this
order, the first step that matches deciding; local parts compare as ADR-0050 decision 5 says
(ordinally, then the one case-insensitive match if exactly one):

1. a `--mail-alias` of that address: each of its targets is resolved again from step 1;
2. a `--mailbox` of that address: its owner;
3. a `--user` or `--user-file` account named by the local part: its owner;
4. the domain's `--mail-catch-all`: its target, resolved from step 1;
5. none: **accepted exactly as a known recipient and discarded at delivery**, with ADR-0050
   decision 5's note. Refusing an unknown local part (`550 5.1.1`) would let any peer enumerate
   the mailboxes, which ADR-0032 section 8 forbids; with a domain configured that rule is unchanged.

- **Aliases and catch-alls are static, so their mistakes are found at start**, refused with exit 2
  and these texts (added to ADR-0101 decision 5 row 18): an alias or catch-all target outside every
  configured domain, `(2) option --mail-alias: target <address> is not in any --mail-domain` (and
  `--mail-catch-all`); a target that resolves to no owner (step 5),
  `(2) option --mail-alias: target <address> names no mailbox`; an alias that reaches itself through
  other aliases or catch-alls, `(2) option --mail-alias: alias <address> includes itself`. An address
  is not a secret; a password never appears. So at runtime an alias always ends in owners, and
  expansion needs no depth limit beyond the alias count.
- **One copy per owner per message.** However many `RCPT`s, aliases and catch-alls name an owner,
  it receives one copy (ADR-0053's "two `RCPT`s naming the same account deliver one copy").
- **`--mail-catch-all`** widens access (ADR-0101 decision 4: warned at every start): mail to every
  unknown local part of its domain is kept instead of discarded.
- **POP3 and IMAP are untouched by resolution**: they read an owner's mailboxes after a login.

#### 1.4 `protocols=`, `quota=` and `folders=`

- **`protocols=`** limits where the mailbox's account can log in (decision 4.3). Absent, a
  `--mailbox` account logs in on the mail schemes only: `smtp`, `smtps`, `imap`, `imaps`, `pop3`,
  `pop3s`. **Why:** a mailbox password is a mail credential; letting it open FTP or SMB would widen
  access beyond what the option names. Given, it replaces that list and may name any scheme a
  registered server speaks (`protocols=imap,ftp` opens FTP on purpose).
- **`quota=<bytes>`** bounds the bytes of every message in the owner's own mailboxes, each copy at
  its full size (ADR-0050 decision 7's shared message files do not discount it). `0` is refused
  (row 10, `key quota needs a value above 0`). The quota is configuration, never persisted; usage is
  counted from the index at load, and an owner already over its quota at start keeps its mail and
  receives nothing more until it is under. ADR-0050 decision 6's store-wide bounds still apply on
  top. Past the quota nothing is stored and the operation gets a new typed outcome,
  `QuotaExceeded`, distinct from `StoreFull` so the words differ:

  | Where | Reply | Measured |
  | --- | --- | --- |
  | SMTP, after the body, when any resolved owner would pass its quota | `452 4.2.2 Mailbox full` (RFC 5321 section 4.2.2's `452`, transient so the sender retries once mail is read; RFC 3463 `4.2.2` mailbox full), nothing delivered to any recipient: a delivery is all or none (ADR-0050 decision 3) | R5, exit 8 |
  | IMAP `APPEND`, decided from the literal's `{n}` before the `+`, as ADR-0055 decides `[TOOBIG]` | tagged `NO [OVERQUOTA] Mailbox is over its quota` (RFC 5530 section 3) | R6, exit 25 |
  | IMAP `COPY`, `MOVE` (a `-X` command) | the same tagged `NO` | R10's rule: any tagged `NO` to `-X` is exit 21 |

  The quota is not checked at `RCPT`: a `452` there would answer only for mailboxes that exist, an
  enumeration. After `DATA` it says only that some recipient of this message is full. A message to
  an over-quota owner and to others is refused for all of them; the sender's retry reaches them once
  the owner has room. Under `--allow-anonymous` the anonymous owner has no quota.
- **`folders=<name>,...`** creates each named mailbox for the owner at start if it is missing,
  after `INBOX` (ADR-0050 decision 3), with the index written once. Names follow ADR-0050 decision 3's
  rules; `INBOX` in any case is already there and is skipped. A folder removed from the option later
  is kept: configuration never deletes mail.

#### 1.5 What decision 1 leaves as it was

ADR-0053's every other reply, the 100-recipient bound, `--allow-uploads` not gating mail (ADR-0050
decision 5), the bounds and persistence of ADR-0050 decisions 6 and 7.

#### 1.6 Shared folders

- **The store.** Shared folders are mailboxes of one **reserved owner** named `:shared`, which no
  account name can equal (`--user` and `--user-file` split at the first `:`, ADR-0032 section 1, and
  a `--mailbox` address may not hold one, decision 1.2) and no login reaches directly. A
  `--shared-folder <name>=<members>` creates mailbox `<name>` of that owner at start if missing. The
  index format is unchanged; an index written before layer 2 loads as before. A shared folder no
  longer configured is kept, unreached. Shared folders count against the store-wide bounds and
  against no member's quota.
- **Members** are account names and `@group`s, expanded at composition (decision 4.1); `@everyone`
  (decision 4.2) is refused here (`(2) option --shared-folder: member @everyone is not allowed
  here`), because the anonymous owner is one shared mailbox set already.
- **IMAP shows it as `Shared/<name>`** in the one hierarchy (`/`, ADR-0055 decision 6), and
  `NAMESPACE` names it as RFC 2342's shared namespace: for a session whose account is a member of
  at least one shared folder, `* NAMESPACE (("" "/")) NIL (("Shared/" "/"))`, else ADR-0055's
  `* NAMESPACE (("" "/")) NIL NIL` unchanged. `LIST` shows `Shared` as `\Noselect \HasChildren`
  and each folder the member may use; curl prints both as sent (R7, R8) and selects
  `Shared/<name>` by name (R9). Both plain `LIST` names and `NAMESPACE` are given: curl reads the
  first, other IMAP clients the second.
- **Rights.** Every member may `SELECT` (read-write), `FETCH`, `STORE` any flag, `EXPUNGE`,
  `APPEND`, and `COPY` or `MOVE` in and out. Flags are the folder's, shared by its members (no
  per-member `\Seen`; RFC 4314 ACLs are not offered). Members are equals: who may delete messages is
  every member. No one may `DELETE` or `RENAME` a shared folder or `Shared`, or `CREATE` under them:
  tagged `NO [NOPERM] A shared folder cannot be deleted` (`renamed`, `created under` in place of
  `deleted`; RFC 5530 section 3; R10, exit 21).
- **A non-member** sees no `Shared/` entry for a folder it is not in, and `SELECT`, `EXAMINE`,
  `STATUS`, `APPEND` or `COPY` to it get exactly ADR-0055's missing-mailbox replies (R11, exit 67):
  a peer cannot learn that a shared folder exists.
- **A personal mailbox named `Shared` or under `Shared/`**, created before any shared folder was
  configured, is kept, unreached, while at least one shared folder is configured, and `surl` notes it
  at start; with shared folders configured, `CREATE` of such a name gets the `NO [NOPERM]` above.
- **POP3 never sees shared folders** (its maildrop is `INBOX`, ADR-0050 decision 4), and SMTP never
  delivers to one: a shared folder has no address. Under `--allow-anonymous` shared folders are not
  reached, as every account's mailboxes are not (ADR-0050 decision 2).

### 2. Files

#### 2.1 `--share <name>=<path>[;readonly][;protocols=<scheme>,...]`

- **A share is a top-level folder of the content store whose files live at `<path>` on disk**,
  through `IContentShareLookup` (ADR-0100 decision 2.6). `<path>` is read as `--directory`'s path
  is (ADR-0018: relative to the working directory) and must be an existing directory at start
  (ADR-0101 decision 5 row 19, exit 37). A share is on disk **with or without `--directory`**:
  without it, the root stays in memory and each share is still its own directory; `surl`'s own state
  (`.surl`) lives only under `--directory` (or in memory), never inside a share.
- **The same rules as the root**, inside every share: ADR-0015's exposure options (dot-files,
  listings, links), `RequestPathSegments`' refusals, `..` stopping at the share's own root, a
  symbolic link out of the share refused as one out of the root is (ADR-0002 decision 2), and any
  `.surl` inside the share hidden (ADR-0031 decision 5).
- **No overlap.** A share whose full path equals, lies inside, or contains `--directory`'s path or
  another share's is refused at start, exit 2,
  `(2) option --share: share <name> overlaps <other share name or --directory>`. **Why:** one file
  reachable by two names would be governed by two sets of access rules, and the looser would win.
- **The name** is one path segment that `RequestPathSegments.CheckSegment` accepts and not `.surl`
  (row 10, `not a valid folder name`). It shadows a root entry of the same name: while the share
  exists, that root entry is unreachable over every protocol, and `surl -v` notes it.
- **Listings.** The root's listing shows each share the caller may read (2.2) and whose
  `protocols=` admits the scheme, as a directory beside the root's own entries, whenever the root's
  listing is shown at all (ADR-0006 section 2's `--list-directories`). SMB's tree connect names a
  top-level folder already (ADR-0073), so `smb://h/<share>/…` reaches a share with no SMB change.
- **`readonly`**: every write in the share - upload, append, rename, delete, folder create or
  remove - is `NotPermitted` (ADR-0015), whatever `--allow-uploads` and access say.
- **`protocols=`** (absent: every file scheme) limits which schemes see the share at all; to any
  other scheme it is missing, as if not configured. A scheme and its TLS twin are one entry
  (decision 4.3).

#### 2.2 `--share-access <share>=<member>:<r|rw>,...`

- **Per share.** A share no `--share-access` names is open exactly as the root is today: every
  caller the server lets in may read, and may write when `--allow-uploads` is on. A share with at
  least one `--share-access` row is **closed to every account not granted, and to anonymous
  callers**. **Why per share:** a global default would close shares the operator never meant to
  restrict; within a share, the operator who lists anyone has said who.
- **`r`** reads and lists; **`rw`** also uploads, appends, renames, deletes and creates or removes
  folders. A write still needs `--allow-uploads` and a share without `readonly`: the three are
  ANDed, so no access row loosens ADR-0006's default.
- **Anonymous callers** are TFTP and Gopher (no login, ADR-0032), an object-storage request with no
  signature, and every login `--allow-anonymous` accepted unchecked (it carries no account name,
  ADR-0049 section 6). They reach a closed share only through the `@everyone` member (decision 4.2),
  which widens access and is warned at start:
  `surl: warning: --share-access: share <name> grants <r|rw> to every caller, anonymous included`.
  `--allow-anonymous` alone never opens a closed share.
- **The root has no access rows**: it is served as today. An operator who wants only access-checked
  files serves shares and no `--directory`.

#### 2.3 What a denied caller gets: the replies the servers already give

The store has two outcomes, so no server gains a new reply and no new byte reaches upstream curl:

- **A share the caller may not read** (no grant, or a scheme `protocols=` excludes) is **missing**:
  not in the root listing, and every path in it answered as a path that does not exist. A peer
  cannot learn that the share exists (ADR-0006 section 3).
- **A write the caller may not make, in a share it may read** (`r` only, `readonly`, or
  `--allow-uploads` off) is `ContentUploadResult.NotPermitted` (ADR-0015 decision 5) and its kin
  for rename and delete: the caller can already see the files, so there is nothing to hide.

| Server | Read denied (missing) | Write denied (`NotPermitted`) |
| --- | --- | --- |
| HTTP (and HTTP/2, HTTP/3) | `404 Not Found` (ADR-0008) | `405` for every `PUT` (ADR-0008; the HTTP server takes no uploads) |
| FTP | `550` on `SIZE`/`RETR`/`CWD`/`LIST` (ADR-0052; curl 78, 19 or 9) | `550` on `STOR` (ADR-0052 row 18; curl 25) |
| SFTP, SCP | `NO_SUCH_FILE` (curl 78); ADR-0054's SCP download refusal (curl 78) | `PERMISSION_DENIED` (curl 9); `\x01scp: <target>: Permission denied` (ADR-0054; curl 25) |
| SMB | tree connect `ERRSRV/ERRinvnetname` or create `ERRDOS/ERRbadfile` (ADR-0073 rows 11, 14; curl 78) | `ERRDOS/ERRnoaccess` (ADR-0073; curl 9) |
| TFTP | ERROR 1 `File not found` (ADR-0013; curl 68) | ERROR 2 `Access violation` (ADR-0013; curl 69) |
| Gopher | ADR-0012's missing-selector reply | n/a (no writes) |
| RTSP | `404 Not Found` (ADR-0074) | `403 Forbidden` (ADR-0074) |
| S3 | `404 NoSuchKey` / `NoSuchBucket` (S3 API "Error responses") | `403 AccessDenied` (S3 API "List of error codes") |
| AzureStorage | `404 BlobNotFound` / `ContainerNotFound` (Azure "Blob service error codes") | `403 AuthorizationPermissionMismatch` (Azure "Common REST API error codes") |
| GoogleCloudStorage | `404` (GCS JSON API "HTTP status and error codes") | `403` reason `forbidden` (same page) |
| OciRegistry | `404 NAME_UNKNOWN` / `BLOB_UNKNOWN` / `MANIFEST_UNKNOWN` (OCI Distribution Specification, "Error Codes") | `403 DENIED` (same section) |
| CoAP | `4.04 Not Found` (RFC 7252 section 5.9.2.5) | `4.03 Forbidden` (RFC 7252 section 5.9.2.4) |

Every original-server row is a reply its own ADR already measured against pinned upstream curl, with
the exit code given there; each extended row cites its specification (ADR-0083).

#### 2.4 `--bucket <name>=<path>`

- **Maps a bucket name onto a content path** for the object-storage servers only (S3, AzureStorage,
  GoogleCloudStorage, and the first segment of an OciRegistry repository name): `<path>` is a
  content-store path (`/media/photos`, or `/team/photos` inside the share `team`), read with the
  root's and the shares' rules. ADR-0100 decision 2.1's mapping then applies under it. At start the
  folder must exist when it is on disk (row 19's text with `bucket` for `share`, exit 37); in
  memory it is created empty.
- **Listing buckets** (`ListBuckets`, `List Containers`, `buckets.list`) gives the top-level folders
  ADR-0100 decision 2.1 names plus every `--bucket`, a top-level folder of the same name as a bucket
  being shadowed by it for the object servers. The original file servers never see bucket names.
- **Access** is that of the share or root the path lies in (2.2), checked on the mapped path.

### 3. Messages

#### 3.1 `--topic <filter>[;retain][;qos=<0|1|2>]`

- **Declares** a canonical filter (ADR-0100 decision 3.2) whose attributes apply to every topic it
  matches; several matching declarations combine: `retain` if any says it, the lowest `qos`.
- **`retain`**: every publish on a matching topic writes the retained slot, as if its `RETAIN` flag
  were set (MQTT 3.1.1 already retains every publish, ADR-0014 decision 1; this lets MQTT 5, AMQP,
  NATS, STOMP and Kafka publishers leave a message for a later MQTT subscriber).
- **`qos=<n>`**: a publish on a matching topic is carried by the bus at QoS at most `n`, so no
  subscriber is granted more (MQTT 5.0 section 3.8.3.1's `min`). The publisher is never refused for
  a higher QoS: MQTT 3.1.1 has no way to say so, and MQTT 5 says it per server in `CONNACK`
  (`Maximum QoS`), not per topic.
- **A declared topic without wildcards exists from start**: its log is created empty (ADR-0100
  decision 3.3), so a Kafka `Metadata` request lists it before anything is published. Undeclared
  topics stay as they are: publishing on one is never refused for being undeclared.

#### 3.2 `--queue <name>[;durable]`

- **Declares a named queue** (ADR-0100 decision 3.3) that exists from start, is never exclusive and
  never auto-deleted. `durable` persists it and its messages under `.surl/bus` (ADR-0100 decision
  3.7); without it the queue is recreated empty at every start.
- **A client may not delete a declared queue**: AMQP 0-9-1 `queue.delete` of one closes the channel
  `403 ACCESS_REFUSED` (AMQP 0-9-1 section 1.2, reply codes); AMQP 1.0 and STOMP cannot delete a
  queue. Binding it to more filters (AMQP 0-9-1 `queue.bind`) is allowed, subject to 3.4.

#### 3.3 `--topic-map <scheme>:<name>=<scheme>:<name>`

- **An alias of one exact name.** The left side's name, on the left side's scheme, means the
  canonical topic the right side's name has on its scheme by ADR-0100 decision 3.2's default
  mapping: `amqp:orders=mqtt:shop/orders` makes AMQP 0-9-1's routing key `orders` the canonical topic
  `shop/orders`. It applies **both ways**: a publish of `orders` over AMQP lands on `shop/orders`, and
  a message on `shop/orders` is delivered to an AMQP binding of `orders` with routing key `orders`.
  The left name's default mapping (`orders`) is no longer reached from that scheme.
- **Exact names only**: a wildcard filter on the left scheme is mapped by the default rules alone,
  and a right side that is not a valid name on its scheme is refused at start (row 10, `not a valid
  name for <scheme>`). Maps do not chain: the right side is resolved by the default mapping only.
  A scheme and its TLS twin share one map (decision 4.3).

#### 3.4 `--topic-access <filter>=<member>:<pub|sub|pubsub>,...`

- **Global, once given.** With no `--topic-access`, every logged-in account (and every anonymous
  caller a broker lets in) may publish and subscribe, as today. With at least one, **every publish
  and subscribe needs a grant**; an account with none, and every anonymous caller, is refused unless
  `@everyone` is granted (decision 4.2), which widens access and is warned at start:
  `surl: warning: --topic-access: <filter> grants <pub|sub|pubsub> to every caller, anonymous included`.
- **A publish** on canonical topic `t` is allowed when some grant with `pub` or `pubsub` to the
  caller has a filter matching `t`. **A subscribe** to filter `f` is allowed when some grant with
  `sub` or `pubsub` to the caller has a filter that **covers** `f`: level by level, a grant `#`
  covers the rest, a grant `+` covers any one level but `#`, and a literal level covers only itself;
  MQTT's `$` rule (ADR-0100 decision 3.2) holds, so `#` never covers a `$` topic. A subscription
  allowed this way receives only what the grant covers, retained messages included.
- **Queues** are named in access rows as the filter `$queue/<name>`: `sub` lets a caller consume
  from the queue, `pub` lets it send to the queue directly (AMQP 0-9-1's default exchange, AMQP 1.0
  and STOMP `/queue/<name>`). The `$` rule keeps a `#` grant from opening every queue. Binding a
  queue to a filter needs `sub` on that filter.
- **What a denied caller gets**, the same answer whether or not the topic exists (ADR-0006 section 3):

  | Broker | Denied subscribe | Denied publish | Reference |
  | --- | --- | --- | --- |
  | MQTT 3.1.1 (`mqtt`, `mqtts`) | `SUBACK` return code `0x80` for that filter, then ADR-0014's `DISCONNECT` | dropped: nothing retained, delivered or logged; noted with `IExchangeLog.Note`; the connection goes on | MQTT 3.1.1 section 3.9.3; measured R12, R13 (exit 8) and R14 (a QoS 0 publish waits for nothing, exit 0; 3.1.1 has no negative acknowledgement) |
  | MQTT 5 | `SUBACK` reason `0x87` Not authorized | `PUBACK` or `PUBREC` reason `0x87` at QoS 1 or 2; dropped at QoS 0 | MQTT 5.0 sections 3.9.3, 3.4.2.1, 3.5.2.1 |
  | AMQP 0-9-1 | `basic.consume` or `queue.bind`: channel close `403 ACCESS_REFUSED` | `basic.publish`: channel close `403 ACCESS_REFUSED` | AMQP 0-9-1 section 1.2 (reply codes) |
  | AMQP 1.0 | `attach` answered with a `detach` carrying `amqp:unauthorized-access` | the same on the sending link's `attach`; a transfer to a denied address is rejected with `amqp:unauthorized-access` | AMQP 1.0 part 2 section 2.8.15 |
  | Kafka | `Fetch`, `ListOffsets`: `TOPIC_AUTHORIZATION_FAILED` (29) | `Produce`: `TOPIC_AUTHORIZATION_FAILED` (29) | Kafka protocol guide, "Error Codes" |
  | NATS | `-ERR 'Permissions Violation for Subscription to <subject>'`, the connection kept | `-ERR 'Permissions Violation for Publish to <subject>'`, the connection kept | NATS client protocol, "-ERR" |
  | STOMP 1.2 | `ERROR` frame, then close | `ERROR` frame, then close | STOMP 1.2 "ERROR" |
  | Redis pub/sub (BL-678) | `-NOPERM` error reply for the channel | `-NOPERM` error reply | Redis "ACL" (`NOPERM` errors) |

#### 3.5 What BL-664 settled in `Surl.MessageBus`

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-664,
while building 3.1 to 3.4 into `Surl.MessageBus.UnitLibrary`; recorded here by BL-704.

- **The caller is an account name.** `MessageBus.Connect(string?)`, `MessageBus.Publish(BusMessage,
  string?)` and `MessageBus.PublishToQueue(string, BusMessage, string?)` take the caller's account
  name, `null` for an anonymous caller; the parameterless `Connect()`, `Publish(message)` and
  `PublishToQueue(name, message)` are anonymous. **Why:** groups are already expanded at composition
  (4.1), so an account name is all 3.4 needs; and a server not yet passing its caller is refused once
  `--topic-access` is given rather than let through.
- **Access does not depend on the scheme.** 3.4's grants are global, so the bus checks them on
  canonical topics alone; the scheme reaches the bus only through `TopicNameLookup` (exposed as
  `MessageBus.Names`, ADR-0100's planned `ITopicNameLookup`), where it chooses the name mapping.
- **One refusal, `MessageBusOutcome.AccessDenied`.** A denied publish, subscribe, `BindQueue`, send to
  a queue or consume answers it, checked before anything that would tell whether the topic or queue
  exists (unknown, locked), so the refusal reveals nothing (ADR-0006 section 3). Deleting a declared
  queue (3.2) answers it too, which the AMQP 0-9-1 server sends as `403 ACCESS_REFUSED`. Unbinding
  needs no grant: it only narrows what a queue receives.
- **A queue's name is one level.** In `$queue/<name>` the name is a single level whatever it holds,
  `/` included, so `$queue/+` grants every queue and no queue name can escape a literal grant.
- **The schemes `TopicNameLookup` knows** are the messaging schemes of ADR-0083's table with their
  TLS twins: `mqtt`, `mqtts`, `mqtt5`, `mqtt5s`, `mqtt5+ws`, `amqp`, `amqps`, `amqp1`, `amqp1s`,
  `kafka`, `kafkas`, `nats`, `natss`, `stomp` and `stomps`. Redis pub/sub is left to BL-678, until its
  channel naming is decided.
- **A STOMP or AMQP 1.0 alias names a topic address.** In `--topic-map`, a name on `stomp` or `amqp1`
  is a `/topic/<t>` destination or address; queues are not aliased.
- **Of two aliases with one target, the first is sent back.** Inbound each alias reaches its target;
  outbound, a message on that canonical topic is named by the first `--topic-map` given for the
  scheme. An alias's own name mapped twice on one scheme is refused at start.
- **A declared queue whose flags changed starts empty.** A queue the files persisted as durable but
  now declared without `durable` is made again empty at load, as a non-durable declared queue is at
  every start; `LoadAsync` accepts a bus holding only its still-empty declarations.

#### 3.6 What BL-991 settled in `Surl.Composition`

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11, in BL-991,
while composing 3.1 to 3.4 into the bus (`MessageBusComposition`). No byte any server sends changes.

- **Refused at start, exit 2, in this order:** a `--topic` filter that is not a canonical filter,
  `(2) option --topic: topic <filter> is not a valid topic filter`; more than 1000 `--queue`s (the
  bus's `DefaultMaxQueues`), `(2) option --queue: more than 1000 queues`; a `--topic-map` scheme
  `TopicNameLookup` does not know, `(2) option --topic-map: <scheme> is not a messaging scheme`; a
  name not valid on its scheme, 3.3's `(2) option --topic-map: not a valid name for <scheme>` naming
  the left scheme when the left name is the bad one, else the right; a name a map on the same scheme
  or its TLS twin already aliases (`mqtt:a` then `mqtts:a`), row 14's
  `(2) option --topic-map: topic map <scheme>:<name> is given twice`; a `--topic-access` filter that
  is not a canonical filter, `(2) option --topic-access: topic <filter> is not a valid topic filter`;
  then row 18's member naming no account or group. **Why:** each would otherwise reach the bus's
  constructor as an `ArgumentException`; a filter and a scheme are names, never secrets, so they may
  be quoted, as row 14 quotes names.
- **A member reached twice in one row keeps the union of its rights** (`@staff:pub` and `alice:sub`
  give a `staff` member `alice` `pubsub`), one `TopicAccessGrant` per member per filter, as a share
  member keeps its wider access (2.2).
- **The `-v` notes** of decision 5 put `: ` before a topic's first attribute and `, ` between them:
  `* Topic shop/#: retained, QoS at most 1`, `* Topic news: QoS at most 0`; `Topic access: <n> grants`
  counts the grants after group expansion. They follow the share notes; the `@everyone` warning
  follows the share warning.

### 4. Accounts and groups

#### 4.1 `--group <name>=<member>,...`

- Members are account names (`--user`, `--user-file`, `--mailbox` addresses) and `@<group>`s.
  **Groups nest**; a cycle is refused at start (ADR-0101 decision 5 row 18). A member naming no
  account or group is refused at start (row 18). A member reached twice through two groups counts
  once.
- **Groups are expanded at composition** (`Surl.Composition`, planning name
  `AccountGroupExpansion`), after `--user-file` is read, into sets of account names; the stores see
  only account names and the `@everyone` marker, never a group. So no store, server or contract in
  `Surl.Protocol.Abstractions` learns about groups.

#### 4.2 `@everyone`

A reserved member meaning every caller, logged in or anonymous. It is accepted only in
`--share-access` and `--topic-access`, where it widens access and is warned (decisions 2.2 and 3.4);
anywhere else it is refused, `(2) option --<option>: member @everyone is not allowed here`, and
`--group everyone=…` is refused, `(2) option --group: group everyone is reserved`. **Why one
reserved word:** ADR-0101 left a "member that grants every caller" to this ADR; without it a share or
topic restricted by access rows could never serve TFTP, Gopher or `--allow-anonymous` callers.

#### 4.3 `protocols=`: one view of the accounts per scheme

- **Where it is enforced:** `Surl.Composition` hands each server a view of `AuthenticationPolicy`
  (and its SASL and HTTP siblings) that sees only the accounts allowed on that server's scheme
  (BL-647). No contract in `Surl.Protocol.Abstractions` changes, and every login method - password,
  SASL, Digest, NTLM, Kerberos's account check, SSH public keys - is covered because the view
  filters accounts, not methods. CoAP's PSK lookup, built from the account list (ADR-0100 decision
  4.2), is built from the same view.
- **Refused as no such account**, with ADR-0032 section 8's same verdict, note and one-second delay:
  a peer cannot tell an account restricted from its scheme from an account that does not exist.
- **A scheme and its TLS twin are one entry**: naming `imap` admits `imaps` and the other way round,
  and likewise `smtp`/`smtps`, `pop3`/`pop3s`, `ftp`/`ftps`, `http`/`https` (and HTTP/2 and HTTP/3,
  which are `https`), `ws`/`wss`, `mqtt`/`mqtts`, `ldap`/`ldaps`, `gopher`/`gophers`, `smb`/`smbs`,
  `telnet`, `dict`, `rtsp`, and each extended pair (`mqtt5`/`mqtt5s`, `coap`/`coaps`, …) as its
  server registers both. **Why:** TLS is the transport, not another protocol; `STARTTLS` already
  carries `imap` to TLS on the same scheme, and an operator who writes `imap` means the mail protocol.
- **Defaults:** a `--user` or `--user-file` account logs in on every scheme, as today; a
  `--mailbox` account on the mail schemes (decision 1.4).
- **Amendment, 2026-11-06 (BL-881) - every scheme gets its view, even with no restricted account.**
  `SchemeAuthenticationPolicies.For(scheme)` composes one policy per scheme over
  `AuthenticationSettings.ForScheme`, cached, and never hands back the start's one policy. BL-881 was
  written to reuse the one policy while no account is restricted, but ADR-0132 (written after it)
  lets a test add an account with `protocols=` while the server runs, and a server holding the one
  policy would then admit that account on every scheme. Each view reads the same changing account
  book, so with no restricted account every view admits exactly what the one policy admits: the
  servers answer exactly as before. The SSH server is composed once for `scp` and once for `sftp`,
  each registered alone through `SingleSchemeServer`, so `protocols=sftp` admits one and not the
  other. Decided by Claude under Stewart's delegation.

### 5. What `Surl.Composition` composes, and what `surl -v` notes

Composition happens once per `ComposedServing` (ADR-0100 decision 1), after the data-directory lock,
`--user-file` and `--mailbox-file` are read, in this order: groups, accounts and their scheme views,
then each store's configuration, then the stores are loaded (ADR-0100's order). Every refusal of
decisions 1 to 4 that can be found from the configuration alone is found here, before any listener
binds. Each note is written at start at ADR-0033's verbose level, after the warnings, never holding a
password:

| Option | Composed into | `surl -v` note at start |
| --- | --- | --- |
| `--group` | `AccountGroupExpansion`: name to account-name set | `Group <name>: <n> accounts` |
| `--mailbox`, `--mailbox-file` | an `Account` per mailbox in the `AccountBook`, restricted by its schemes; an owner, quota and folders in `MailDomainConfiguration` | `Mailbox <address>: logins on <schemes>[, quota <bytes> bytes][, <n> folders]` |
| `--mail-domain`, `--mail-alias`, `--mail-catch-all` | `MailDomainConfiguration` (domains, aliases resolved to owner sets, catch-alls) handed to `MailboxStore` | `Mail domains: <domains>`; `Alias <address>: <n> owners`; the catch-all warning (ADR-0101); `Mail domains not applied: --allow-anonymous delivers every recipient to one mailbox set` under `--allow-anonymous` |
| `--shared-folder` | shared folders and their member sets in `MailDomainConfiguration` | `Shared folder Shared/<name>: <n> members` |
| (start) | | `Mail owner <name> is held but not an account; its mail is kept, unreached` per such owner; `Root entry <name> is hidden by share <name>`; `Personal mailboxes under Shared/ are kept, unreached` |
| `--share`, `--share-access`, `--bucket` | the `IContentShareLookup` implementation (BL-655) with each share's file system, flags, schemes and grant sets | `Share <name>: <full path>[, read-only][, schemes <schemes>][, access <n> grants]`; `Bucket <name>: <content path>`; the `@everyone` warning |
| `--topic`, `--queue`, `--topic-map`, `--topic-access` | the `ITopicNameLookup` implementation (BL-664): declarations, maps and grants | `Topic <filter>[: retained][, QoS at most <n>]`; `Queue <name>[: durable]`; `Topic map <scheme>:<name> = <scheme>:<name>`; `Topic access: <n> grants`; the `@everyone` warning |

A share's full path is the operator's own configuration, so it may be noted; the exchange log never
writes it to a peer.

## Alternatives considered

- **Refuse an unknown local part `550 5.1.1` once a domain is configured.** Rejected: it enumerates
  mailboxes for any peer, which ADR-0032 section 8 forbids; ADR-0050 decision 5's reason holds with
  domains too.
- **Check quotas at `RCPT`.** Rejected: a `452` there answers only for existing mailboxes. **Deliver
  to the recipients under quota and drop the full one.** Rejected: silent loss; SMTP has one reply
  after `DATA`.
- **Match subdomains of a `--mail-domain`.** Rejected: it serves names the operator did not list.
- **A `--mailbox` account unrestricted by default.** Rejected: a mail password opening file servers
  widens access beyond what the option names.
- **Shared folders under RFC 2342 `NAMESPACE` only.** Rejected: curl never sends `NAMESPACE` and
  lists with `LIST "" *` (R7), so the folders must be plain `LIST` names too.
- **RFC 4314 ACLs for shared folders.** Rejected for now: every member equal is what the option
  says; a later ADR may add rights.
- **A global share-access default.** Rejected: it would close shares the operator never restricted.
- **A distinct "forbidden" reply for a share the caller cannot read.** Rejected: it tells the peer
  the share exists (ADR-0006 section 3). For writes in a readable share nothing is hidden, so the
  existing `NotPermitted` replies are used.
- **Closing the MQTT 3.1.1 connection on a denied publish.** Rejected: curl has already sent
  `DISCONNECT` and gone (R14), so the close tells nobody anything; dropping and noting is what the
  protocol allows.
- **Per-filter topic-access defaults.** Rejected: filters overlap, so "the topic's own rows" has no
  single answer; one global switch is predictable.
- **Groups resolved inside each store.** Rejected: four stores would each learn groups, cycles and
  `@` syntax; expanding once at composition keeps every store on account names.

## Consequences

- No byte any original server sends upstream curl changes for a `surl` given no layer-2 option
  (decision 0). Every new original-server reply is measured above (R1 to R14) against the pinned
  Windows reference build; the file servers gain none (decision 2.3).
- ADR-0050 carries an "Amended" line naming this ADR for decisions 2 and 5. ADR-0101 decision 5
  row 18 gains the alias, catch-all and `@everyone` texts of decisions 1.3 and 4.2, and the share
  overlap text of decision 2.1, each added by the task that builds its option.
- `Documentation/Product/Requirements.md`'s FR-058 and FR-059 cite this ADR.
- **The build tasks**, and where this ADR changes their Goal:
  - **BL-646** (`--group`): adds `@everyone` as reserved (4.2), refused in `--group` and
    `--shared-folder`; groups nest (4.1).
  - **BL-647** (per-scheme account views): a scheme admits its TLS twin (4.3); the CoAP PSK lookup
    is built from the view; a `--mailbox` account defaults to the mail schemes.
  - **BL-648** (domains, addresses, aliases, catch-alls in `MailboxStore`): resolution order 1.3,
    unknown local parts accepted and discarded, a new typed outcome for relaying answered `550 5.7.1`,
    one copy per owner, IDNA through `IdnMapping`, no subdomain match; the index format does not
    change.
  - **BL-653** (`--mail-domain`, `--mailbox`, `--mailbox-file`): the account is named by the
    normalised address; `:` in an address refused; `protocols=` absent means the mail schemes.
  - **BL-654** (`--mail-alias`, `--mail-catch-all`, `--shared-folder`): alias and catch-all targets
    checked at start with 1.3's three refusals.
  - **BL-655** (shares and buckets in `ContentStore`): shares on disk with or without
    `--directory`, overlap refused, a share shadows a root entry, buckets for the object servers only.
  - **BL-656** (share access): per-share closing, `r`/`rw` ANDed with `--allow-uploads` and
    `readonly`, denied read is missing and denied write `NotPermitted` - so the server tasks
    **BL-657 to BL-662 add no new reply**: each passes its caller and scheme and keeps its existing
    missing and `NotPermitted` answers (2.3); their Goals' "status measured by BL-642's ADR" is the
    existing reply in decision 2.3's table.
  - **BL-663** (`--share`, `--share-access`, `--bucket`): the `@everyone` warning and the overlap
    refusal.
  - **BL-664** (topics, queues, maps, access in `MessageBus`): 3.1 to 3.4, with queues as
    `$queue/<name>` in grants and maps applying both ways.
  - **BL-665** (MQTT 3.1.1 access): denied subscribe `SUBACK 0x80` then `DISCONNECT` (R12), denied
    publish dropped and noted (R14).
  - **BL-666** (`--topic`, `--queue`, `--topic-map`, `--topic-access`): the `@everyone` warning.
  - **BL-675** (quotas, folders, shared folders in `MailboxStore`): the `QuotaExceeded` outcome, the
    reserved owner `:shared`, members equal, no index change.
  - **BL-676** (SMTP): `550 5.7.1 Relaying denied` at `RCPT` (R1 to R4), `452 4.2.2 Mailbox full`
    after the body for a quota (R5).
  - **BL-677** (IMAP): `Shared/<name>` in `LIST`, the shared `NAMESPACE` (R7, R8),
    `NO [OVERQUOTA] Mailbox is over its quota` before the `+` (R6), `NO [NOPERM]` for `DELETE`,
    `RENAME` and `CREATE` under `Shared/` (R10).
  - BL-649, BL-650, BL-651 and BL-652 fall in the same number range but are not layer-2 tasks; this
    ADR does not change them. BL-671 (LDAP as an account source) decides within decision 4: an LDAP account
    is an account name like any other to groups, access rows and scheme views.
- The extended brokers' and object servers' own build tasks word decisions 2.3 and 3.4's denials
  from the specifications cited; the Curl port never decides any of them (ADR-0083).
